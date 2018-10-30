module RequestResponse

open System

type MessageId = MessageId of Guid
type ReceivedEvent = ReceivedEvent of string

type Weave =
    // Could this also be a request?
    | Send of MessageId * string * (string -> Async<unit>)
    | Receive of MessageId * string
    | Register of ReceivedEvent * (string -> Async<unit>)
    | Event of ReceivedEvent * string // Sent by OBS
    | Unknown
    | Quit

let messageWeaver sender =
    let start (processor: MailboxProcessor<_>) =
        let rec loop callbacks (events: Map<ReceivedEvent, string -> Async<unit>>) =
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
                            return! loop (callbacks |> Map.add messageId receiver) events
                        }
                    | Receive (messageId, response) ->
                        match callbacks |> Map.tryFind messageId with
                        | None -> failwithf "No receiver found for message: '%A'" messageId
                        | Some callback -> 
                            async {
                                do! callback response
                                return! loop (callbacks |> Map.remove messageId) events
                            }
                    | Event (event, json) ->
                        events
                        |> Map.filter (fun item _callback -> item = event)
                        |> Map.toSeq
                        |> Seq.iter (fun (_eventName, callback) -> (callback json) |> Async.Start)
                        loop callbacks events
                    | Unknown -> loop callbacks events
                    | Register (event, callback) ->
                        loop callbacks (events |> Map.add event callback)
                    | Quit -> async.Return ()
                return! continuation
            }
        loop Map.empty Map.empty
    MailboxProcessor.Start start