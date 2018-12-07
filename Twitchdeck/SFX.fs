module SFX

open System.Net.Http
open System
open Chiron
open Chiron.Operators
open System.Text

type SoundEffects =
    { sounds : string list }

    static member New sounds = { sounds = sounds }

    static member Decoder =
        SoundEffects.New
        <!> Json.read "names"
    
    static member FromJson (_:SoundEffects) = SoundEffects.Decoder

type Request =
    { sound : string }
    
    static member ToJson(request: Request) =
        Json.write "sound" request.sound

let soundList () =
    async {
        use client = new HttpClient()
        let uri = Uri("http://192.168.1.100:8087/get_sounds")
        let! results = client.GetStringAsync(uri) |> Async.AwaitTask
        let effects : SoundEffects = results |> Json.parse |> Json.deserialize
        return effects.sounds
    }

let play soundName _dispatcher =
    async {
        use client = new HttpClient()
        let uri = Uri("http://192.168.1.100:8087/play_sound")
        let json = { sound = soundName } |> Json.serialize |> Json.format
        use content = new StringContent(json, Encoding.UTF8, "application/json");
        let! _ = client.PostAsync(uri, content) |> Async.AwaitTask
        return ()
    } |> Async.StartImmediate