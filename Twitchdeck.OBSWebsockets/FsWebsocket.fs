module FsWebsocket

open System.Net.WebSockets
open System.Threading
open System.Text
open RequestResponse
open Twitchdeck.OBSWebsockets.Dto
open System
open System.Threading.Tasks

//I'm sorry - please don't think any less of me :(
let mutable client = new ClientWebSocket()

//TODO: Can we try to deserialise into Event or Response with Chiron?
let weaveFrom (json: string) =
    let parsed : Result<ReceivedFromOBS, string> = json |> parseToResult
    match parsed with
    | Result.Ok (Response { responseId = id }) -> Weave.Receive ((MessageId id), json)
    | Result.Ok (Event { updateType = eventName }) -> Weave.Event ((ReceivedEvent eventName), json)
    | Result.Error _errorMessage -> Weave.Unknown
    
let receive (weaver: MailboxProcessor<Weave>) =
    let receiveBuffer = Array.create 255 0uy
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
    
//TODO: Gareth - This is a very very bad practicey thing to do!
let fireAndForget (fn : Task)  =
    async {
        try
            do! fn |> Async.AwaitTask
        with
        | err ->
            System.Diagnostics.Debug.WriteLine(err.Message)
    }

let sendRequest token (message : string) =
    async { 
        let encoding = new UTF8Encoding()
        
        let buffer = message |> encoding.GetBytes
        let bufferSegment = new ArraySegment<byte>(buffer)
        if client.State = WebSocketState.Open then
            do! client.SendAsync(bufferSegment, WebSocketMessageType.Text, true, token) |> fireAndForget 
    }

//TODO: Gareth - Did we connect or not?
let connectTo weaver server port =
    async {
        client.Dispose()
        client <- new ClientWebSocket();

        let! token = Async.CancellationToken
        let uriString = sprintf "ws://%s:%d" server port
        if Uri.IsWellFormedUriString(uriString, UriKind.Absolute) then
            let task = client.ConnectAsync(new Uri(uriString), token)
            do! task |> fireAndForget
            receive weaver |> ignore
    }

