module AssetTracker.Server.Tests.IntegrationTests

open System
open System.IO
open Expecto
open Npgsql
open AssetTracker.Shared
open AssetTracker.Server

// Real-Postgres tests. Gated on TEST_DATABASE_URL (CI provides a service
// container per docs/13; the Cowork sandbox has no root, so no local PG).
// The database is DROPPED AND RECREATED per run via the migration runner's
// SQL files — point this at a throwaway database only.

let private connString = Environment.GetEnvironmentVariable "TEST_DATABASE_URL"

let private applyMigrations (ds: NpgsqlDataSource) =
    task {
        let dir = Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "db", "migrations")

        for file in Directory.GetFiles(dir, "V*.sql") |> Array.sort do
            use conn = ds.CreateConnection()
            do! conn.OpenAsync()
            use cmd = new NpgsqlCommand(File.ReadAllText file, conn)
            let! _ = cmd.ExecuteNonQueryAsync()
            ()
    }

let private seed (ds: NpgsqlDataSource) =
    task {
        use conn = ds.CreateConnection()
        do! conn.OpenAsync()

        use cmd =
            new NpgsqlCommand(
                "INSERT INTO squadron (id, name, code) VALUES (@sq, 'TestSq', 'TSQ'); \
                 INSERT INTO unit (id, squadron_id, name, code) \
                   VALUES (@u1, @sq, 'Unit1', 'U1'), (@u2, @sq, 'Unit2', 'U2'); \
                 INSERT INTO app_user (id, edipi, cert_subject_dn, display_name, role, unit_id) \
                   VALUES (@usr, '1234567890', 'CN=TEST.CUSTODIAN.1234567890', 'Custodian One', 'unit_custodian', @u1)",
                conn
            )

        let sq, u1, u2, usr = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
        cmd.Parameters.AddWithValue("sq", sq) |> ignore
        cmd.Parameters.AddWithValue("u1", u1) |> ignore
        cmd.Parameters.AddWithValue("u2", u2) |> ignore
        cmd.Parameters.AddWithValue("usr", usr) |> ignore
        let! _ = cmd.ExecuteNonQueryAsync()
        return sq, u1, u2, usr
    }

let private writeDto unitId : Dtos.AssetWriteDto =
    { UnitId = unitId
      AssetTag = $"AF-{Guid.NewGuid():N}"
      SerialNumber = $"SN-{Guid.NewGuid():N}"
      DeviceType = Laptop
      Make = "Dell"
      Model = "Latitude 5400"
      OsName = Some "Windows 11"
      OsVersion = None
      MacAddresses = [ "aa:bb:cc:dd:ee:01" ]
      Condition = Some Good
      AssignedTo = Some "SSgt Example"
      Location = Some "Bldg 100"
      AcquisitionDate = None
      AcquisitionCost = None
      WarrantyExpiry = None
      Notes = None
      UpdatedAt = None }

[<Tests>]
let integrationTests =
    match connString with
    | null
    | "" -> testList "AssetsDb (integration)" [] |> testLabel "skipped-no-TEST_DATABASE_URL"
    | cs ->
        let ds = NpgsqlDataSource.Create cs

        let setup =
            lazy
                (let t =
                    task {
                        do! applyMigrations ds
                        return! seed ds
                    }

                 t.Result)

        let actor unitId : Auth.Principal =
            let _, _, _, usr = setup.Force()

            { UserId = UserId usr
              DisplayName = "Custodian One"
              Role = UnitCustodian
              UnitId = Some(UnitId unitId)
              SquadronId = None }

        testList
            "AssetsDb (integration)"
            [ test "create → get roundtrip with history row" {
                  let _, u1, _, _ = setup.Force()
                  let scope = UnitScope(UnitId u1)
                  let dto = writeDto u1
                  let asset = Dtos.AssetWrite.toAsset (AssetId Guid.Empty) InUse dto

                  let id =
                      (AssetsDb.create ds (actor u1) asset).Result
                      |> function
                          | Ok id -> id
                          | Error e -> failtest $"create failed: %A{e}"

                  let fetched = (AssetsDb.tryGetById ds scope id).Result
                  Expect.isSome fetched ""
                  Expect.equal fetched.Value.AssetTag asset.AssetTag ""
                  Expect.equal fetched.Value.MacAddresses [ "AA:BB:CC:DD:EE:01" ] "normalized on read"
              }
              test "duplicate tag reports conflict" {
                  let _, u1, _, _ = setup.Force()
                  let dto = writeDto u1
                  let asset = Dtos.AssetWrite.toAsset (AssetId Guid.Empty) InUse dto
                  let first = (AssetsDb.create ds (actor u1) asset).Result
                  Expect.isOk first ""

                  let second =
                      (AssetsDb.create
                          ds
                          (actor u1)
                          { asset with
                              SerialNumber = "different" })
                          .Result

                  Expect.equal second (Error AssetsDb.DuplicateTag) ""
              }
              test "out-of-scope read returns none" {
                  let _, u1, u2, _ = setup.Force()
                  let dto = writeDto u1
                  let asset = Dtos.AssetWrite.toAsset (AssetId Guid.Empty) InUse dto

                  let id =
                      (AssetsDb.create ds (actor u1) asset).Result
                      |> function
                          | Ok id -> id
                          | Error e -> failtest $"create failed: %A{e}"

                  Expect.isNone
                      ((AssetsDb.tryGetById ds (UnitScope(UnitId u2)) id).Result)
                      "unit2 cannot see unit1 asset"
              }
              test "update writes field-level history and bumps updated_at" {
                  let _, u1, _, _ = setup.Force()
                  let scope = UnitScope(UnitId u1)
                  let dto = writeDto u1
                  let asset = Dtos.AssetWrite.toAsset (AssetId Guid.Empty) InUse dto

                  let id =
                      (AssetsDb.create ds (actor u1) asset).Result
                      |> function
                          | Ok id -> id
                          | Error e -> failtest $"create failed: %A{e}"

                  let before = (AssetsDb.tryGetById ds scope id).Result.Value

                  let after =
                      { asset with
                          Location = Some "Bldg 200"
                          Notes = Some "moved" }

                  let result = (AssetsDb.update ds (actor u1) scope id before.UpdatedAt after).Result

                  match result with
                  | Error e -> failtest $"update failed: %A{e}"
                  | Ok updated ->
                      Expect.equal updated.Location (Some "Bldg 200") ""
                      Expect.isGreaterThan updated.UpdatedAt before.UpdatedAt ""

                  use conn = ds.CreateConnection()
                  conn.Open()

                  use cmd =
                      new NpgsqlCommand("SELECT field FROM asset_history WHERE asset_id=@id ORDER BY changed_at", conn)

                  cmd.Parameters.AddWithValue("id", id) |> ignore
                  use r = cmd.ExecuteReader()

                  let fields =
                      [ while r.Read() do
                            r.GetString 0 ]

                  Expect.containsAll fields [ "(created)"; "location"; "notes" ] ""
              }
              test "stale updated_at is a concurrency conflict" {
                  let _, u1, _, _ = setup.Force()
                  let scope = UnitScope(UnitId u1)
                  let dto = writeDto u1
                  let asset = Dtos.AssetWrite.toAsset (AssetId Guid.Empty) InUse dto

                  let id =
                      (AssetsDb.create ds (actor u1) asset).Result
                      |> function
                          | Ok id -> id
                          | Error e -> failtest $"create failed: %A{e}"

                  let stale = DateTime.UtcNow.AddDays -1.0
                  let result = (AssetsDb.update ds (actor u1) scope id stale asset).Result
                  Expect.equal result (Error AssetsDb.ConcurrencyConflict) ""
              }
              test "status transition records history; illegal transition rejected" {
                  let _, u1, _, _ = setup.Force()
                  let scope = UnitScope(UnitId u1)
                  let dto = writeDto u1
                  let asset = Dtos.AssetWrite.toAsset (AssetId Guid.Empty) InUse dto

                  let id =
                      (AssetsDb.create ds (actor u1) asset).Result
                      |> function
                          | Ok id -> id
                          | Error e -> failtest $"create failed: %A{e}"

                  let disposed =
                      (AssetsDb.changeStatus ds (actor u1) scope id Disposed (Some "survey")).Result

                  match disposed with
                  | Error e -> failtest $"dispose failed: %A{e}"
                  | Ok a -> Expect.equal a.Status Disposed ""

                  // disposed is terminal (soft delete) — nothing may leave it
                  let revive = (AssetsDb.changeStatus ds (actor u1) scope id InUse None).Result
                  Expect.equal revive (Error(AssetsDb.IllegalTransition(Disposed, InUse))) ""
              }
              test "duplicate probe sees tag and (make, serial)" {
                  let _, u1, _, _ = setup.Force()
                  let dto = writeDto u1
                  let asset = Dtos.AssetWrite.toAsset (AssetId Guid.Empty) InUse dto
                  (AssetsDb.create ds (actor u1) asset).Result |> ignore

                  let probe =
                      (AssetsDb.checkDuplicate ds (Some asset.AssetTag) (Some asset.Make) (Some asset.SerialNumber) None)
                          .Result

                  Expect.isTrue probe.AssetTagTaken ""
                  Expect.isTrue probe.SerialTaken ""

                  let clean =
                      (AssetsDb.checkDuplicate ds (Some "AF-FREE") (Some "Dell") (Some "SN-FREE") None)
                          .Result

                  Expect.isFalse clean.AssetTagTaken ""
                  Expect.isFalse clean.SerialTaken ""
              } ]
