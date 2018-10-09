module UserInterface

open Xunit
open FsUnit.Xunit
open FsCheck.Xunit
open Twitchdeck.App
open Fabulous.DynamicViews
open FsCheck
open Xamarin.Forms
open Twitchdeck
open TestHelpers

[<Fact>]
let ``With no specified scenes we should be displaying the no scenes view`` () =
    let model = { SceneNames = []; SelectedScene = "" }
    Twitchdeck.App.view model ignore
    |> should equal Views.noScenes

[<Fact>]
let ``With a single scene defined we should see a single button`` () =
    let model = { SceneNames = ["Scene 1"]; SelectedScene = "" }
    Twitchdeck.App.view model ignore
    |> descendentsAndSelf
    |> List.filter rendersAs<Xamarin.Forms.Button>
    |> List.length
    |> should equal 1
    

[<Property>]
let ``The button for a single scene should have text matching the scene name`` (sceneName: string) =
    let model = { SceneNames = [sceneName]; SelectedScene = "" }
    let button = Twitchdeck.App.view model ignore
                |> descendentsAndSelf
                |> List.find rendersAs<Xamarin.Forms.Button>
                |> hydrate<Xamarin.Forms.Button>

    button.Text |> should equal sceneName
    
[<Property>]
let ``For each scene in the model we get a button`` (sceneNames: string list) =
    not sceneNames.IsEmpty ==>
        fun () ->
            let model = { SceneNames = sceneNames; SelectedScene = "" }
            Twitchdeck.App.view model ignore
            |> descendentsAndSelf
            |> List.filter rendersAs<Xamarin.Forms.Button>
            |> List.length
            |> should equal sceneNames.Length

[<Fact>]
let ``When a scene is selected then the relevant button should be highlighted`` () =
    let model = { SceneNames = ["Scene 1"; "Scene 2"]; SelectedScene = "Scene 2"}

    let button =
        Twitchdeck.App.view model ignore
        |> descendentsAndSelf
        |> List.filter rendersAs<Xamarin.Forms.Button>
        |> List.map hydrate<Xamarin.Forms.Button>
        |> List.find (fun x -> x.Text = model.SelectedScene)

    button.BackgroundColor |> should equal (Color.FromHex "#33B2FF")

[<Fact>]
let ``When we send a SelectScene message, the relavent button becomes highlighted`` () =
    let model = { SceneNames = ["Scene 1"; "Scene 2"]; SelectedScene = ""}

    let (model, _command) = Twitchdeck.App.update (Msg.SelectScene "Scene 1") model
    model.SelectedScene |> should equal "Scene 1"


[<Fact>]
let ``When we press a button it executes the command passed to it`` () =
    let model = { SceneNames = ["Scene 1"; "Scene 2"]; SelectedScene = "Scene 2"}

    let mutable messagesReceived = []

    Twitchdeck.App.view model (
        fun message ->
            messagesReceived <- message :: messagesReceived )
    |> descendentsAndSelf
    |> List.filter rendersAs<Xamarin.Forms.Button>
    |> List.map (fun button -> button |> attr<(unit -> unit)> "ButtonCommand")
    |> List.iter (fun func -> func ())

    messagesReceived |> should contain (SelectScene "Scene 1")
    messagesReceived |> should contain (SelectScene "Scene 2")


