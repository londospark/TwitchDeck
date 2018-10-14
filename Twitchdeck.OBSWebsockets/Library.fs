namespace Twitchdeck.OBSWebsockets

module Socket =
    open System.Net.WebSockets
    open System
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

    let socket (message : string) =
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
