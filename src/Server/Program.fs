module AssetTracker.Server.Program

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Serilog
open Serilog.Formatting.Compact

// M0 scaffold: health endpoints + structured logging + correlation IDs.
// Auth (docs/09), RBAC, and domain endpoints land in subsequent feature branches.

let private correlationHeader = "X-Correlation-Id"

/// Ensure every request carries a correlation id and it is logged + echoed (NFR-6)
let correlationMiddleware: HttpHandler =
    fun next ctx ->
        let cid =
            match ctx.TryGetRequestHeader correlationHeader with
            | Some v when not (String.IsNullOrWhiteSpace v) -> v
            | _ -> Guid.NewGuid().ToString "N"

        ctx.Items["CorrelationId"] <- cid
        ctx.SetHttpHeader(correlationHeader, cid)
        Serilog.Context.LogContext.PushProperty("CorrelationId", cid) |> ignore
        next ctx

let webApp: HttpHandler =
    choose
        [ GET >=> route "/health/live" >=> Successful.OK {| status = "live" |}
          // ponytail: readiness returns static OK until DB wiring lands (feature/database-schema)
          GET >=> route "/health/ready" >=> Successful.OK {| status = "ready" |}
          RequestErrors.NOT_FOUND "not found" ]

[<EntryPoint>]
let main args =
    Log.Logger <-
        LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(CompactJsonFormatter())
            .CreateLogger()

    try
        let builder = WebApplication.CreateBuilder args
        builder.Host.UseSerilog() |> ignore
        builder.Services.AddGiraffe() |> ignore

        let app = builder.Build()
        app.UseSerilogRequestLogging() |> ignore
        app.UseGiraffe(correlationMiddleware >=> webApp)
        app.Run()
        0
    with ex ->
        Log.Fatal(ex, "Host terminated unexpectedly")
        1
