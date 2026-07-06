module AssetTracker.Migrate.Program

open System
open System.IO
open DbUp

// Migration runner: applies db/migrations/V*.sql in order, journaled in schemaversions.
// Runs as the app_migrate role via ECS task pre-deploy (docs/12 §4); locally against dev Postgres.
// Usage: dotnet run --project db/Migrate  (connection string from DB_CONNECTION_STRING)

[<EntryPoint>]
let main _ =
    let connectionString =
        Environment.GetEnvironmentVariable "DB_CONNECTION_STRING"
        |> Option.ofObj
        |> Option.defaultValue "Host=localhost;Database=assettracker;Username=postgres;Password=postgres"

    let migrationsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "migrations")
        |> Path.GetFullPath

    if not (Directory.Exists migrationsDir) then
        eprintfn $"Migrations directory not found: {migrationsDir}"
        exit 2

    let upgrader =
        DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsFromFileSystem(migrationsDir)
            .WithTransactionPerScript()
            .LogToConsole()
            .Build()

    let result = upgrader.PerformUpgrade()

    if result.Successful then
        printfn "Migrations applied successfully."
        0
    else
        eprintfn $"Migration failed: {result.Error.Message}"
        1
