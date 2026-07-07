module AssetTracker.Server.Tests.PipelineTests

open System
open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Expecto
open Giraffe
open AssetTracker.Shared
open AssetTracker.Server

// Handler-level tests: no live server, real Giraffe pipeline semantics.

let private services =
    let sc = ServiceCollection()
    sc.AddGiraffe() |> ignore

    sc.AddSingleton(typeof<Json.ISerializer>, SystemTextJson.Serializer Program.jsonOptions)
    |> ignore

    sc.BuildServiceProvider()

let private makeContext (headers: (string * string) list) =
    let ctx = DefaultHttpContext()
    ctx.RequestServices <- services
    ctx.Response.Body <- new MemoryStream()

    for name, value in headers do
        ctx.Request.Headers[name] <- value

    ctx

let private run (handler: HttpHandler) (ctx: HttpContext) =
    (handler (fun c -> Task.FromResult(Some c)) ctx).Result

let private devHeaders role =
    [ "X-Dev-UserId", Guid.NewGuid().ToString()
      "X-Dev-Role", role
      "X-Dev-UnitId", Guid.NewGuid().ToString() ]

[<Tests>]
let devAuthTests =
    testList
        "DevAuth middleware"
        [ test "valid headers produce a principal" {
              let ctx = makeContext (devHeaders "UnitCustodian")
              run Auth.DevAuth.middleware ctx |> ignore
              let p = Auth.tryGetPrincipal ctx
              Expect.isSome p ""
              Expect.equal p.Value.Role UnitCustodian ""
          }
          test "unknown role produces no principal" {
              let ctx = makeContext (devHeaders "SuperAdmin")
              run Auth.DevAuth.middleware ctx |> ignore
              Expect.isNone (Auth.tryGetPrincipal ctx) ""
          }
          test "malformed user id produces no principal" {
              let ctx = makeContext [ "X-Dev-UserId", "not-a-guid"; "X-Dev-Role", "SystemAdmin" ]
              run Auth.DevAuth.middleware ctx |> ignore
              Expect.isNone (Auth.tryGetPrincipal ctx) ""
          } ]

[<Tests>]
let authEnforcementTests =
    testList
        "auth enforcement"
        [ test "dev headers alone never authenticate — 401 without the dev middleware" {
              // Production wiring (buildApp devAuthEnabled=false) omits DevAuth.middleware;
              // requireAuth must therefore reject even when dev headers are present.
              let ctx = makeContext (devHeaders "SystemAdmin")
              let handler = AssetsHttp.requireAuth (fun _ -> Successful.OK "in")
              run handler ctx |> ignore
              Expect.equal ctx.Response.StatusCode 401 ""
          }
          test "mutation without CSRF header is rejected" {
              let ctx = makeContext []
              ctx.Request.Method <- "POST"
              run (AssetsHttp.requireCsrfHeader >=> Successful.OK "in") ctx |> ignore
              Expect.equal ctx.Response.StatusCode 403 ""
          }
          test "mutation with CSRF header passes" {
              let ctx = makeContext [ "X-Requested-With", "XMLHttpRequest" ]
              ctx.Request.Method <- "POST"
              run (AssetsHttp.requireCsrfHeader >=> Successful.OK "in") ctx |> ignore
              Expect.equal ctx.Response.StatusCode 200 ""
          } ]
