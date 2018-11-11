module Twitchdeck.Views

open Twitchdeck.Domain
open Fabulous.DynamicViews
open Xamarin.Forms

let optionsMenu (model: Model) =
    View.ContentPage(
        title = "Options",
        content =
            View.StackLayout(
                children=[
                    yield match model.OBSConfig with
                          | Configuration _ -> View.Button(text="Modify OBS Setup", automationId="obs-setup-button")
                          | NotConfigured -> View.Button(text="Setup OBS", automationId="obs-setup-button")
                ]))
let obsDetail (model: Model) dispatcher =
    let currentServer =
        match model.OBSConfig with
        | Configuration obs -> obs.IPAddress
        | NotConfigured -> ""
    
    let server =
        match model.dynamicOBSConfig.TryFind "server" with
        | Some s -> s
        | None -> ""


    let serverCell =
        View.EntryCell(label="Server: ",
                       text = currentServer,
                       automationId = "obs_server_address",
                       completed=
                           fun text ->
                               dispatcher <| OBSConfigUpdate ("server", text))

    View.ContentPage(
        content =
            View.TableView(
                intent = TableIntent.Settings,
                items = [
                    ("OBS Config",
                        [ serverCell
                          View.EntryCell(label="Port: ", keyboard=Keyboard.Numeric)
                          //TODO: Gareth - can we hide/show the password?
                          View.EntryCell(label="Password: ")
                          View.ViewCell(
                            view=
                                View.Button(
                                    text="Update OBS Config",
                                    command=(fun () -> dispatcher <| SetOBSConfig {IPAddress = server; Port = 4444; Password = None})))])
                ]
            )
    )

let options (model: Model) dispatcher =
    View.MasterDetailPage(
        title = "Options",
        masterBehavior=MasterBehavior.Default,
        master = (optionsMenu model),
        isPresented = true,
        detail = (obsDetail model dispatcher))

let sceneButton name selected command =
    let color = if selected then (Color.FromHex "#33B2FF") else Color.Default
    View.Button(
        text = name,
        verticalOptions = LayoutOptions.FillAndExpand,
        backgroundColor = color,
        command = command)

let noScenes =
    View.ContentPage(
        title="OBS Scenes",
        content = View.Label(text="There are no scenes defined."))

let scenes (names: string list) selectedScene selectSceneCommand =
    View.ContentPage(
        title = "OBS Scenes",
        content = View.StackLayout(
                    automationId = "SceneButtonContainer",
                    children = [for name in names ->
                                    sceneButton name (name = selectedScene) (selectSceneCommand name)]))

let sceneView (model: Domain.Model) dispatcher =
    if model.SceneNames.Length = 0 then
        noScenes
    else // Look into this from an architecture standpoint.
        scenes model.SceneNames model.SelectedScene (fun name () -> dispatcher <| SelectScene name)
