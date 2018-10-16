namespace Twitchdeck.OBSWebsockets
open Newtonsoft.Json
open System
open FSharp.Data

type RequestType =
    | GetAuthRequired

type JsonRequest = {
    ``request-type`` : string
    ``message-id`` : Guid
}

type Request = {
    requestType : RequestType
    messageId : Guid
}

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

module FsWebsocket =
    open System.Net.WebSockets
    open System.Threading
    open System.Text

    let receive (client: ClientWebSocket) (receiveSegment: ArraySegment<byte>) (token: CancellationToken) : Async<string> =
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

    let sendRequest (message : string) =
        async { 
            let encoding = new UTF8Encoding()
            use client = new ClientWebSocket()
            let! token = Async.CancellationToken
            
            let buffer = message |> encoding.GetBytes
            let bufferSegment = new ArraySegment<byte>(buffer)
            
            let receiveBuffer = Array.create 8 0uy
            let receiveSegment = new ArraySegment<byte>(receiveBuffer)
            
            do! client.ConnectAsync(new Uri("ws://192.168.1.100:4444"), token) |> Async.AwaitTask
            do! client.SendAsync(bufferSegment, WebSocketMessageType.Text, true, token) |> Async.AwaitTask
            
            return! receive client receiveSegment token
        }
    
module OBS =
    open Microsoft.FSharp.Reflection
    let getUnionCaseName (x:'a) = 
        match FSharpValue.GetUnionFields(x, typeof<'a>) with
        | case, _ -> case.Name

    let serialiseRequest (request : Request) =
        let request = {
            ``request-type`` = request.requestType |> getUnionCaseName
            ``message-id`` = request.messageId
        }
        JsonConvert.SerializeObject(request)

    let authRequiredRequest () =
        { requestType = GetAuthRequired; messageId = Guid.NewGuid() }
    
    let sendRequest request =
        request |> serialiseRequest |> FsWebsocket.sendRequest
    
    let exceptionToResult func =
        try
            Ok (func ())
        with
        | _ as ex -> Error (ex.Message) 

    let authenticateFromChallenge (_password : string option) (challenge: string) =
        let response = (fun () -> AuthChallenge.Parse(challenge)) |> exceptionToResult
        response |> Result.bind(fun unwrapped ->
            match unwrapped.AuthRequired with 
            | false -> Ok ()
            | true -> Error "We don't yet support auth.")
     
    let authenticate (password : string option) =
        async {
            let! challenge = authRequiredRequest () |> sendRequest
            return challenge |> authenticateFromChallenge password
        }