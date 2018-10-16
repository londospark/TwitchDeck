open System
open Twitchdeck.OBSWebsockets
open System.Threading.Tasks

[<EntryPoint>]
let main argv =
    async {
        let! scenes = OBS.getSceneList ()
        printfn "%A" scenes
    } |> Async.RunSynchronously
    0