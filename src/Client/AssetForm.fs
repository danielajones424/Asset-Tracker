module AssetTracker.Client.AssetForm

open System
open Elmish
open Feliz
open Feliz.UseElmish
open AssetTracker.Shared
open AssetTracker.Client

// Fast single-asset entry (backlog #13, US-C1): keyboard-first, validated by
// the SAME shared code the server runs, duplicate probe as you type.
// After a successful save, make/model/type survive so batch entry of similar
// devices stays under the 60-second target.

type private Fields =
    { AssetTag: string
      SerialNumber: string
      DeviceType: DeviceType
      Make: string
      Model: string
      OsName: string
      OsVersion: string
      Macs: string
      Condition: AssetCondition option
      AssignedTo: string
      Location: string
      AcquisitionDate: string
      AcquisitionCost: string
      WarrantyExpiry: string
      Notes: string }

let private emptyFields =
    { AssetTag = ""
      SerialNumber = ""
      DeviceType = Laptop
      Make = ""
      Model = ""
      OsName = ""
      OsVersion = ""
      Macs = ""
      Condition = None
      AssignedTo = ""
      Location = ""
      AcquisitionDate = ""
      AcquisitionCost = ""
      WarrantyExpiry = ""
      Notes = "" }

type private Model =
    { UnitId: Guid
      Fields: Fields
      Errors: Validation.FieldError list
      Dup: Dtos.DuplicateCheckDto option
      DupGen: int
      Saving: bool
      Saved: int // count this session — cheap feedback for batch entry
      Failure: string option }

[<NoEquality; NoComparison>] // Set carries a function
type private Msg =
    | Set of (Fields -> Fields)
    | DebounceDup
    | DupResult of gen: int * Result<Dtos.DuplicateCheckDto, Api.ApiError>
    | Submit
    | Saved of Result<Guid, Api.ApiError>

let private opt (s: string) =
    if String.IsNullOrWhiteSpace s then None else Some(s.Trim())

let private parseDate (s: string) : DateOnly option =
    match s.Split '-' with
    | [| y; m; d |] ->
        match Int32.TryParse y, Int32.TryParse m, Int32.TryParse d with
        | (true, y), (true, m), (true, d) -> Some(DateOnly(y, m, d))
        | _ -> None
    | _ -> None

let private parseDecimal (s: string) : decimal option =
    match Decimal.TryParse s with
    | true, d -> Some d
    | _ -> None

let private splitMacs (s: string) =
    s.Split([| ','; ';'; ' ' |], StringSplitOptions.RemoveEmptyEntries)
    |> Array.map (fun m -> m.Trim())
    |> List.ofArray

let private toWriteDto (unitId: Guid) (f: Fields) : Dtos.AssetWriteDto =
    { UnitId = unitId
      AssetTag = f.AssetTag
      SerialNumber = f.SerialNumber
      DeviceType = f.DeviceType
      Make = f.Make
      Model = f.Model
      OsName = opt f.OsName
      OsVersion = opt f.OsVersion
      MacAddresses = splitMacs f.Macs
      Condition = f.Condition
      AssignedTo = opt f.AssignedTo
      Location = opt f.Location
      AcquisitionDate = parseDate f.AcquisitionDate
      AcquisitionCost = parseDecimal f.AcquisitionCost
      WarrantyExpiry = parseDate f.WarrantyExpiry
      Notes = opt f.Notes
      UpdatedAt = None }

let private init (unitId: Guid) () =
    { UnitId = unitId
      Fields = emptyFields
      Errors = []
      Dup = None
      DupGen = 0
      Saving = false
      Saved = 0
      Failure = None },
    Cmd.none

let private dupCmd (gen: int) (f: Fields) =
    Cmd.OfAsync.perform
        (fun () ->
            async {
                do! Async.Sleep 350
                return! Api.checkDuplicate (f.AssetTag.Trim()) (f.Make.Trim()) (f.SerialNumber.Trim())
            })
        ()
        (fun r -> DupResult(gen, r))

let private update msg (model: Model) =
    match msg with
    | Set f ->
        let fields = f model.Fields

        { model with
            Fields = fields
            Failure = None },
        Cmd.ofMsg DebounceDup
    | DebounceDup ->
        let gen = model.DupGen + 1
        let f = model.Fields

        if f.AssetTag.Trim() = "" && (f.SerialNumber.Trim() = "" || f.Make.Trim() = "") then
            { model with DupGen = gen; Dup = None }, Cmd.none
        else
            { model with DupGen = gen }, dupCmd gen f
    | DupResult(gen, result) when gen = model.DupGen ->
        (match result with
         | Ok d -> { model with Dup = Some d }
         | Error _ -> model), // probe is advisory; server still enforces
        Cmd.none
    | DupResult _ -> model, Cmd.none // stale keystroke
    | Submit ->
        let dto = toWriteDto model.UnitId model.Fields

        match Dtos.AssetWrite.validate dto with
        | [] ->
            { model with
                Saving = true
                Errors = []
                Failure = None },
            Cmd.OfAsync.perform (fun () -> Api.createAsset dto) () Saved
        | errors -> { model with Errors = errors }, Cmd.none
    | Saved(Ok _) ->
        // keep device identity fields for the next similar device (batch entry)
        let keep = model.Fields

        { model with
            Saving = false
            Saved = model.Saved + 1
            Dup = None
            Fields =
                { emptyFields with
                    DeviceType = keep.DeviceType
                    Make = keep.Make
                    Model = keep.Model
                    OsName = keep.OsName
                    Condition = keep.Condition
                    Location = keep.Location } },
        Cmd.none
    | Saved(Error(Api.Problem p)) ->
        { model with
            Saving = false
            Errors = p.Errors
            Failure =
                if p.Errors.IsEmpty then
                    Some(p.Detail |> Option.defaultValue p.Title)
                else
                    p.Detail },
        Cmd.none
    | Saved(Error(Api.Network msg)) ->
        { model with
            Saving = false
            Failure = Some $"Network error: {msg}" },
        Cmd.none

// ── View ────────────────────────────────────────────────────────────────

let private errorFor (errors: Validation.FieldError list) (field: string) =
    errors
    |> List.filter (fun e -> e.Field = field)
    |> List.map (fun e -> Html.span [ prop.className "field-error"; prop.text e.Message ])

let private labeled (label: string) (error: ReactElement list) (control: ReactElement) =
    Html.label
        [ prop.className "form-field"
          prop.children
              [ Html.span [ prop.className "form-label"; prop.text label ]
                control
                yield! error ] ]

let private textInput (value: string) (onChange: string -> unit) (autoFocus: bool) =
    Html.input
        [ prop.type' "text"
          prop.value value
          prop.autoFocus autoFocus
          prop.onChange onChange ]

let private deviceTypes =
    [ Desktop; Laptop; Phone; Tablet; Monitor; Network; Peripheral; Other ]

let private conditions = [ New; Good; Fair; Poor; Unserviceable ]

[<ReactComponent>]
let AssetForm (unitId: Guid) =
    let model, dispatch = React.useElmish (init unitId, update, [| box unitId |])
    let f = model.Fields
    let set g = dispatch (Set g)

    let dupWarning (taken: bool) (text: string) =
        if taken then
            [ Html.span [ prop.className "field-error"; prop.text text ] ]
        else
            []

    Html.form
        [ prop.className "asset-form"
          prop.onSubmit (fun e ->
              e.preventDefault ()
              dispatch Submit)
          prop.children
              [ Html.h2 "New asset"
                if model.Saved > 0 then
                    Html.p [ prop.className "save-count"; prop.text $"{model.Saved} saved this session" ]

                Html.div
                    [ prop.className "form-grid"
                      prop.children
                          [ labeled
                                "Asset tag *"
                                (errorFor model.Errors "assetTag"
                                 @ dupWarning (model.Dup |> Option.exists _.AssetTagTaken) "tag already in use")
                                (textInput f.AssetTag (fun v -> set (fun f -> { f with AssetTag = v })) true)
                            labeled
                                "Serial number *"
                                (errorFor model.Errors "serialNumber"
                                 @ dupWarning
                                     (model.Dup |> Option.exists _.SerialTaken)
                                     "serial already recorded for this make")
                                (textInput f.SerialNumber (fun v -> set (fun f -> { f with SerialNumber = v })) false)
                            labeled
                                "Device type *"
                                []
                                (Html.select
                                    [ prop.value (Api.deviceTypeToWire f.DeviceType)
                                      prop.onChange (fun (v: string) ->
                                          deviceTypes
                                          |> List.tryFind (fun d -> Api.deviceTypeToWire d = v)
                                          |> Option.iter (fun d -> set (fun f -> { f with DeviceType = d })))
                                      prop.children (
                                          deviceTypes
                                          |> List.map (fun d ->
                                              Html.option [ prop.value (Api.deviceTypeToWire d); prop.text $"%A{d}" ])
                                      ) ])
                            labeled
                                "Make *"
                                (errorFor model.Errors "make")
                                (textInput f.Make (fun v -> set (fun f -> { f with Make = v })) false)
                            labeled
                                "Model *"
                                (errorFor model.Errors "model")
                                (textInput f.Model (fun v -> set (fun f -> { f with Model = v })) false)
                            labeled
                                "MAC addresses"
                                (errorFor model.Errors "macAddresses")
                                (textInput f.Macs (fun v -> set (fun f -> { f with Macs = v })) false)
                            labeled
                                "Condition"
                                []
                                (Html.select
                                    [ prop.value (
                                          f.Condition |> Option.map Api.conditionToWire |> Option.defaultValue ""
                                      )
                                      prop.onChange (fun (v: string) ->
                                          set (fun f ->
                                              { f with
                                                  Condition =
                                                      conditions |> List.tryFind (fun c -> Api.conditionToWire c = v) }))
                                      prop.children
                                          [ Html.option [ prop.value ""; prop.text "—" ]
                                            yield!
                                                conditions
                                                |> List.map (fun c ->
                                                    Html.option
                                                        [ prop.value (Api.conditionToWire c); prop.text $"%A{c}" ]) ] ])
                            labeled
                                "Assigned to"
                                []
                                (textInput f.AssignedTo (fun v -> set (fun f -> { f with AssignedTo = v })) false)
                            labeled
                                "Location"
                                []
                                (textInput f.Location (fun v -> set (fun f -> { f with Location = v })) false)
                            labeled "OS" [] (textInput f.OsName (fun v -> set (fun f -> { f with OsName = v })) false)
                            labeled
                                "Acquisition date"
                                []
                                (Html.input
                                    [ prop.type' "date"
                                      prop.value f.AcquisitionDate
                                      prop.onChange (fun (v: string) -> set (fun f -> { f with AcquisitionDate = v })) ])
                            labeled
                                "Cost"
                                []
                                (Html.input
                                    [ prop.type' "number"
                                      prop.step 0.01
                                      prop.value f.AcquisitionCost
                                      prop.onChange (fun (v: string) -> set (fun f -> { f with AcquisitionCost = v })) ]) ] ]

                labeled
                    "Notes"
                    []
                    (Html.textarea
                        [ prop.value f.Notes
                          prop.rows 2
                          prop.onChange (fun (v: string) -> set (fun f -> { f with Notes = v })) ])

                match model.Failure with
                | Some msg -> Html.p [ prop.className "form-failure"; prop.text msg ]
                | None -> Html.none

                Html.button
                    [ prop.type' "submit"
                      prop.className "btn-primary"
                      prop.disabled model.Saving
                      prop.text (if model.Saving then "Saving…" else "Save asset (⏎)") ] ] ]
