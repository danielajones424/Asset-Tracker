module AssetTracker.Server.Tests.UnitTests

open System
open Expecto
open AssetTracker.Shared
open AssetTracker.Server

let private unitId = UnitId(Guid.NewGuid())
let private sqnId = SquadronId(Guid.NewGuid())

let private principal role unit sqn : Auth.Principal =
    { UserId = UserId(Guid.NewGuid())
      DisplayName = "Test"
      Role = role
      UnitId = unit
      SquadronId = sqn }

[<Tests>]
let scopeTests =
    testList
        "Auth.Principal.scope"
        [ test "system admin gets all-scope" {
              Expect.equal (Auth.Principal.scope (principal SystemAdmin None None)) (Ok AllScope) ""
          }
          test "squadron admin gets squadron scope" {
              Expect.equal
                  (Auth.Principal.scope (principal SquadronAdmin None (Some sqnId)))
                  (Ok(SquadronScope sqnId))
                  ""
          }
          test "custodian gets unit scope" {
              Expect.equal (Auth.Principal.scope (principal UnitCustodian (Some unitId) None)) (Ok(UnitScope unitId)) ""
          }
          test "member gets unit scope" {
              Expect.equal (Auth.Principal.scope (principal UnitMember (Some unitId) None)) (Ok(UnitScope unitId)) ""
          }
          test "squadron admin without squadron is rejected" {
              Expect.isError (Auth.Principal.scope (principal SquadronAdmin None None)) "no scope assignment"
          }
          test "custodian without unit is rejected" {
              Expect.isError (Auth.Principal.scope (principal UnitCustodian None (Some sqnId))) "no scope assignment"
          } ]

[<Tests>]
let writeRoleTests =
    testList
        "Auth.Principal.canWriteAssets"
        [ test "unit member is read-only" {
              Expect.isFalse (Auth.Principal.canWriteAssets (principal UnitMember (Some unitId) None)) ""
          }
          test "custodian and above can write" {
              for r in [ UnitCustodian; SquadronAdmin; SystemAdmin ] do
                  Expect.isTrue (Auth.Principal.canWriteAssets (principal r (Some unitId) (Some sqnId))) $"%A{r}"
          } ]

[<Tests>]
let sortWhitelistTests =
    testList
        "AssetsDb.parseSort"
        [ test "default sort when absent" { Expect.equal (AssetsDb.parseSort None) "updated_at DESC" "" }
          test "known column ascending" { Expect.equal (AssetsDb.parseSort (Some "assetTag")) "asset_tag ASC" "" }
          test "known column descending" { Expect.equal (AssetsDb.parseSort (Some "make:desc")) "make DESC" "" }
          test "unknown column falls back (no injection)" {
              Expect.equal (AssetsDb.parseSort (Some "1;DROP TABLE asset--")) "updated_at DESC" ""
          }
          test "injection via direction falls back" {
              Expect.equal (AssetsDb.parseSort (Some "assetTag:asc;DROP")) "updated_at DESC" ""
          } ]

[<Tests>]
let assetWriteTests =
    let valid: Dtos.AssetWriteDto =
        { UnitId = Guid.NewGuid()
          AssetTag = "AF-001"
          SerialNumber = "SN-1"
          DeviceType = Laptop
          Make = "Dell"
          Model = "5400"
          OsName = None
          OsVersion = None
          MacAddresses = [ "aa-bb-cc-dd-ee-0f" ]
          Condition = None
          AssignedTo = None
          Location = None
          AcquisitionDate = None
          AcquisitionCost = None
          WarrantyExpiry = None
          Notes = None
          UpdatedAt = None }

    testList
        "Dtos.AssetWrite"
        [ test "valid payload has no errors" { Expect.isEmpty (Dtos.AssetWrite.validate valid) "" }
          test "missing tag is reported" {
              let errors = Dtos.AssetWrite.validate { valid with AssetTag = " " }
              Expect.exists errors (fun e -> e.Field = "assetTag") ""
          }
          test "bad mac is reported" {
              let errors = Dtos.AssetWrite.validate { valid with MacAddresses = [ "nope" ] }
              Expect.exists errors (fun e -> e.Field = "macAddresses") ""
          }
          test "macs are normalized to canonical form" {
              let asset = Dtos.AssetWrite.toAsset (AssetId Guid.Empty) InUse valid
              Expect.equal asset.MacAddresses [ "AA:BB:CC:DD:EE:0F" ] ""
          }
          test "fields are trimmed" {
              let asset =
                  Dtos.AssetWrite.toAsset (AssetId Guid.Empty) InUse { valid with Make = " Dell " }

              Expect.equal asset.Make "Dell" ""
          } ]
