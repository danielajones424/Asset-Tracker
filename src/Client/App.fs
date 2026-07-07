module AssetTracker.Client.App

open Feliz
open Browser.Dom
open AssetTracker.Shared
open AssetTracker.Client

// App shell: session bootstrap (GET /session) then the custodian entry form.
// Routing and the full portal layout land in later feature branches (docs/05).

type private SessionState =
    | Loading
    | Ready of Dtos.SessionDto
    | Failed of string

[<ReactComponent>]
let App () =
    let session, setSession = React.useState Loading

    React.useEffectOnce (fun () ->
        async {
            let! result = Api.getSession ()

            match result with
            | Ok s -> setSession (Ready s)
            | Error(Api.Problem p) when p.Status = 401 ->
                setSession (
                    Failed "Not signed in. In development, configure the dev-auth proxy headers (see STATUS.md)."
                )
            | Error(Api.Problem p) -> setSession (Failed p.Title)
            | Error(Api.Network m) -> setSession (Failed $"Cannot reach the API: {m}")
        }
        |> Async.StartImmediate)

    Html.div
        [ prop.className "app-shell"
          prop.children
              [ Html.header
                    [ prop.className "app-header"
                      prop.children
                          [ Html.h1 "AssetTrack"
                            match session with
                            | Ready s -> Html.span [ prop.className "who"; prop.text $"{s.DisplayName} · %A{s.Role}" ]
                            | _ -> Html.none ] ]
                match session with
                | Loading -> Html.p "Loading session…"
                | Failed msg -> Html.p [ prop.className "form-failure"; prop.text msg ]
                | Ready s ->
                    match s.UnitId with
                    | Some unitId -> AssetForm.AssetForm unitId
                    | None ->
                        Html.p
                            [ prop.className "form-failure"
                              prop.text "Your account has no unit assignment; asset entry requires one." ] ] ]

let root = ReactDOM.createRoot (document.getElementById "root")
root.render (App())
