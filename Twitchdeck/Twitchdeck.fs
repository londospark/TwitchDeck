namespace Twitchdeck

open System.Diagnostics
open Fabulous.Core
open Xamarin.Forms
open System

module App = 
    open Twitchdeck.OBSWebsockets
    open Domain

    //TODO: Check that we can connect and handle errors
    let connectToObs (config : OBSConfiguration) dispatch =
        async {
            let! result = OBS.startCommunication config.IPAddress config.Port config.Password
            match result with
            | Result.Ok () -> 
                let! scenes = OBS.getSceneList ()
                dispatch (UpdateScenes scenes)
            | Result.Error err ->
                Debug.WriteLine(err)

        } |> Async.StartImmediate
    
    let attemptExternalConnections (model : Domain.Model) dispatch =
        match model.OBSConfig with
        | Configuration config -> connectToObs config dispatch
        | NotConfigured -> ()

    let setup dispatch =
        async {
            OBS.registerSwitchScene <|
                fun event ->
                    async { dispatch (SceneChanged event.scene) }

            let! sounds = SFX.soundList ()
            dispatch (Msg.GetSounds sounds)
        } |> Async.StartImmediate
                
    let init () =
        {
            SceneNames = [];
            SelectedScene = ""
            OBSConfig = NotConfigured
            dynamicOBSConfig = Map.empty
            Sfx = []
            Muted = false
        }, Cmd.ofSub setup

    let changeSceneTo sceneName _dispatch =
        async {
            do! OBS.setCurrentScene sceneName
        } |> Async.Start

    let mute source shouldMute _dispatch =
        async {
            do! OBS.setMute source shouldMute
        } |> Async.Start

    let update msg model =
        match msg with
        | UpdateScenes (scene, scenes) -> { model with SceneNames = scenes; SelectedScene = scene }, Cmd.none
        | SelectScene name -> { model with SelectedScene = name }, Cmd.ofSub (changeSceneTo name)
        | SceneChanged name -> { model with SelectedScene = name }, Cmd.none
        | SetOBSConfig config ->  { model with OBSConfig = Configuration config }, Cmd.ofSub (connectToObs config)
        | OBSConfigUpdate (key, value) -> { model with dynamicOBSConfig = model.dynamicOBSConfig |> Map.add key value }, Cmd.none
        | GetSounds soundList -> { model with Sfx = soundList }, Cmd.none
        | PlaySound sound -> model, Cmd.ofSub (SFX.play sound)
        | MuteStream -> { model with Muted = true }, Cmd.ofSub (mute "Headset" true)
        | UnmuteStream -> { model with Muted = false}, Cmd.ofSub (mute "Headset" false)

    let view (model: Model) (dispatch: Msg -> unit) =
        Views.main model dispatch

    // Note, this declaration is needed if you enable LiveUpdate
    let program = Program.mkProgram init update view

type App () as app = 
    inherit Application ()

    let runner = 
        try
            App.program
#if DEBUG
            |> Program.withConsoleTrace
#endif
            |> Program.runWithDynamicView app
        with
        | ex -> Debug.WriteLine(ex.Message)
                reraise ()

#if DEBUG
    // Uncomment this line to enable live update in debug mode. 
    // See https://fsprojects.github.io/Fabulous/tools.html for further  instructions.
    //
    do runner.EnableLiveUpdate()
#endif    

    // Uncomment this code to save the application state to app.Properties using Newtonsoft.Json
    // See https://fsprojects.github.io/Fabulous/models.html for further  instructions.

    let modelId = "twitchdeck"
    override __.OnSleep() = 

        let json = Newtonsoft.Json.JsonConvert.SerializeObject(runner.CurrentModel)
        Console.WriteLine("OnSleep: saving model into app.Properties, json = {0}", json)

        app.Properties.[modelId] <- json

    override __.OnResume() = 
        Console.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue modelId with
            | true, (:? string as json) -> 

                Console.WriteLine("OnResume: restoring model from app.Properties, json = {0}", json)
                let model = Newtonsoft.Json.JsonConvert.DeserializeObject<Domain.Model>(json)

                Console.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.SetCurrentModel (model, Cmd.ofSub (App.attemptExternalConnections model))

            | _ -> ()
        with ex -> 
            App.program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = 
        Console.WriteLine "OnStart: using same logic as OnResume()"
        this.OnResume()



