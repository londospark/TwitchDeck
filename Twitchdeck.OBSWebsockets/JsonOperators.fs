[<AutoOpen>]
module JsonOperators

open Chiron

let inline orElse (a: Json<'a>) (b: Json<'a>) : Json<'a> =
    fun json ->
        match a json with
        | Value a, json -> Value a, json
        | Error e, json -> 
          match b json with
          | Value b, json -> Value b, json
          | Error f, json -> Error (e + "|" + f), json
          
let inline parseToResult json =
    match json |> Json.parse |> Json.tryDeserialize with
    | Choice1Of2 success -> Result.Ok success
    | Choice2Of2 error -> Result.Error error

let inline (<|>) a b = orElse a b
let (|>>) m f = Json.map f m
