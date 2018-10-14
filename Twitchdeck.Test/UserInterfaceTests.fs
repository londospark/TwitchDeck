module UserInterface

open System.Drawing
open Xamarin.Forms

open Xunit
open FsUnit.Xunit

open FsCheck
open FsCheck.Xunit

open TestHelpers

open Twitchdeck.App
open Twitchdeck

let getSceneButtonContainer rootView =
    match rootView |> tryFindElementById "SceneButtonContainer" with
    | Some element -> element
    | None -> raise (Xunit.Sdk.XunitException("Element 'SceneButtonContainer' Could not be found in the given view."))

[<Fact>]
let ``With no specified scenes we should be displaying the no scenes view`` () =
    let model = { SceneNames = []; SelectedScene = "" }
    Twitchdeck.App.view model ignore
    |> should equal Views.noScenes

[<Fact>]
let ``With a single scene defined we should see a single button`` () =
    let model = { SceneNames = ["Scene 1"]; SelectedScene = "" }
    Twitchdeck.App.view model ignore
    |> getSceneButtonContainer
    |> descendents
    |> List.filter rendersAs<Xamarin.Forms.Button>
    |> List.length
    |> should equal 1
    

[<Property>]
let ``The button for a single scene should have text matching the scene name`` (sceneName: string) =
    let model = { SceneNames = [sceneName]; SelectedScene = "" }
    let button = Twitchdeck.App.view model ignore
                |> getSceneButtonContainer
                |> descendents
                |> List.find rendersAs<Xamarin.Forms.Button>
                |> hydrate<Xamarin.Forms.Button>

    button.Text |> should equal sceneName
    
[<Property>]
let ``For each scene in the model we get a button within the correct container`` (sceneNames: string list) =
    not sceneNames.IsEmpty ==>
        fun () ->
            let model = { SceneNames = sceneNames; SelectedScene = "" }
            Twitchdeck.App.view model ignore
            |> getSceneButtonContainer
            |> descendents
            |> List.filter rendersAs<Xamarin.Forms.Button>
            |> List.length
            |> should equal sceneNames.Length

[<Fact>]
let ``When a scene is selected then the relevant button should be highlighted`` () =
    let model = { SceneNames = ["Scene 1"; "Scene 2"]; SelectedScene = "Scene 2"}

    let button =
        Twitchdeck.App.view model ignore
        |> getSceneButtonContainer
        |> descendents
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
    |> List.map (fun button ->
        match button |> tryGetAttr<(unit -> unit)> "ButtonCommand" with
        | Some fn -> fn
        | None    -> raise (Xunit.Sdk.XunitException(sprintf "Button with text: '%s' does not have a command" (button |> hydrate<Xamarin.Forms.Button>).Text)))
    |> List.iter (fun func -> func ())

    messagesReceived |> should contain (SelectScene "Scene 1")
    messagesReceived |> should contain (SelectScene "Scene 2")


