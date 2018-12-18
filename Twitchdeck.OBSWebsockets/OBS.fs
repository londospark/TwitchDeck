namespace Twitchdeck.OBSWebsockets

open System
open FSharp.Data
open Chiron

module OBS =
    open RequestResponse
    open FSharp.Data.JsonExtensions
    open Dto
    open System.Security.Cryptography

    let (|Ok|Error|) (result: Result<unit, string>) =
        match result with
        | Result.Ok () -> Ok
        | e -> Error e

    let weaver = RequestResponse.messageWeaver FsWebsocket.sendRequest
        
    let requestFromType type' =
        { requestType = type'; messageId = Guid.NewGuid() }

    let authRequiredRequest () = requestFromType GetAuthRequired

    let getSceneListRequest () = requestFromType GetSceneList

    let authenticateRequest auth = requestFromType <| Authenticate auth

    let setCurrentSceneRequest sceneName = requestFromType <| SetCurrentScene sceneName

    let setMuteRequest source mute = requestFromType <| SetMute (source, mute)
    
    let asyncRequest (request: Request) =
        async {
            let id = request.messageId
            return!
                weaver.PostAndAsyncReply
                    <| fun channel -> (Send (MessageId id, request |> Json.serialize |> Json.format, channel))
        }

    let exceptionToResult func =
        try
            Result.Ok (func ())
        with
        | _ as ex -> Result.Error (ex.Message) 

    let sendPasswordToOBS (password : string option) (salt : string) (challenge : string) =
        async {
            let encrypt (text: string): byte array = SHA256Managed.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))
            let base64 (bytes: byte array): string = System.Convert.ToBase64String(bytes)

            match password with
            | Some pass ->
                let secret = (pass + salt) |> encrypt |> base64
                let authResponse = (secret + challenge) |> encrypt |> base64
                let! response = asyncRequest (authenticateRequest authResponse)
                let authResponse : Response = response |> Json.parse |> Json.deserialize

                match authResponse.error with
                | None -> return Result.Ok ()
                | Some message -> return Result.Error message
            | None ->
                return Result.Error "Cannot login without password."
        }

    let authenticateFromChallenge (password : string option) (challenge: string): Async<Result<unit, string>> =
        async {
            let response : Result<AuthChallenge, string> =
                (fun () -> challenge |> Json.parse |> Json.deserialize) |> exceptionToResult
            
            return!
                match response with
                | Result.Ok authChallenge -> 
                    match authChallenge with 
                    | NoAuthRequired _ -> (Result.Ok () |> async.Return)
                    | AuthRequired info -> sendPasswordToOBS password info.salt info.challenge
                | Result.Error err -> err |> Result.Error |> async.Return
        }
     
    let authenticate (password : string option) : Async<Result<unit, string>> =
        async {
            let! response = asyncRequest (authRequiredRequest ())
            return! response |> authenticateFromChallenge password
        }

    let getSceneList () : Async<string * string list> =
            async {
                let! response = asyncRequest (getSceneListRequest ())
                let parsed = JsonValue.Parse(response)
                let scenes = parsed?scenes
                let currentScene = parsed?``current-scene``.AsString()
                let sceneList =
                    scenes.AsArray ()
                    |> Array.toList
                    |> List.map (fun jsarray -> jsarray?name.AsString())
                return (currentScene, sceneList)
            }

    let serialiseRequest (request: Request) = request |> Json.serialize |> Json.format

    let setCurrentScene (sceneName: string) =
        async {
            let! _ = asyncRequest (setCurrentSceneRequest sceneName)
            return ()
        }
        
    let setMute (source: string) (mute: bool) =
        async {
            let! _ = asyncRequest (setMuteRequest source mute)
            return ()
        }
    
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

    let startCommunication server port password : Async<Result<unit, string>> =
        async {
            weaver.Post Flush
            let! response = FsWebsocket.connectTo weaver server port
            match response with
            | Ok -> return! authenticate password
            | Error e -> return e
        }