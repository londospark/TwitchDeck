module Twitchdeck.Views

open Twitchdeck.Domain
open Fabulous.DynamicViews
open Xamarin.Forms

let sceneButton name selected command =
    let color = if selected then (Color.FromHex "#33B2FF") else Color.Default
    View.Button(
        text = name,
        verticalOptions = LayoutOptions.FillAndExpand,
        backgroundColor = color,
        command = command)

let noScenes =
    View.ContentPage(content = View.Label(text="There are no scenes defined."))

let scenes (names: string list) selectedScene selectSceneCommand =
    View.ContentPage(
        title = "OBS Scenes",
        content = View.StackLayout(
                    automationId = "SceneButtonContainer",
                    children = [for name in names ->
                                    sceneButton name (name = selectedScene) (selectSceneCommand name)]))

let sceneView (model: Domain.Model) dispatcher =
    if model.SceneNames.Length = 0 then
        noScenes
    else // Look into this from an architecture standpoint.
        scenes model.SceneNames model.SelectedScene (fun name () -> dispatcher <| SelectScene name)
