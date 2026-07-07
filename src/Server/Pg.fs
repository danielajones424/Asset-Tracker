module AssetTracker.Server.Pg

open System
open Npgsql
open AssetTracker.Shared

// DU ↔ Postgres enum text. Casting `::enum` in SQL keeps Npgsql free of global
// enum-mapping state and makes every conversion greppable.

module DeviceType =
    let toDb =
        function
        | Desktop -> "desktop"
        | Laptop -> "laptop"
        | Phone -> "phone"
        | Tablet -> "tablet"
        | Monitor -> "monitor"
        | Network -> "network"
        | Peripheral -> "peripheral"
        | Other -> "other"

    let ofDb =
        function
        | "desktop" -> Desktop
        | "laptop" -> Laptop
        | "phone" -> Phone
        | "tablet" -> Tablet
        | "monitor" -> Monitor
        | "network" -> Network
        | "peripheral" -> Peripheral
        | "other" -> Other
        | s -> failwith $"unknown device_type '{s}'"

module AssetCondition =
    let toDb =
        function
        | New -> "new"
        | Good -> "good"
        | Fair -> "fair"
        | Poor -> "poor"
        | Unserviceable -> "unserviceable"

    let ofDb =
        function
        | "new" -> New
        | "good" -> Good
        | "fair" -> Fair
        | "poor" -> Poor
        | "unserviceable" -> Unserviceable
        | s -> failwith $"unknown asset_condition '{s}'"

module AssetStatus =
    let toDb =
        function
        | InUse -> "in_use"
        | InStorage -> "in_storage"
        | InRepair -> "in_repair"
        | PendingTransfer -> "pending_transfer"
        | Transferred -> "transferred"
        | Disposed -> "disposed"

    let ofDb =
        function
        | "in_use" -> InUse
        | "in_storage" -> InStorage
        | "in_repair" -> InRepair
        | "pending_transfer" -> PendingTransfer
        | "transferred" -> Transferred
        | "disposed" -> Disposed
        | s -> failwith $"unknown asset_status '{s}'"

// ── Parameter / reader helpers ──────────────────────────────────────────

/// F# overload resolution picks the base DbCommand.ExecuteReaderAsync();
/// downcast back to the provider reader (always an NpgsqlDataReader here).
let execReader (cmd: NpgsqlCommand) : Threading.Tasks.Task<NpgsqlDataReader> =
    task {
        let! (r: Data.Common.DbDataReader) = cmd.ExecuteReaderAsync()
        return r :?> NpgsqlDataReader
    }

let optStr (v: string option) : obj =
    match v with
    | Some s -> box s
    | None -> box DBNull.Value

let optDate (v: DateOnly option) : obj =
    match v with
    | Some d -> box d
    | None -> box DBNull.Value

let optDec (v: decimal option) : obj =
    match v with
    | Some d -> box d
    | None -> box DBNull.Value

let readOptStr (r: NpgsqlDataReader) (i: int) =
    if r.IsDBNull i then None else Some(r.GetString i)

let readOptDate (r: NpgsqlDataReader) (i: int) =
    if r.IsDBNull i then
        None
    else
        Some(r.GetFieldValue<DateOnly> i)

let readOptDec (r: NpgsqlDataReader) (i: int) =
    if r.IsDBNull i then None else Some(r.GetDecimal i)
