namespace Twitchdeck

open System.Diagnostics
open Fabulous.Core
open Xamarin.Forms

module App = 
    open Twitchdeck.OBSWebsockets
    open Domain
        
    let setup dispatch =
        async {
            do! OBS.startCommunication ()
            OBS.getSceneList <|
                fun scenes ->
                    dispatch (UpdateScenes scenes)
                    async.Zero ()
        } |> Async.RunSynchronously
        OBS.registerSwitchScene <|
            fun event ->
                async {
                    dispatch (SceneChanged event.scene)
                }
                
    let init () =
        {
            SceneNames = [];
            SelectedScene = ""
            OBSConfig = NotConfigured
        }, Cmd.ofSub setup

    let changeSceneTo sceneName model =
        OBS.setCurrentScene sceneName
        { model with SelectedScene = sceneName }

    let update msg model =
        match msg with
        | UpdateScenes (scene, scenes) -> { model with SceneNames = scenes; SelectedScene = scene }, Cmd.none
        | SelectScene name -> model |> changeSceneTo name, Cmd.none
        | SceneChanged name -> { model with SelectedScene = name}, Cmd.none

    let view (model: Model) (dispatch: Msg -> unit) =
        Views.sceneView model dispatch

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
    //do runner.EnableLiveUpdate()
#endif    

    // Uncomment this code to save the application state to app.Properties using Newtonsoft.Json
    // See https://fsprojects.github.io/Fabulous/models.html for further  instructions.
#if APPSAVE
    let modelId = "model"
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
                let model = Newtonsoft.Json.JsonConvert.DeserializeObject<App.Model>(json)

                Console.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.SetCurrentModel (model, Cmd.none)

            | _ -> ()
        with ex -> 
            App.program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = 
        Console.WriteLine "OnStart: using same logic as OnResume()"
        this.OnResume()
#endif


