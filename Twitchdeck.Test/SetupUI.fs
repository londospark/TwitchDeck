module SetupUI

open Xunit
open Twitchdeck.Domain
open Twitchdeck
open FsUnit.Xunit
open TestHelpers

let model : Model = {
    SceneNames = []
    SelectedScene = ""
    OBSConfig = NotConfigured
}

let OBSConfig = Configuration { IPAddress = "Some host"; Port = 8080; Password = None}

[<Fact>]
let ``When OBS is not configured we are given the option to do so``() =
    Views.options model
    |> tryFindElementById "setup"
    |> Option.isSome
    |> should equal true

[<Fact>]
let ``When OBS is configured we are given the option to modify the config``() =
    Views.options { model with OBSConfig = OBSConfig }
    |> tryFindElementById "modify"
    |> Option.isSome
    |> should equal true

[<Fact>]
let ``We may not modify a non-existing OBS Config``() =
    Views.options model
    |> tryFindElementById "modify"
    |> Option.isNone
    |> should equal true

[<Fact>]
let ``We may not set OBS up twice``() =
    Views.options { model with OBSConfig = OBSConfig }
    |> tryFindElementById "setup"
    |> Option.isNone
    |> should equal true

