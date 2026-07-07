module AssetTracker.Server.Program

open System
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Npgsql
open Serilog
open Serilog.Formatting.Compact

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

/// Wire contract: camelCase fields, fieldless DUs as camelCase strings,
/// options unwrapped to value-or-null (doc 10 §1)
let jsonOptions =
    let o = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

    JsonFSharpOptions
        .Default()
        .WithUnionUnwrapFieldlessTags()
        .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)
        .AddToJsonSerializerOptions
        o

    o

/// Full request pipeline as a value — testable without a live server
let buildApp (devAuthEnabled: bool) (dataSource: NpgsqlDataSource option) : HttpHandler =
    let apiRoutes =
        match dataSource with
        | Some ds -> AssetsHttp.routes ds
        | None -> subRoute "/api/v1" (setStatusCode 503 >=> json {| title = "database not configured" |})

    let core: HttpHandler =
        choose
            [ GET >=> route "/health/live" >=> Successful.OK {| status = "live" |}
              // ponytail: readiness returns static OK until DB wiring lands (feature/database-schema)
              GET >=> route "/health/ready" >=> Successful.OK {| status = "ready" |}
              apiRoutes
              RequestErrors.NOT_FOUND "not found" ]

    if devAuthEnabled then
        correlationMiddleware >=> Auth.DevAuth.middleware >=> core
    else
        correlationMiddleware >=> core

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

        // non-generic overload: F# nullness flags TService=interface on the generic one
        builder.Services.AddSingleton(typeof<Json.ISerializer>, SystemTextJson.Serializer jsonOptions)
        |> ignore

        let dataSource =
            match builder.Configuration.GetConnectionString "Default" with
            | null
            | "" ->
                Log.Warning "No ConnectionStrings__Default configured — API routes disabled (503)"
                None
            | cs -> Some(NpgsqlDataSource.Create cs)

        // DEV ONLY seam — cannot activate outside Development (see Auth.DevAuth)
        let devAuthEnabled =
            builder.Environment.IsDevelopment()
            && builder.Configuration["AssetTracker:DevAuth"] = "true"

        if devAuthEnabled then
            Log.Warning "Dev-auth middleware ENABLED — header-injected principals accepted"

        let app = builder.Build()
        app.UseSerilogRequestLogging() |> ignore
        app.UseGiraffe(buildApp devAuthEnabled dataSource)
        app.Run()
        0
    with ex ->
        Log.Fatal(ex, "Host terminated unexpectedly")
        1
