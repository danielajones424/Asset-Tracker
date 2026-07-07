namespace AssetTracker.Shared

open System

/// Wire contracts — single source of truth for client and server (docs/10 §4)
module Dtos =

    type SessionDto =
        { UserId: Guid
          DisplayName: string
          Role: AppRole
          UnitId: Guid option
          SquadronId: Guid option }

    type PageDto<'T> =
        { Items: 'T list
          Page: int
          PageSize: int
          TotalCount: int }

    type ProblemDetailsDto =
        { Type: string
          Title: string
          Status: int
          Detail: string option
          Errors: Validation.FieldError list
          CorrelationId: string }

    type AssetListItemDto =
        { Id: Guid
          AssetTag: string
          SerialNumber: string
          DeviceType: DeviceType
          Make: string
          Model: string
          AssignedTo: string option
          Status: AssetStatus
          UpdatedAt: DateTime }

    /// Full asset detail; UpdatedAt doubles as the concurrency precondition on PUT (doc 10 §3)
    type AssetDto =
        { Id: Guid
          UnitId: Guid
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
          Notes: string option
          CreatedAt: DateTime
          UpdatedAt: DateTime }

    /// Create/update payload — user-supplied fields only (US-C1). Status changes go
    /// through POST /assets/{id}/status; unit moves through transfer requests.
    type AssetWriteDto =
        {
            UnitId: Guid
            AssetTag: string
            SerialNumber: string
            DeviceType: DeviceType
            Make: string
            Model: string
            OsName: string option
            OsVersion: string option
            MacAddresses: string list
            Condition: AssetCondition option
            AssignedTo: string option
            Location: string option
            AcquisitionDate: DateOnly option
            AcquisitionCost: decimal option
            WarrantyExpiry: DateOnly option
            Notes: string option
            /// Required on PUT (409 on mismatch); ignored on POST
            UpdatedAt: DateTime option
        }

    type StatusChangeDto =
        { Status: AssetStatus
          Note: string option }

    /// As-you-type duplicate probe for the entry form (backlog #13)
    type DuplicateCheckDto =
        {
            AssetTagTaken: bool
            /// serial uniqueness is scoped (make, serialNumber) — doc 08 amendment
            SerialTaken: bool
        }

    /// Shared by client form and server handler — validate once, same answer (ADR-5)
    module AssetWrite =

        /// Domain view of a write payload; id/status supplied by caller
        let toAsset (id: AssetId) (status: AssetStatus) (w: AssetWriteDto) : Asset =
            { Id = id
              UnitId = UnitId w.UnitId
              AssetTag = w.AssetTag.Trim()
              SerialNumber = w.SerialNumber.Trim()
              DeviceType = w.DeviceType
              Make = w.Make.Trim()
              Model = w.Model.Trim()
              OsName = w.OsName
              OsVersion = w.OsVersion
              MacAddresses =
                w.MacAddresses
                |> List.map (fun m ->
                    if Validation.isValidMac m then
                        Validation.normalizeMac m
                    else
                        m)
              Condition = w.Condition
              Status = status
              AssignedTo = w.AssignedTo
              Location = w.Location
              AcquisitionDate = w.AcquisitionDate
              AcquisitionCost = w.AcquisitionCost
              WarrantyExpiry = w.WarrantyExpiry
              Notes = w.Notes }

        let validate (w: AssetWriteDto) : Validation.FieldError list =
            toAsset (AssetId Guid.Empty) InUse w |> Validation.validateAsset
