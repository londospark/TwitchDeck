open System
open Twitchdeck.OBSWebsockets

[<EntryPoint>]
let main argv =
    printfn "%s" (Socket.socket "{\"request-type\": \"GetAuthRequired\", \"message-id\": \"1\"}" |> Async.RunSynchronously)
    0
