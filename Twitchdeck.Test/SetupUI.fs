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
    dynamicOBSConfig = Map.empty
}

let OBSConfig = Configuration { IPAddress = "Some host"; Port = 8080; Password = None}

[<Fact>]
let ``When OBS is not configured we are given the option to do so``() =
    Views.options model ignore
    |> findElementById "obs-setup-button"
    |> getAttr "Text"
    |> should equal "Setup OBS"

[<Fact>]
let ``When OBS is configured we are given the option to modify the config``() =
    Views.options { model with OBSConfig = OBSConfig } ignore
    |> findElementById "obs-setup-button"
    |> getAttr "Text"
    |> should equal "Modify OBS Setup"

[<Fact>]
let ``The current OBS server name/IP is displayed in the settings``() =
    Views.obsDetail { model with OBSConfig = OBSConfig } ignore
    |> findElementById "obs_server_address"
