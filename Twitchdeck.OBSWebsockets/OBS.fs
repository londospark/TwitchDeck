namespace Twitchdeck.OBSWebsockets

open System
open FSharp.Data
open Chiron

module OBS =
    open RequestResponse
    open FSharp.Data.JsonExtensions
    open Dto

    let weaver = RequestResponse.messageWeaver FsWebsocket.sendRequest

    let startCommunication server port =
        async {
            weaver.Post Flush
            do! FsWebsocket.connectTo weaver server port
        }
    
    let requestFromType type' =
        { requestType = type'; messageId = Guid.NewGuid() }

    let authRequiredRequest () = requestFromType GetAuthRequired

    let getSceneListRequest () = requestFromType GetSceneList

    let setCurrentSceneRequest sceneName = requestFromType <| SetCurrentScene sceneName
    
    let request (request: Request) (continuation: string -> Async<_>) =
        let id = request.messageId
        let receiver response =
            async {
                return! continuation response
            }
        weaver.Post (Send (MessageId id, request |> Json.serialize |> Json.format, receiver))

    let exceptionToResult func =
        try
            Result.Ok (func ())
        with
        | _ as ex -> Result.Error (ex.Message) 

    let authenticateFromChallenge (_password : string option) (challenge: string) =
        let response : Result<AuthChallenge, string> =
            (fun () -> challenge |> Json.parse |> Json.deserialize) |> exceptionToResult

        response |> Result.bind(fun challenge ->
            match challenge with 
            | NoAuthRequired _ -> Result.Ok ()
            | AuthRequired _ -> Result.Error "We don't yet support auth.")
     
    let authenticate (password : string option) =
        request (authRequiredRequest ()) <| fun response ->
            async {
                let _result = response |> authenticateFromChallenge password
                return ()
            }

    let getSceneList (callback) =
        request (getSceneListRequest ()) <| fun response ->
            async {
                let parsed = JsonValue.Parse(response)
                let scenes = parsed?scenes
                let currentScene = parsed?``current-scene``.AsString()
                let sceneList =
                    scenes.AsArray ()
                    |> Array.toList
                    |> List.map (fun jsarray -> jsarray?name.AsString())
                return! (currentScene, sceneList) |> callback
            }

    let serialiseRequest (request: Request) = request |> Json.serialize |> Json.format

    let setCurrentScene (sceneName: string) =
        request (setCurrentSceneRequest sceneName) <| fun _response -> async.Return( () )
    
    let registerEvent name callback =
        weaver.Post <| Register (ReceivedEvent name, callback)
    
    //TODO: Some error handling please.
    let switchSceneCallback callback json =
        let parsed : Result<SwitchSceneEvent, string> = json |> parseToResult
        match parsed with
        | Result.Ok event -> callback event
        | Result.Error _error -> async.Zero ()

    let registerSwitchScene callback =
        registerEvent "SwitchScenes" (switchSceneCallback callback)