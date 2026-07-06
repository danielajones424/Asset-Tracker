module AssetTracker.Client.App

open Feliz
open Browser.Dom
open AssetTracker.Shared

// M0 scaffold: proves Fable compiles the Shared project and renders.
// Elmish app shell, routing, and portals land in feature branches per docs/05.

[<ReactComponent>]
let App () =
    let role = UnitCustodian // placeholder until session bootstrap (GET /session)

    Html.div
        [ prop.style [ style.fontFamily "system-ui"; style.padding 32 ]
          prop.children
              [ Html.h1 "AssetTrack"
                Html.p $"Scaffold OK — shared domain loaded, role sample: %A{role}" ] ]

let root = ReactDOM.createRoot (document.getElementById "root")
root.render (App())
