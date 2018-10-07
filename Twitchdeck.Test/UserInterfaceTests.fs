module UserInterface

open Xunit
open FsUnit.Xunit
open FsCheck.Xunit
open Twitchdeck.App
open Fabulous.DynamicViews
open FsCheck
open Xamarin.Forms
open Twitchdeck

let rendersAs<'TView> (viewElement: ViewElement) : bool =
    viewElement.TargetType = typedefof<'TView>

let hydrate<'TView> (viewElement: ViewElement) : 'TView =
    viewElement.Create() :?> 'TView

let attr<'T> name (parent : ViewElement) =
        let (c : ValueOption<'T>) = parent.TryGetAttribute name
        match c with
        | ValueSome x -> x
        | ValueNone -> raise (System.Exception(sprintf "Attribute %s not found" name))

let stackPanelChildren view =
    view 
    |> attr<ViewElement> "Content"
    |> attr<ViewElement[]> "Children"

let rec descendentsAndSelf (view: ViewElement) : ViewElement list =
    let (content : ValueOption<ViewElement>) = view.TryGetAttribute "Content"
    let (children : ValueOption<ViewElement[]>) = view.TryGetAttribute "Children"
    [ match content with
      | ValueSome content' -> yield! (descendentsAndSelf content')
      | ValueNone -> ()

      match children with
      | ValueSome children' ->
          for child in children' do
              yield! descendentsAndSelf child
      | ValueNone -> ()
      
      yield view ]

[<Fact>]
let ``Run some code`` () =
    let view =
        View.ContentPage(
            content = View.StackLayout(
                children = [
                View.StackLayout(
                    children = [View.Button(); View.Button(); View.Button()]);
                    
                View.StackLayout(
                    children = [View.Button(); View.Button(); View.Button()])]))

    let desc = view |> descendentsAndSelf
    desc

[<Fact>]
let ``With no specified scenes we should be displaying the no scenes view`` () =
    let model = { SceneNames = []; SelectedScene = "" }
    Twitchdeck.App.view model ignore
    |> should equal Views.noScenes

[<Fact>]
let ``With a single scene defined we should see a single button`` () =
    let model = { SceneNames = ["Scene 1"]; SelectedScene = "" }
    Twitchdeck.App.view model ignore
    |> stackPanelChildren
    |> Array.filter rendersAs<Xamarin.Forms.Button>
    |> Array.length
    |> should equal 1
    

[<Property>]
let ``The button for a single scene should have text matching the scene name`` (sceneName: string) =
    let model = { SceneNames = [sceneName]; SelectedScene = "" }
    let button = Twitchdeck.App.view model ignore
                |> stackPanelChildren
                |> Array.find rendersAs<Xamarin.Forms.Button>
                |> hydrate<Xamarin.Forms.Button>

    button.Text |> should equal sceneName
    
[<Property>]
let ``For each scene in the model we get a button`` (sceneNames: string list) =
    not sceneNames.IsEmpty ==>
        fun () ->
            let model = { SceneNames = sceneNames; SelectedScene = "" }
            Twitchdeck.App.view model ignore
            |> stackPanelChildren
            |> Array.filter rendersAs<Xamarin.Forms.Button>
            |> Array.length
            |> should equal sceneNames.Length

[<Fact>]
let ``When a scene is selected then the relevant button should be highlighted`` () =
    let model = { SceneNames = ["Scene 1"; "Scene 2"]; SelectedScene = "Scene 2"}

    let button =
        Twitchdeck.App.view model ignore
        |> stackPanelChildren
        |> Array.filter rendersAs<Xamarin.Forms.Button>
        |> Array.map hydrate<Xamarin.Forms.Button>
        |> Array.find (fun x -> x.Text = model.SelectedScene)

    button.BackgroundColor |> should equal (Color.FromHex "#33B2FF")
