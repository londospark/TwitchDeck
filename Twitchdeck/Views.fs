module Twitchdeck.Views

open Twitchdeck.Domain
open Fabulous.DynamicViews
open Xamarin.Forms
open System

let border (view: ViewElement) = view.BorderColor(Color.AliceBlue).BorderWidth(3.0)

let sfxButton name dispatcher =
    border <|
    View.Button(
        text = name,
        verticalOptions = LayoutOptions.FillAndExpand,
        command = fun () -> dispatcher (PlaySound name))

let noSfxView = View.Label(text="No Sound Effects Found.")

let muteButton (model : Domain.Model) dispatcher =
    border <|
    if model.Muted then
        View.Button(text="Unmute", command=(fun () -> (dispatcher UnmuteStream)))
    else
        View.Button(text="Mute", command=(fun () -> (dispatcher MuteStream)))

let someSfxView model dispatcher =
    View.StackLayout(
            children = [for effect in model.Sfx ->
                            sfxButton effect dispatcher])


let sfxView (model: Model) dispatcher =
    if model.Sfx.Length > 0 then
        someSfxView model dispatcher
    else
        noSfxView

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

    let currentPort =
        match model.OBSConfig with
        | Configuration obs -> obs.Port.ToString()
        | NotConfigured -> ""
    
    let port =
        match model.dynamicOBSConfig.TryFind "port" with
        | Some s when Int32.TryParse(s) |> fst -> Int32.Parse(s)
        | _ -> 4444

    let currentPassword =
        match model.OBSConfig with
        | Configuration { Password = Some pass } -> pass
        | _ -> ""
    
    let password =
        match model.dynamicOBSConfig.TryFind "password" with
        | Some s when not(System.String.IsNullOrEmpty(s)) -> Some s
        | _ -> None


    let serverCell =
        View.EntryCell(label="Server: ",
                       text = currentServer,
                       automationId = "obs_server_address",
                       textChanged=
                           fun args ->
                               dispatcher <| OBSConfigUpdate ("server", args.NewTextValue))

    let portCell =
        View.EntryCell(label="Port: ",
                       text = currentPort,
                       automationId = "obs_server_port",
                       keyboard=Keyboard.Numeric,
                       textChanged=
                           fun args ->
                               dispatcher <| OBSConfigUpdate ("port", args.NewTextValue))

    //TODO: Gareth - can we hide/show the password?
    let passwordCell =
        View.EntryCell(label="Password: ",
                       text = currentPassword,
                       automationId = "obs_password",
                       textChanged=
                           fun args ->
                               dispatcher <| OBSConfigUpdate ("password", args.NewTextValue))

    View.ContentPage(
        content =
            View.TableView(
                intent = TableIntent.Settings,
                items = [
                    ("OBS Config",
                        [ serverCell
                          portCell
                          passwordCell
                          View.ViewCell(
                            view=
                                View.Button(
                                    text="Update OBS Config",
                                    command=(fun () -> dispatcher <| SetOBSConfig {IPAddress = server; Port = port; Password = password})))])
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
    border <|
    View.Button(
        text = name,
        verticalOptions = LayoutOptions.FillAndExpand,
        backgroundColor = color,
        command = command)

let noScenes =
    View.Label(text="There are no scenes defined.")

let scenes (names: string list) selectedScene selectSceneCommand =
    View.StackLayout(
        automationId = "SceneButtonContainer",
        children = [for name in names ->
                    sceneButton name (name = selectedScene) (selectSceneCommand name)])

let sceneView (model: Domain.Model) dispatcher =
    if model.SceneNames.Length = 0 then
        noScenes
    else // Look into this from an architecture standpoint.
        scenes model.SceneNames model.SelectedScene (fun name () -> dispatcher <| SelectScene name)
        
let withMuteButton (view: ViewElement) title model dispatcher =
    View.ContentPage(
        title = title,
        content =
            View.AbsoluteLayout (
                children = [
                    view
                        .LayoutFlags(AbsoluteLayoutFlags.All)
                        .LayoutBounds(Rectangle(0.0, 0.0, 1.0, 0.8))
                    (muteButton model dispatcher)
                        .LayoutFlags(AbsoluteLayoutFlags.All)
                        .LayoutBounds(Rectangle(0.0, 1.0, 1.0, 0.2))
                ]
            )
    )

let main (model: Domain.Model) dispatcher =
    View.TabbedPage(
            children=[
                options model dispatcher
                withMuteButton (sceneView model dispatcher) "OBS Scenes" model dispatcher
                withMuteButton (sfxView model dispatcher) "Sounds" model dispatcher
            ])