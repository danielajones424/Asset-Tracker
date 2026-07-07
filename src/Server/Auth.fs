module AssetTracker.Server.Auth

open System
open Microsoft.AspNetCore.Http
open Giraffe
open AssetTracker.Shared

// The authentication SEAM. CAC/mTLS middleware (feature/aws-auth, docs/09) will
// populate the same Principal at the same ctx.Items key; everything downstream
// (scoping, handlers, repositories) is already real and stays unchanged.

type Principal =
    { UserId: UserId
      DisplayName: string
      Role: AppRole
      UnitId: UnitId option
      SquadronId: SquadronId option }

module Principal =
    /// Data scope derived from role (docs/09 §2) — required by every repository query
    let scope (p: Principal) : Result<DataScope, string> =
        match p.Role, p.UnitId, p.SquadronId with
        | SystemAdmin, _, _ -> Ok AllScope
        | SquadronAdmin, _, Some s -> Ok(SquadronScope s)
        | (UnitCustodian | UnitMember), Some u, _ -> Ok(UnitScope u)
        | r, _, _ -> Error $"user has role %A{r} but no matching scope assignment"

    let canWriteAssets (p: Principal) =
        match p.Role with
        | UnitCustodian
        | SquadronAdmin
        | SystemAdmin -> true
        | UnitMember -> false

let private principalKey = "Principal"

let setPrincipal (ctx: HttpContext) (p: Principal) = ctx.Items[principalKey] <- p

let tryGetPrincipal (ctx: HttpContext) : Principal option =
    match ctx.Items.TryGetValue principalKey with
    | true, (:? Principal as p) -> Some p
    | _ -> None

/// DEV ONLY — header-injected principal. Wired only when
/// ASPNETCORE_ENVIRONMENT=Development AND AssetTracker__DevAuth=true; the
/// production pipeline never contains this middleware (verified by test).
module DevAuth =
    let private tryHeader (ctx: HttpContext) (name: string) =
        match ctx.TryGetRequestHeader name with
        | Some v when not (String.IsNullOrWhiteSpace v) -> Some v
        | _ -> None

    let private tryGuid (s: string) =
        match Guid.TryParse s with
        | true, g -> Some g
        | _ -> None

    let private tryRole =
        function
        | "UnitMember" -> Some UnitMember
        | "UnitCustodian" -> Some UnitCustodian
        | "SquadronAdmin" -> Some SquadronAdmin
        | "SystemAdmin" -> Some SystemAdmin
        | _ -> None

    /// Headers: X-Dev-UserId (guid), X-Dev-Role, X-Dev-UnitId?, X-Dev-SquadronId?
    let middleware: HttpHandler =
        fun next ctx ->
            let principal =
                match
                    tryHeader ctx "X-Dev-UserId" |> Option.bind tryGuid,
                    tryHeader ctx "X-Dev-Role" |> Option.bind tryRole
                with
                | Some uid, Some role ->
                    Some
                        { UserId = UserId uid
                          DisplayName = tryHeader ctx "X-Dev-Name" |> Option.defaultValue "Dev User"
                          Role = role
                          UnitId = tryHeader ctx "X-Dev-UnitId" |> Option.bind tryGuid |> Option.map UnitId
                          SquadronId = tryHeader ctx "X-Dev-SquadronId" |> Option.bind tryGuid |> Option.map SquadronId }
                | _ -> None

            principal |> Option.iter (setPrincipal ctx)
            next ctx
