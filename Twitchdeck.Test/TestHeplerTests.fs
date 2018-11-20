module TestHeplerTests

open Fabulous.DynamicViews
open Xunit
open FsUnit.Xunit
open TestHelpers

let expectedElement = View.StackLayout(
                        automationId = "Layout",
                        children = [View.Button(); View.Button(); View.Button()])

let view =
    View.ContentPage(
        content = View.StackLayout(
            children = [
                expectedElement;
                
                View.StackLayout(
                    children = [View.Button(); View.Button(); View.Button()])]))

[<Fact>]
let ``descendantsAndSelf returns the correct number of elements`` () =
    view
    |> descendentsAndSelf
    |> should haveLength 10

[<Fact>]
let ``descendants returns the correct number of elements`` () =
    let desc = view |> descendents
    desc |> should haveLength 9
    desc |> should not' (contain view)

[<Fact>]
let ``findElementById should find the correct element`` () =
    let result = view |> tryFindElementById "Layout"
    result |> should equal (Some expectedElement)

[<Fact>]
let ``findElementById should handle the element not being present gracefully`` () =
    let result = view |> tryFindElementById "Non-existent"
    result |> should equal None