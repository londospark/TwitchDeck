open System
open Twitchdeck.OBSWebsockets
open System.Threading.Tasks

[<EntryPoint>]
let main argv =
    async {
        let! authResponse = OBS.authenticate None
        printfn "%A" authResponse
    } |> Async.RunSynchronously
    0