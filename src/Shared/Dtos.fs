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
