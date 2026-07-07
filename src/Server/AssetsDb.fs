module AssetTracker.Server.AssetsDb

open System
open System.Threading.Tasks
open Npgsql
open AssetTracker.Shared
open AssetTracker.Shared.Dtos
open AssetTracker.Server

// Repository for the asset aggregate. Explicit SQL (docs/19 "no magic"),
// parametrized throughout, every query scope-constrained (docs/09 §2).
// History capture (US-C1 AC) happens in the same transaction as the write.

type SearchFilters =
    { Q: string option
      UnitId: Guid option
      SquadronId: Guid option
      DeviceType: DeviceType option
      Status: AssetStatus option
      Sort: string option
      Page: int
      PageSize: int }

type WriteError =
    | DuplicateTag
    | DuplicateSerial
    | NotFound
    | ConcurrencyConflict
    | IllegalTransition of from: AssetStatus * to': AssetStatus

/// Sort whitelist — never interpolate user input into ORDER BY
let private sortColumn =
    function
    | "assetTag" -> Some "asset_tag"
    | "serialNumber" -> Some "serial_number"
    | "make" -> Some "make"
    | "model" -> Some "model"
    | "status" -> Some "status"
    | "updatedAt" -> Some "updated_at"
    | _ -> None

/// public for unit tests — the whitelist is security-relevant (ORDER BY injection)
let parseSort (sort: string option) =
    match sort with
    | None -> "updated_at DESC"
    | Some s ->
        let col, dir =
            match s.Split ':' with
            | [| c; "desc" |] -> c, "DESC"
            | [| c; "asc" |]
            | [| c |] -> c, "ASC"
            | _ -> "", ""

        match sortColumn col with
        | Some c -> $"{c} {dir}"
        | None -> "updated_at DESC"

/// WHERE fragment + parameter for the caller's data scope. Every read and
/// write on assets goes through this — no scope, no query.
let private scopePredicate (scope: DataScope) : string * (string * obj) list =
    match scope with
    | UnitScope(UnitId u) -> "a.unit_id = @scope_unit", [ "scope_unit", box u ]
    | SquadronScope(SquadronId s) ->
        "a.unit_id IN (SELECT id FROM unit WHERE squadron_id = @scope_sqn)", [ "scope_sqn", box s ]
    | AllScope -> "TRUE", []

let private addParams (cmd: NpgsqlCommand) (ps: (string * obj) list) =
    for name, value in ps do
        cmd.Parameters.AddWithValue(name, value) |> ignore

let private readListItem (r: NpgsqlDataReader) : Dtos.AssetListItemDto =
    { Id = r.GetGuid 0
      AssetTag = r.GetString 1
      SerialNumber = r.GetString 2
      DeviceType = Pg.DeviceType.ofDb (r.GetString 3)
      Make = r.GetString 4
      Model = r.GetString 5
      AssignedTo = Pg.readOptStr r 6
      Status = Pg.AssetStatus.ofDb (r.GetString 7)
      UpdatedAt = r.GetDateTime 8 }

let private detailColumns =
    "a.id, a.unit_id, a.asset_tag, a.serial_number, a.device_type::text, a.make, a.model, \
     a.os_name, a.os_version, a.mac_addresses::text[], a.condition::text, a.status::text, \
     a.assigned_to, a.location, a.acquisition_date, a.acquisition_cost, a.warranty_expiry, \
     a.notes, a.created_at, a.updated_at"

let private readDetail (r: NpgsqlDataReader) : Dtos.AssetDto =
    { Id = r.GetGuid 0
      UnitId = r.GetGuid 1
      AssetTag = r.GetString 2
      SerialNumber = r.GetString 3
      DeviceType = Pg.DeviceType.ofDb (r.GetString 4)
      Make = r.GetString 5
      Model = r.GetString 6
      OsName = Pg.readOptStr r 7
      OsVersion = Pg.readOptStr r 8
      MacAddresses =
        if r.IsDBNull 9 then
            []
        else
            r.GetFieldValue<string[]> 9 |> Array.map Validation.normalizeMac |> List.ofArray
      Condition =
        (if r.IsDBNull 10 then
             None
         else
             Some(Pg.AssetCondition.ofDb (r.GetString 10)))
      Status = Pg.AssetStatus.ofDb (r.GetString 11)
      AssignedTo = Pg.readOptStr r 12
      Location = Pg.readOptStr r 13
      AcquisitionDate = Pg.readOptDate r 14
      AcquisitionCost = Pg.readOptDec r 15
      WarrantyExpiry = Pg.readOptDate r 16
      Notes = Pg.readOptStr r 17
      CreatedAt = r.GetDateTime 18
      UpdatedAt = r.GetDateTime 19 }

// ── Search ──────────────────────────────────────────────────────────────

let search (ds: NpgsqlDataSource) (scope: DataScope) (f: SearchFilters) : Task<Dtos.PageDto<Dtos.AssetListItemDto>> =
    task {
        let scopeSql, scopeParams = scopePredicate scope
        let conditions = ResizeArray [ scopeSql ]
        let ps = ResizeArray scopeParams

        f.Q
        |> Option.iter (fun q ->
            conditions.Add
                "(a.asset_tag ILIKE @q OR a.serial_number ILIKE @q OR a.model ILIKE @q OR a.make ILIKE @q OR a.assigned_to ILIKE @q)"

            ps.Add("q", box $"%%{q}%%"))

        f.UnitId
        |> Option.iter (fun u ->
            conditions.Add "a.unit_id = @unit_id"
            ps.Add("unit_id", box u))

        f.SquadronId
        |> Option.iter (fun s ->
            conditions.Add "a.unit_id IN (SELECT id FROM unit WHERE squadron_id = @squadron_id)"
            ps.Add("squadron_id", box s))

        f.DeviceType
        |> Option.iter (fun d ->
            conditions.Add "a.device_type = @device_type::device_type"
            ps.Add("device_type", box (Pg.DeviceType.toDb d)))

        f.Status
        |> Option.iter (fun s ->
            conditions.Add "a.status = @status::asset_status"
            ps.Add("status", box (Pg.AssetStatus.toDb s)))

        let where = String.concat " AND " conditions
        let orderBy = parseSort f.Sort
        let pageSize = max 1 (min 100 f.PageSize)
        let page = max 1 f.Page

        use conn = ds.CreateConnection()
        do! conn.OpenAsync()

        use countCmd =
            new NpgsqlCommand($"SELECT count(*) FROM asset a WHERE {where}", conn)

        addParams countCmd (List.ofSeq ps)
        let! total = countCmd.ExecuteScalarAsync()

        let sql =
            $"SELECT a.id, a.asset_tag, a.serial_number, a.device_type::text, a.make, a.model, \
               a.assigned_to, a.status::text, a.updated_at \
               FROM asset a WHERE {where} ORDER BY {orderBy} LIMIT @limit OFFSET @offset"

        use cmd = new NpgsqlCommand(sql, conn)
        addParams cmd (List.ofSeq ps)
        addParams cmd [ "limit", box pageSize; "offset", box ((page - 1) * pageSize) ]

        use! reader = Pg.execReader cmd
        let items = ResizeArray()

        let mutable more = true

        while more do
            let! hasRow = reader.ReadAsync()

            if hasRow then
                items.Add(readListItem reader)
            else
                more <- false

        return
            { Items = List.ofSeq items
              Page = page
              PageSize = pageSize
              TotalCount = Convert.ToInt32 total }
    }

// ── Get by id (scoped — out-of-scope reads as absent, no existence leak) ─

let tryGetById (ds: NpgsqlDataSource) (scope: DataScope) (id: Guid) : Task<Dtos.AssetDto option> =
    task {
        let scopeSql, scopeParams = scopePredicate scope
        use conn = ds.CreateConnection()
        do! conn.OpenAsync()

        use cmd =
            new NpgsqlCommand($"SELECT {detailColumns} FROM asset a WHERE a.id = @id AND {scopeSql}", conn)

        addParams cmd (("id", box id) :: scopeParams)
        use! reader = Pg.execReader cmd
        let! hasRow = reader.ReadAsync()
        return if hasRow then Some(readDetail reader) else None
    }

// ── Duplicate probe (backlog #13). Uniqueness is org-wide (DB constraints),
// so the probe is deliberately unscoped; it reveals only taken/not-taken. ──

let checkDuplicate
    (ds: NpgsqlDataSource)
    (assetTag: string option)
    (make: string option)
    (serial: string option)
    (excludeId: Guid option)
    : Task<Dtos.DuplicateCheckDto> =
    task {
        use conn = ds.CreateConnection()
        do! conn.OpenAsync()

        let exclude = excludeId |> Option.defaultValue Guid.Empty

        use cmd =
            new NpgsqlCommand(
                "SELECT \
                   EXISTS(SELECT 1 FROM asset WHERE asset_tag = @tag AND id <> @ex), \
                   EXISTS(SELECT 1 FROM asset WHERE make = @make AND serial_number = @serial AND id <> @ex)",
                conn
            )

        addParams
            cmd
            [ "tag", box (assetTag |> Option.defaultValue "")
              "make", box (make |> Option.defaultValue "")
              "serial", box (serial |> Option.defaultValue "")
              "ex", box exclude ]

        use! reader = Pg.execReader cmd
        let! _ = reader.ReadAsync()

        return
            { AssetTagTaken = assetTag.IsSome && reader.GetBoolean 0
              SerialTaken = make.IsSome && serial.IsSome && reader.GetBoolean 1 }
    }

// ── History capture ─────────────────────────────────────────────────────

let private insertHistory
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    (assetId: Guid)
    (UserId changedBy)
    (field: string)
    (oldV: string option)
    (newV: string option)
    : Task =
    task {
        use cmd =
            new NpgsqlCommand(
                "INSERT INTO asset_history (asset_id, changed_by, field, old_value, new_value) \
                 VALUES (@asset_id, @changed_by, @field, @old, @new)",
                conn,
                tx
            )

        addParams
            cmd
            [ "asset_id", box assetId
              "changed_by", box changedBy
              "field", box field
              "old", Pg.optStr oldV
              "new", Pg.optStr newV ]

        let! _ = cmd.ExecuteNonQueryAsync()
        ()
    }


let private diff (before: Dtos.AssetDto) (after: Asset) : (string * string option * string option) list =
    let str (o: 'a option) (f: 'a -> string) = o |> Option.map f
    let macs (l: string list) = String.concat "," l

    [ "assetTag", Some before.AssetTag, Some after.AssetTag
      "serialNumber", Some before.SerialNumber, Some after.SerialNumber
      "deviceType", Some $"%A{before.DeviceType}", Some $"%A{after.DeviceType}"
      "make", Some before.Make, Some after.Make
      "model", Some before.Model, Some after.Model
      "osName", before.OsName, after.OsName
      "osVersion", before.OsVersion, after.OsVersion
      "macAddresses",
      (if before.MacAddresses.IsEmpty then
           None
       else
           Some(macs before.MacAddresses)),
      (if after.MacAddresses.IsEmpty then
           None
       else
           Some(macs after.MacAddresses))
      "condition", str before.Condition (sprintf "%A"), str after.Condition (sprintf "%A")
      "assignedTo", before.AssignedTo, after.AssignedTo
      "location", before.Location, after.Location
      "acquisitionDate", str before.AcquisitionDate string, str after.AcquisitionDate string
      "acquisitionCost", str before.AcquisitionCost string, str after.AcquisitionCost string
      "warrantyExpiry", str before.WarrantyExpiry string, str after.WarrantyExpiry string
      "notes", before.Notes, after.Notes ]
    |> List.filter (fun (_, o, n) -> o <> n)

// ── Create ──────────────────────────────────────────────────────────────

let private mapUniqueViolation (ex: PostgresException) =
    if ex.SqlState = PostgresErrorCodes.UniqueViolation then
        match ex.ConstraintName with
        | null -> Some DuplicateSerial
        | c when c.Contains "asset_tag" -> Some DuplicateTag
        | _ -> Some DuplicateSerial
    else
        None

let create (ds: NpgsqlDataSource) (actor: Auth.Principal) (a: Asset) : Task<Result<Guid, WriteError>> =
    task {
        use conn = ds.CreateConnection()
        do! conn.OpenAsync()
        use! tx = conn.BeginTransactionAsync()

        try
            use cmd =
                new NpgsqlCommand(
                    "INSERT INTO asset \
                       (unit_id, asset_tag, serial_number, device_type, make, model, os_name, os_version, \
                        mac_addresses, condition, status, assigned_to, location, acquisition_date, \
                        acquisition_cost, warranty_expiry, notes) \
                     VALUES \
                       (@unit_id, @asset_tag, @serial, @device_type::device_type, @make, @model, @os_name, \
                        @os_version, @macs::macaddr[], @condition::asset_condition, @status::asset_status, \
                        @assigned_to, @location, @acq_date, @acq_cost, @warranty, @notes) \
                     RETURNING id",
                    conn,
                    tx
                )

            let (UnitId unitId) = a.UnitId

            addParams
                cmd
                [ "unit_id", box unitId
                  "asset_tag", box a.AssetTag
                  "serial", box a.SerialNumber
                  "device_type", box (Pg.DeviceType.toDb a.DeviceType)
                  "make", box a.Make
                  "model", box a.Model
                  "os_name", Pg.optStr a.OsName
                  "os_version", Pg.optStr a.OsVersion
                  "macs", box (Array.ofList a.MacAddresses)
                  "condition",
                  (match a.Condition with
                   | Some c -> box (Pg.AssetCondition.toDb c)
                   | None -> box DBNull.Value)
                  "status", box (Pg.AssetStatus.toDb a.Status)
                  "assigned_to", Pg.optStr a.AssignedTo
                  "location", Pg.optStr a.Location
                  "acq_date", Pg.optDate a.AcquisitionDate
                  "acq_cost", Pg.optDec a.AcquisitionCost
                  "warranty", Pg.optDate a.WarrantyExpiry
                  "notes", Pg.optStr a.Notes ]

            let! idObj = cmd.ExecuteScalarAsync()
            let id = idObj :?> Guid

            do! insertHistory conn tx id actor.UserId "(created)" None (Some a.AssetTag)
            do! tx.CommitAsync()
            return Ok id
        with :? PostgresException as ex ->
            match mapUniqueViolation ex with
            | Some e -> return Error e
            | None -> return raise ex
    }

// ── Update (allowable fields; optimistic concurrency on updated_at) ─────

let update
    (ds: NpgsqlDataSource)
    (actor: Auth.Principal)
    (scope: DataScope)
    (id: Guid)
    (expectedUpdatedAt: DateTime)
    (after: Asset)
    : Task<Result<Dtos.AssetDto, WriteError>> =
    task {
        let scopeSql, scopeParams = scopePredicate scope
        use conn = ds.CreateConnection()
        do! conn.OpenAsync()
        use! tx = conn.BeginTransactionAsync()

        use readCmd =
            new NpgsqlCommand(
                $"SELECT {detailColumns} FROM asset a WHERE a.id = @id AND {scopeSql} FOR UPDATE",
                conn,
                tx
            )

        addParams readCmd (("id", box id) :: scopeParams)

        let! before =
            task {
                use! reader = Pg.execReader readCmd
                let! hasRow = reader.ReadAsync()
                return if hasRow then Some(readDetail reader) else None
            }

        match before with
        | None -> return Error NotFound
        | Some before when abs ((before.UpdatedAt - expectedUpdatedAt).TotalMilliseconds) > 1.0 ->
            return Error ConcurrencyConflict
        | Some before ->
            let changes = diff before after

            if changes.IsEmpty then
                do! tx.CommitAsync()
                return Ok before
            else
                try
                    use upd =
                        new NpgsqlCommand(
                            "UPDATE asset SET \
                               asset_tag=@asset_tag, serial_number=@serial, device_type=@device_type::device_type, \
                               make=@make, model=@model, os_name=@os_name, os_version=@os_version, \
                               mac_addresses=@macs::macaddr[], condition=@condition::asset_condition, \
                               assigned_to=@assigned_to, location=@location, acquisition_date=@acq_date, \
                               acquisition_cost=@acq_cost, warranty_expiry=@warranty, notes=@notes, \
                               updated_at=now() \
                             WHERE id=@id",
                            conn,
                            tx
                        )

                    addParams
                        upd
                        [ "id", box id
                          "asset_tag", box after.AssetTag
                          "serial", box after.SerialNumber
                          "device_type", box (Pg.DeviceType.toDb after.DeviceType)
                          "make", box after.Make
                          "model", box after.Model
                          "os_name", Pg.optStr after.OsName
                          "os_version", Pg.optStr after.OsVersion
                          "macs", box (Array.ofList after.MacAddresses)
                          "condition",
                          (match after.Condition with
                           | Some c -> box (Pg.AssetCondition.toDb c)
                           | None -> box DBNull.Value)
                          "assigned_to", Pg.optStr after.AssignedTo
                          "location", Pg.optStr after.Location
                          "acq_date", Pg.optDate after.AcquisitionDate
                          "acq_cost", Pg.optDec after.AcquisitionCost
                          "warranty", Pg.optDate after.WarrantyExpiry
                          "notes", Pg.optStr after.Notes ]

                    let! _ = upd.ExecuteNonQueryAsync()

                    for field, oldV, newV in changes do
                        do! insertHistory conn tx id actor.UserId field oldV newV

                    do! tx.CommitAsync()

                    // re-read outside tx for fresh updated_at
                    let! updated = tryGetById ds scope id
                    return Ok(Option.defaultValue before updated)
                with :? PostgresException as ex ->
                    match mapUniqueViolation ex with
                    | Some e -> return Error e
                    | None -> return raise ex
    }

// ── Status transition (US-C2; disposed = soft delete) ───────────────────

let changeStatus
    (ds: NpgsqlDataSource)
    (actor: Auth.Principal)
    (scope: DataScope)
    (id: Guid)
    (target: AssetStatus)
    (note: string option)
    : Task<Result<Dtos.AssetDto, WriteError>> =
    task {
        let scopeSql, scopeParams = scopePredicate scope
        use conn = ds.CreateConnection()
        do! conn.OpenAsync()
        use! tx = conn.BeginTransactionAsync()

        use readCmd =
            new NpgsqlCommand(
                $"SELECT a.status::text FROM asset a WHERE a.id = @id AND {scopeSql} FOR UPDATE",
                conn,
                tx
            )

        addParams readCmd (("id", box id) :: scopeParams)
        let! currentObj = readCmd.ExecuteScalarAsync()

        match currentObj with
        | null -> return Error NotFound
        | current ->
            let from = Pg.AssetStatus.ofDb (current :?> string)

            if not (AssetStatus.canTransition from target) then
                return Error(IllegalTransition(from, target))
            else
                use upd =
                    new NpgsqlCommand(
                        "UPDATE asset SET status=@status::asset_status, updated_at=now() WHERE id=@id",
                        conn,
                        tx
                    )

                addParams upd [ "id", box id; "status", box (Pg.AssetStatus.toDb target) ]
                let! _ = upd.ExecuteNonQueryAsync()

                do!
                    insertHistory
                        conn
                        tx
                        id
                        actor.UserId
                        "status"
                        (Some(Pg.AssetStatus.toDb from))
                        (Some(Pg.AssetStatus.toDb target))

                match note with
                | Some n when not (String.IsNullOrWhiteSpace n) ->
                    do! insertHistory conn tx id actor.UserId "status.note" None (Some n)
                | _ -> ()

                do! tx.CommitAsync()
                let! updated = tryGetById ds scope id

                match updated with
                | Some dto -> return Ok dto
                | None -> return Error NotFound
    }
