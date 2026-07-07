module AssetTracker.Server.AssetsHttp

open System
open Microsoft.AspNetCore.Http
open Giraffe
open Npgsql
open AssetTracker.Shared
open AssetTracker.Server

// Thin presentation layer (docs/06 §presentation): deserialize, authorize,
// invoke, serialize. Business rules live in Shared (validation, transitions)
// and AssetsDb (persistence invariants).

let private correlationId (ctx: HttpContext) =
    match ctx.Items.TryGetValue "CorrelationId" with
    | true, (:? string as c) -> c
    | _ -> ""

let private problem
    (ctx: HttpContext)
    (status: int)
    (title: string)
    (detail: string option)
    (errors: Validation.FieldError list)
    : Dtos.ProblemDetailsDto =
    { Type = "about:blank"
      Title = title
      Status = status
      Detail = detail
      Errors = errors
      CorrelationId = correlationId ctx }

let private problemJson
    (status: int)
    (title: string)
    (detail: string option)
    (errors: Validation.FieldError list)
    : HttpHandler =
    fun next ctx ->
        (setStatusCode status
         >=> setHttpHeader "Content-Type" "application/problem+json"
         >=> json (problem ctx status title detail errors))
            next
            ctx

let private validationProblem errors =
    problemJson 400 "Validation failed" None errors

let notFound: HttpHandler =
    // used for both absent and out-of-scope — no existence leak (doc 10 §3)
    problemJson 404 "Not found" None []

let private writeError: AssetsDb.WriteError -> HttpHandler =
    function
    | AssetsDb.DuplicateTag ->
        problemJson
            409
            "Conflict"
            (Some "asset tag already exists")
            [ { Field = "assetTag"
                Message = "already exists" } ]
    | AssetsDb.DuplicateSerial ->
        problemJson
            409
            "Conflict"
            (Some "serial number already exists for this make")
            [ { Field = "serialNumber"
                Message = "already exists for this make" } ]
    | AssetsDb.NotFound -> notFound
    | AssetsDb.ConcurrencyConflict ->
        problemJson 409 "Conflict" (Some "asset was modified by someone else; reload and retry") []
    | AssetsDb.IllegalTransition(from, to') ->
        validationProblem
            [ { Field = "status"
                Message = $"cannot change from %A{from} to %A{to'}" } ]

// ── Auth plumbing ───────────────────────────────────────────────────────

let requireAuth (handler: Auth.Principal -> HttpHandler) : HttpHandler =
    fun next ctx ->
        match Auth.tryGetPrincipal ctx with
        | Some p -> handler p next ctx
        | None -> problemJson 401 "Unauthorized" None [] next ctx

let private withScope (p: Auth.Principal) (handler: DataScope -> HttpHandler) : HttpHandler =
    match Auth.Principal.scope p with
    | Ok scope -> handler scope
    | Error msg -> problemJson 403 "Forbidden" (Some msg) []

let private requireAssetWriter (p: Auth.Principal) (handler: HttpHandler) : HttpHandler =
    if Auth.Principal.canWriteAssets p then
        handler
    else
        problemJson 403 "Forbidden" (Some "role cannot modify assets") []

/// CSRF (doc 10 §1): mutations must carry X-Requested-With: XMLHttpRequest
let requireCsrfHeader: HttpHandler =
    fun next ctx ->
        match ctx.TryGetRequestHeader "X-Requested-With" with
        | Some "XMLHttpRequest" -> next ctx
        | _ -> problemJson 403 "Forbidden" (Some "missing X-Requested-With header") [] next ctx

/// Custodians may only write within their own unit; admins within scope
let private canWriteToUnit (scope: DataScope) (ds: NpgsqlDataSource) (unitId: Guid) =
    task {
        match scope with
        | UnitScope(UnitId u) -> return u = unitId
        | AllScope -> return true
        | SquadronScope(SquadronId s) ->
            use conn = ds.CreateConnection()
            do! conn.OpenAsync()

            use cmd =
                new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM unit WHERE id=@u AND squadron_id=@s)", conn)

            cmd.Parameters.AddWithValue("u", unitId) |> ignore
            cmd.Parameters.AddWithValue("s", s) |> ignore
            let! r = cmd.ExecuteScalarAsync()
            return r :?> bool
    }

// ── Query-string parsing ────────────────────────────────────────────────

let private qs (ctx: HttpContext) (name: string) : string option =
    match ctx.TryGetQueryStringValue name with
    | Some v when not (String.IsNullOrWhiteSpace v) -> Some v
    | _ -> None

let private qsGuid ctx name =
    qs ctx name
    |> Option.bind (fun s ->
        match Guid.TryParse s with
        | true, g -> Some g
        | _ -> None)

let private qsInt ctx name def =
    qs ctx name
    |> Option.bind (fun s ->
        match Int32.TryParse s with
        | true, i -> Some i
        | _ -> None)
    |> Option.defaultValue def

let private parseDeviceType =
    function
    | "desktop" -> Some Desktop
    | "laptop" -> Some Laptop
    | "phone" -> Some Phone
    | "tablet" -> Some Tablet
    | "monitor" -> Some Monitor
    | "network" -> Some Network
    | "peripheral" -> Some Peripheral
    | "other" -> Some Other
    | _ -> None

let private parseStatus =
    function
    | "inUse" -> Some InUse
    | "inStorage" -> Some InStorage
    | "inRepair" -> Some InRepair
    | "pendingTransfer" -> Some PendingTransfer
    | "transferred" -> Some Transferred
    | "disposed" -> Some Disposed
    | _ -> None

// ── Handlers ────────────────────────────────────────────────────────────

let list (ds: NpgsqlDataSource) : HttpHandler =
    requireAuth (fun p ->
        withScope p (fun scope ->
            fun next ctx ->
                task {
                    let filters: AssetsDb.SearchFilters =
                        { Q = qs ctx "q"
                          UnitId = qsGuid ctx "unitId"
                          SquadronId = qsGuid ctx "squadronId"
                          DeviceType = qs ctx "deviceType" |> Option.bind parseDeviceType
                          Status = qs ctx "status" |> Option.bind parseStatus
                          Sort = qs ctx "sort"
                          Page = qsInt ctx "page" 1
                          PageSize = qsInt ctx "pageSize" 25 }

                    let! page = AssetsDb.search ds scope filters
                    return! json page next ctx
                }))

let getById (ds: NpgsqlDataSource) (id: Guid) : HttpHandler =
    requireAuth (fun p ->
        withScope p (fun scope ->
            fun next ctx ->
                task {
                    let! asset = AssetsDb.tryGetById ds scope id

                    match asset with
                    | Some a -> return! json a next ctx
                    | None -> return! notFound next ctx
                }))

let checkDuplicate (ds: NpgsqlDataSource) : HttpHandler =
    requireAuth (fun _ ->
        fun next ctx ->
            task {
                let! result =
                    AssetsDb.checkDuplicate
                        ds
                        (qs ctx "assetTag")
                        (qs ctx "make")
                        (qs ctx "serialNumber")
                        (qsGuid ctx "excludeId")

                return! json result next ctx
            })

let create (ds: NpgsqlDataSource) : HttpHandler =
    requireCsrfHeader
    >=> requireAuth (fun p ->
        requireAssetWriter
            p
            (withScope p (fun scope ->
                fun next ctx ->
                    task {
                        let! dto = ctx.BindJsonAsync<Dtos.AssetWriteDto>()

                        match Dtos.AssetWrite.validate dto with
                        | [] ->
                            let! allowed = canWriteToUnit scope ds dto.UnitId

                            if not allowed then
                                return! problemJson 403 "Forbidden" (Some "unit outside your scope") [] next ctx
                            else
                                let asset = Dtos.AssetWrite.toAsset (AssetId Guid.Empty) InUse dto
                                let! result = AssetsDb.create ds p asset

                                match result with
                                | Ok id ->
                                    let! created = AssetsDb.tryGetById ds scope id
                                    return! (setStatusCode 201 >=> json created.Value) next ctx
                                | Error e -> return! writeError e next ctx
                        | errors -> return! validationProblem errors next ctx
                    })))

let updateAsset (ds: NpgsqlDataSource) (id: Guid) : HttpHandler =
    requireCsrfHeader
    >=> requireAuth (fun p ->
        requireAssetWriter
            p
            (withScope p (fun scope ->
                fun next ctx ->
                    task {
                        let! dto = ctx.BindJsonAsync<Dtos.AssetWriteDto>()

                        match Dtos.AssetWrite.validate dto, dto.UpdatedAt with
                        | _ :: _ as errors, _ -> return! validationProblem errors next ctx
                        | [], None ->
                            return!
                                validationProblem
                                    [ { Field = "updatedAt"
                                        Message = "is required on update" } ]
                                    next
                                    ctx
                        | [], Some expected ->
                            let! allowed = canWriteToUnit scope ds dto.UnitId

                            if not allowed then
                                return! problemJson 403 "Forbidden" (Some "unit outside your scope") [] next ctx
                            else
                                // status is not writable here; keep current (diff ignores it)
                                let after = Dtos.AssetWrite.toAsset (AssetId id) InUse dto
                                let! result = AssetsDb.update ds p scope id expected after

                                match result with
                                | Ok updated -> return! json updated next ctx
                                | Error e -> return! writeError e next ctx
                    })))

let changeStatus (ds: NpgsqlDataSource) (id: Guid) : HttpHandler =
    requireCsrfHeader
    >=> requireAuth (fun p ->
        requireAssetWriter
            p
            (withScope p (fun scope ->
                fun next ctx ->
                    task {
                        let! dto = ctx.BindJsonAsync<Dtos.StatusChangeDto>()
                        let! result = AssetsDb.changeStatus ds p scope id dto.Status dto.Note

                        match result with
                        | Ok updated -> return! json updated next ctx
                        | Error e -> return! writeError e next ctx
                    })))

/// SPA bootstrap (doc 10): who am I, what role, what scope
let session: HttpHandler =
    requireAuth (fun p ->
        let (UserId uid) = p.UserId

        let dto: Dtos.SessionDto =
            { UserId = uid
              DisplayName = p.DisplayName
              Role = p.Role
              UnitId = p.UnitId |> Option.map (fun (UnitId u) -> u)
              SquadronId = p.SquadronId |> Option.map (fun (SquadronId s) -> s) }

        json dto)

let routes (ds: NpgsqlDataSource) : HttpHandler =
    subRoute
        "/api/v1"
        (choose
            [ GET >=> route "/session" >=> session
              GET >=> route "/assets" >=> list ds
              GET >=> route "/assets/check-duplicate" >=> checkDuplicate ds
              GET >=> routef "/assets/%O" (getById ds)
              POST >=> route "/assets" >=> create ds
              PUT >=> routef "/assets/%O" (updateAsset ds)
              POST >=> routef "/assets/%O/status" (changeStatus ds) ])
