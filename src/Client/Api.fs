module AssetTracker.Client.Api

open System
open Fable.Core
open Fetch
open Thoth.Json
open AssetTracker.Shared

// Wire contract, written out by hand on purpose: the server serializes with
// FSharp.SystemTextJson (camelCase fields, camelCase fieldless-union tags,
// options as value-or-null). Explicit coders here make the contract greppable
// and keep Thoth's auto-coder conventions out of the protocol.

// ── Union tag maps (camelCase, mirror Program.jsonOptions) ──────────────

let deviceTypeToWire =
    function
    | Desktop -> "desktop"
    | Laptop -> "laptop"
    | Phone -> "phone"
    | Tablet -> "tablet"
    | Monitor -> "monitor"
    | Network -> "network"
    | Peripheral -> "peripheral"
    | Other -> "other"

let conditionToWire =
    function
    | New -> "new"
    | Good -> "good"
    | Fair -> "fair"
    | Poor -> "poor"
    | Unserviceable -> "unserviceable"

let roleOfWire =
    function
    | "unitMember" -> Some UnitMember
    | "unitCustodian" -> Some UnitCustodian
    | "squadronAdmin" -> Some SquadronAdmin
    | "systemAdmin" -> Some SystemAdmin
    | _ -> None

// ── Decoders ────────────────────────────────────────────────────────────

let private roleDecoder: Decoder<AppRole> =
    Decode.string
    |> Decode.andThen (fun s ->
        match roleOfWire s with
        | Some r -> Decode.succeed r
        | None -> Decode.fail $"unknown role '{s}'")

let sessionDecoder: Decoder<Dtos.SessionDto> =
    Decode.object (fun get ->
        { UserId = get.Required.Field "userId" Decode.guid
          DisplayName = get.Required.Field "displayName" Decode.string
          Role = get.Required.Field "role" roleDecoder
          UnitId = get.Optional.Field "unitId" Decode.guid
          SquadronId = get.Optional.Field "squadronId" Decode.guid })

let dupDecoder: Decoder<Dtos.DuplicateCheckDto> =
    Decode.object (fun get ->
        { AssetTagTaken = get.Required.Field "assetTagTaken" Decode.bool
          SerialTaken = get.Required.Field "serialTaken" Decode.bool })

let private fieldErrorDecoder: Decoder<Validation.FieldError> =
    Decode.object (fun get ->
        { Field = get.Required.Field "field" Decode.string
          Message = get.Required.Field "message" Decode.string })

let problemDecoder: Decoder<Dtos.ProblemDetailsDto> =
    Decode.object (fun get ->
        { Type = get.Optional.Field "type" Decode.string |> Option.defaultValue "about:blank"
          Title = get.Optional.Field "title" Decode.string |> Option.defaultValue "Error"
          Status = get.Optional.Field "status" Decode.int |> Option.defaultValue 0
          Detail = get.Optional.Field "detail" Decode.string
          Errors =
            get.Optional.Field "errors" (Decode.list fieldErrorDecoder)
            |> Option.defaultValue []
          CorrelationId = get.Optional.Field "correlationId" Decode.string |> Option.defaultValue "" })

// ── Encoder ─────────────────────────────────────────────────────────────

let private dateToWire (d: DateOnly) =
    sprintf "%04i-%02i-%02i" d.Year d.Month d.Day

let private optOr (enc: 'a -> JsonValue) (v: 'a option) =
    v |> Option.map enc |> Option.defaultValue Encode.nil

let encodeAssetWrite (w: Dtos.AssetWriteDto) : string =
    Encode.object
        [ "unitId", Encode.guid w.UnitId
          "assetTag", Encode.string w.AssetTag
          "serialNumber", Encode.string w.SerialNumber
          "deviceType", Encode.string (deviceTypeToWire w.DeviceType)
          "make", Encode.string w.Make
          "model", Encode.string w.Model
          "osName", optOr Encode.string w.OsName
          "osVersion", optOr Encode.string w.OsVersion
          "macAddresses", w.MacAddresses |> List.map Encode.string |> Encode.list
          "condition", optOr (conditionToWire >> Encode.string) w.Condition
          "assignedTo", optOr Encode.string w.AssignedTo
          "location", optOr Encode.string w.Location
          "acquisitionDate", optOr (dateToWire >> Encode.string) w.AcquisitionDate
          "acquisitionCost", optOr Encode.decimal w.AcquisitionCost
          "warrantyExpiry", optOr (dateToWire >> Encode.string) w.WarrantyExpiry
          "notes", optOr Encode.string w.Notes
          "updatedAt", Encode.nil ]
    |> Encode.toString 0

// ── HTTP calls ──────────────────────────────────────────────────────────

type ApiError =
    | Problem of Dtos.ProblemDetailsDto
    | Network of string

let private decodeOrNetwork (decoder: Decoder<'a>) (body: string) : Result<'a, ApiError> =
    Decode.fromString decoder body |> Result.mapError Network

let private problemOf (status: int) (body: string) : ApiError =
    match Decode.fromString problemDecoder body with
    | Ok p -> Problem p
    | Error _ ->
        Problem
            { Type = "about:blank"
              Title = $"HTTP {status}"
              Status = status
              Detail = None
              Errors = []
              CorrelationId = "" }

let private call
    (method: HttpMethod)
    (url: string)
    (body: string option)
    (decoder: Decoder<'a>)
    : Async<Result<'a, ApiError>> =
    async {
        try
            let props =
                [ Method method
                  requestHeaders
                      [ HttpRequestHeaders.ContentType "application/json"
                        // CSRF guard (doc 10 §1)
                        HttpRequestHeaders.XRequestedWith "XMLHttpRequest" ]
                  yield! (body |> Option.map (Body << U3.Case3) |> Option.toList) ]

            // GlobalFetch.fetch: the raw binding. (Fable.Fetch's `fetch` helper REJECTS
            // on non-2xx, which would route 401/400/409 into the Network branch and
            // lose the problem+json body.)
            let! response =
                GlobalFetch.fetch (RequestInfo.Url url, requestProps props)
                |> Async.AwaitPromise

            let! text = response.text () |> Async.AwaitPromise

            if response.Ok then
                return decodeOrNetwork decoder text
            else
                return Error(problemOf response.Status text)
        with ex ->
            return Error(Network ex.Message)
    }

let getSession () =
    call HttpMethod.GET "/api/v1/session" None sessionDecoder

let checkDuplicate (assetTag: string) (make: string) (serial: string) =
    let enc (s: string) = JS.encodeURIComponent s

    call
        HttpMethod.GET
        $"/api/v1/assets/check-duplicate?assetTag={enc assetTag}&make={enc make}&serialNumber={enc serial}"
        None
        dupDecoder

let createAsset (w: Dtos.AssetWriteDto) =
    // response is the created AssetDto; the form only needs the id
    call HttpMethod.POST "/api/v1/assets" (Some(encodeAssetWrite w)) (Decode.field "id" Decode.guid)
