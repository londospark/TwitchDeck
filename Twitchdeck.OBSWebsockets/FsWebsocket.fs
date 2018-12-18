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
    
let awaitResult (task : Task): Async<Result<unit, string>> =
    async {
        let! result = task |> Async.AwaitTask |> Async.Catch
        return
            match result with
            | Choice1Of2 _ -> Result.Ok ()
            | Choice2Of2 ex -> Result.Error ex.Message
    }

let sendRequest token (message : string): Async<Result<unit, string>> =
    async { 
        let encoding = new UTF8Encoding()
        
        let buffer = message |> encoding.GetBytes
        let bufferSegment = new ArraySegment<byte>(buffer)
        if client.State = WebSocketState.Open then
            return! client.SendAsync(bufferSegment, WebSocketMessageType.Text, true, token) |> awaitResult
        else
            return Result.Error "Websocket not open."
    }

let connectTo (weaver: MailboxProcessor<Weave>) (server: string) (port: int): Async<Result<unit, string>> =
    async {
        client.Dispose()
        client <- new ClientWebSocket();

        let! token = Async.CancellationToken
        let uriString = sprintf "ws://%s:%d" server port
        if Uri.IsWellFormedUriString(uriString, UriKind.Absolute) then
            let task = client.ConnectAsync(new Uri(uriString), token)
            let! result = task |> awaitResult
            return
                match result with
                | Result.Ok _ ->
                    receive weaver |> ignore
                    Result.Ok ()
                | Result.Error _ -> Result.Error "Could not connect."
        else
            return Result.Error "Malformed URL"
    }

