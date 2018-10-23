namespace Twitchdeck.OBSWebsockets
open Newtonsoft.Json
open System
open FSharp.Data
open Chiron
open Chiron.Operators

type RequestType =
    | GetAuthRequired
    | GetSceneList
    | SetCurrentScene of string

type Request = {
    requestType : RequestType
    messageId : Guid
}

type Response = 
    { responseId : Guid }
    static member FromJson (_:Response) =
        fun id ->
          { responseId = id |> Guid.Parse }
    <!> Json.read "message-id"
type AuthChallenge = JsonProvider<"""
[{
    "authRequired": true,
    "challenge": "iNOoXNawcPXjI2nOZ5gOX0p8dhuSBf/atEdWI2F8wF8=",
    "message-id": "afd51f19-ebd6-498f-9f7c-0a2065a725c8",
    "salt": "Dz0ZE62xHw2BWo0Kc6k5gLe2WRf5gVubKb9sm3lgnuc=",
    "status": "ok"
},
{
    "authRequired": false,
    "message-id": "afd51f19-ebd6-498f-9f7c-0a2065a725c8",
    "status": "ok"
}]""", SampleIsList=true>

module RequestResponse =

    type MessageId = MessageId of Guid

    type Weave =
        // Could this also be a request?
        | Send of MessageId * string * (string -> Async<unit>)
        | Receive of MessageId * string
        | Event // Sent by OBS
        | Quit

    let messageWeaver sender =
        let start (processor: MailboxProcessor<_>) =
            let rec loop callbacks =
                async {
                    let! token = Async.CancellationToken
                    let! message = processor.Receive ()

                    let continuation =
                        match message with
                        | Send (messageId, request, receiver) ->
                            if callbacks |> Map.containsKey messageId then
                                failwithf "There's already a receiver defined for '%A'" messageId
                            async {
                                do! request |> sender token
                                return! loop (callbacks |> Map.add messageId receiver)
                            }
                        | Receive (messageId, response) ->
                            match callbacks |> Map.tryFind messageId with
                            | None -> failwithf "No receiver found for message: '%A'" messageId
                            | Some callback -> 
                                async {
                                    do! callback response
                                    return! loop (callbacks |> Map.remove messageId)
                                }
                        | Event -> loop callbacks
                        | Quit -> async.Return ()
                    return! continuation
                }
            loop Map.empty
        MailboxProcessor.Start start



module FsWebsocket =
    open System.Net.WebSockets
    open System.Threading
    open System.Text
    open RequestResponse

    let client = new ClientWebSocket()

    let weaveFrom (json: string) =
        let parsed : Choice<Response, string> = json |> Json.parse |> Json.tryDeserialize
        match parsed with
        | Choice1Of2 { responseId = id } -> Receive ((MessageId id), json)
        | Choice2Of2 _ -> Event
        
    let receive (weaver: MailboxProcessor<Weave>) =
        let receiveBuffer = Array.create 8 0uy
        let receiveSegment = new ArraySegment<byte>(receiveBuffer)
        let token = Async.CancellationToken |> Async.RunSynchronously
        let start (_processor: MailboxProcessor<_>) =
            let rec loop (client: ClientWebSocket) (receiveSegment: ArraySegment<byte>) (token: CancellationToken) =
                async {
                    let receiveString (client: ClientWebSocket) (receiveSegment: ArraySegment<byte>) (token: CancellationToken) : Async<string> =
                        let rec receiveImpl (buffer : ResizeArray<byte>) : Async<ResizeArray<byte>> =
                            async {
                                let! result = client.ReceiveAsync(receiveSegment, token) |> Async.AwaitTask
                                let trimmed = receiveSegment.Array |> Array.take result.Count
                                buffer.AddRange trimmed
                                return! match result.EndOfMessage with
                                        | true -> buffer |> async.Return
                                        | false -> receiveImpl buffer
                            }
                        async {
                            let! message = receiveImpl (new ResizeArray<byte>())
                            let encoding = new UTF8Encoding()
                            return message.ToArray() |> encoding.GetString
                        }
                    let! response = receiveString client receiveSegment token
                    let weave = weaveFrom response
                    do weaver.Post weave
                    do! loop client receiveSegment token
                }
            loop client receiveSegment token
        MailboxProcessor.Start start
        

    let sendRequest token (message : string) =
        async { 
            let encoding = new UTF8Encoding()
            
            let buffer = message |> encoding.GetBytes
            let bufferSegment = new ArraySegment<byte>(buffer)
            
            do! client.SendAsync(bufferSegment, WebSocketMessageType.Text, true, token) |> Async.AwaitTask
        }

    let start weaver =
        async {
            let! token = Async.CancellationToken 
            do! client.ConnectAsync(new Uri("ws://192.168.1.100:4444"), token) |> Async.AwaitTask
            receive weaver |> ignore
        }

module OBS =
    open RequestResponse
    open Microsoft.FSharp.Reflection
    open FSharp.Data.JsonExtensions
    let weaver = RequestResponse.messageWeaver FsWebsocket.sendRequest

    let startCommunication () =
        async { do! FsWebsocket.start weaver }


    let getUnionCaseName (x:'a) = 
        match FSharpValue.GetUnionFields(x, typeof<'a>) with
        | case, _ -> case.Name

    let serialiseRequest (request : Request) =
        let requestMap =
            [ ("request-type", request.requestType |> getUnionCaseName)
              ("message-id", request.messageId.ToString()) ] |> Map.ofList

        match request.requestType with
        | SetCurrentScene sceneName -> requestMap |> Map.add "scene-name" sceneName
        | _ -> requestMap
        |> JsonConvert.SerializeObject
    
    let requestFromType type' =
        { requestType = type'; messageId = Guid.NewGuid() }

    let authRequiredRequest () = requestFromType GetAuthRequired

    let getSceneListRequest () = requestFromType GetSceneList

    let setCurrentSceneRequest sceneName = requestFromType <| SetCurrentScene sceneName
    
    let request request (continuation: string -> Async<_>) =
        let id = request.messageId
        let receiver response =
            async {
                return! continuation response
            }
        weaver.Post (Send (MessageId id, request |> serialiseRequest, receiver))

    let exceptionToResult func =
        try
            Result.Ok (func ())
        with
        | _ as ex -> Result.Error (ex.Message) 

    let authenticateFromChallenge (_password : string option) (challenge: string) =
        let response = (fun () -> AuthChallenge.Parse(challenge)) |> exceptionToResult
        response |> Result.bind(fun unwrapped ->
            match unwrapped.AuthRequired with 
            | false -> Result.Ok ()
            | true -> Result.Error "We don't yet support auth.")
     
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

    let setCurrentScene (sceneName: string) =
        request (setCurrentSceneRequest sceneName) <| fun _response -> async.Return( () )