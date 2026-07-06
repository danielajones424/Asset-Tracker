namespace AssetTracker.Shared

/// Field validation shared verbatim by client (Fable) and server — written once (docs/19)
module Validation =

    type FieldError = { Field: string; Message: string }

    let private isMacChar c = System.Char.IsAsciiHexDigit c

    /// Accepts AA:BB:CC:DD:EE:FF or AA-BB-CC-DD-EE-FF (case-insensitive)
    let isValidMac (s: string) =
        let parts =
            if s.Contains ':' then s.Split ':'
            elif s.Contains '-' then s.Split '-'
            else [||]

        parts.Length = 6
        && parts |> Array.forall (fun p -> p.Length = 2 && p |> Seq.forall isMacChar)

    let private required name (value: string) =
        if System.String.IsNullOrWhiteSpace value then
            Some { Field = name; Message = "is required" }
        else
            None

    let private maxLen name (max: int) (value: string) =
        if value <> null && value.Length > max then
            Some { Field = name; Message = $"must be at most {max} characters" }
        else
            None

    /// Validation for create/edit of an asset's user-supplied fields
    let validateAsset (a: Asset) : FieldError list =
        [ required "assetTag" a.AssetTag
          maxLen "assetTag" 50 a.AssetTag
          required "serialNumber" a.SerialNumber
          maxLen "serialNumber" 100 a.SerialNumber
          required "make" a.Make
          required "model" a.Model
          yield!
              a.MacAddresses
              |> List.filter (isValidMac >> not)
              |> List.map (fun m ->
                  Some { Field = "macAddresses"; Message = $"'{m}' is not a valid MAC address" }) ]
        |> List.choose id
