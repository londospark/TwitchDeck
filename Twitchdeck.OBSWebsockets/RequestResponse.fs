module RequestResponse

open System

type MessageId = MessageId of Guid
type ReceivedEvent = ReceivedEvent of string

type Weave =
    // Could this also be a request?
    | Send of MessageId * string * AsyncReplyChannel<string>
    | Receive of MessageId * string
    | Register of ReceivedEvent * (string -> Async<unit>)
    | Event of ReceivedEvent * string // Sent by OBS
    | Unknown
    | Flush
    | Quit

let messageWeaver (sender: Threading.CancellationToken -> string -> Async<Result<unit, string>>) =
    let start (processor: MailboxProcessor<_>) =
        let rec loop channels (events: Map<ReceivedEvent, string -> Async<unit>>) =
            async {
                let! token = Async.CancellationToken
                let! message = processor.Receive ()

                let continuation =
                    match message with
                    | Send (messageId, request, channel) ->
                        if channels |> Map.containsKey messageId then
                            failwithf "There's already a receiver defined for '%A'" messageId
                        async {
                            let! response =  request |> sender token
                            match response with
                            | Result.Ok () -> return! loop (channels |> Map.add messageId channel) events
                            | Result.Error error -> failwithf "Send error '%s'" error
                        }
                    | Receive (messageId, response) ->
                        match channels |> Map.tryFind messageId with
                        | None -> failwithf "No receiver found for message: '%A'" messageId
                        | Some channel -> 
                            channel.Reply(response)
                            loop (channels |> Map.remove messageId) events
                    | Event (event, json) ->
                        events
                        |> Map.filter (fun item _callback -> item = event)
                        |> Map.toSeq
                        |> Seq.iter (fun (_eventName, callback) -> (callback json) |> Async.Start)
                        loop channels events
                    | Unknown -> loop channels events
                    | Flush -> 
                        let rec flush (mbp: MailboxProcessor<_>) =
                            if mbp.CurrentQueueLength > 0 then
                                mbp.Receive() |> Async.RunSynchronously |> ignore
                                flush mbp

                        flush processor
                        loop channels events
                    | Register (event, callback) ->
                        loop channels (events |> Map.add event callback)
                    | Quit -> async.Return ()
                return! continuation
            }
        loop Map.empty Map.empty
    let proc = MailboxProcessor.Start start
    proc.Error.Add(
        fun err ->
            printfn "[ERROR!!!!]: %A" err)
    proc