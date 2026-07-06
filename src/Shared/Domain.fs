namespace AssetTracker.Shared

open System

/// Single-case union ids — illegal states unrepresentable (docs/15 §2)
type SquadronId = SquadronId of Guid
type UnitId = UnitId of Guid
type AssetId = AssetId of Guid
type UserId = UserId of Guid
type DocumentId = DocumentId of Guid

type AppRole =
    | UnitMember
    | UnitCustodian
    | SquadronAdmin
    | SystemAdmin

type DeviceType =
    | Desktop
    | Laptop
    | Phone
    | Tablet
    | Monitor
    | Network
    | Peripheral
    | Other

type AssetCondition =
    | New
    | Good
    | Fair
    | Poor
    | Unserviceable

type AssetStatus =
    | InUse
    | InStorage
    | InRepair
    | PendingTransfer
    | Transferred
    | Disposed

type DocStatus =
    | Uploaded
    | Scanning
    | Quarantined
    | Parsing
    | Extracting
    | Validating
    | NeedsReview
    | Completed
    | Failed

/// Data scope derived from role — required by every repository query (docs/09 §2)
type DataScope =
    | UnitScope of UnitId
    | SquadronScope of SquadronId
    | AllScope

type Asset =
    { Id: AssetId
      UnitId: UnitId
      AssetTag: string
      SerialNumber: string
      DeviceType: DeviceType
      Make: string
      Model: string
      OsName: string option
      OsVersion: string option
      MacAddresses: string list
      Condition: AssetCondition option
      Status: AssetStatus
      AssignedTo: string option
      Location: string option
      AcquisitionDate: DateOnly option
      AcquisitionCost: decimal option
      WarrantyExpiry: DateOnly option
      Notes: string option }

module AssetStatus =
    /// Legal status transitions; UI and server share this single definition
    let canTransition (from: AssetStatus) (to': AssetStatus) =
        match from, to' with
        | Disposed, _ -> false
        | a, b when a = b -> false
        | _, PendingTransfer -> true
        | PendingTransfer, (InUse | InStorage | Transferred) -> true
        | (InUse | InStorage | InRepair), (InUse | InStorage | InRepair | Disposed) -> true
        | Transferred, InUse -> true
        | _ -> false
