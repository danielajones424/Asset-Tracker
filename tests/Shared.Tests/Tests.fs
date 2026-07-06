module AssetTracker.Shared.Tests

open Expecto
open AssetTracker.Shared

[<Tests>]
let statusTransitionTests =
    testList
        "AssetStatus.canTransition"
        [ test "disposed is terminal" {
              for target in [ InUse; InStorage; InRepair; PendingTransfer; Transferred ] do
                  Expect.isFalse (AssetStatus.canTransition Disposed target) $"disposed → %A{target}"
          }
          test "self-transition is never legal" {
              for s in [ InUse; InStorage; InRepair; Disposed ] do
                  Expect.isFalse (AssetStatus.canTransition s s) $"%A{s} → %A{s}"
          }
          test "in-use asset can be disposed" { Expect.isTrue (AssetStatus.canTransition InUse Disposed) "" }
          test "pending transfer resolves to transferred" {
              Expect.isTrue (AssetStatus.canTransition PendingTransfer Transferred) ""
          } ]

[<Tests>]
let macValidationTests =
    testList
        "Validation.isValidMac"
        [ test "accepts colon form" { Expect.isTrue (Validation.isValidMac "AA:BB:CC:DD:EE:0F") "" }
          test "accepts dash form" { Expect.isTrue (Validation.isValidMac "aa-bb-cc-dd-ee-0f") "" }
          test "rejects short" { Expect.isFalse (Validation.isValidMac "AA:BB:CC:DD:EE") "" }
          test "rejects non-hex" { Expect.isFalse (Validation.isValidMac "GG:BB:CC:DD:EE:0F") "" }
          test "rejects empty" { Expect.isFalse (Validation.isValidMac "") "" } ]

[<EntryPoint>]
let main args = runTestsInAssemblyWithCLIArgs [] args
