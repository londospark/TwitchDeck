module TestHeplerTests

open Fabulous.DynamicViews
open Xunit
open FsUnit.Xunit
open TestHelpers

let view =
    View.ContentPage(
        content = View.StackLayout(
            children = [
                View.StackLayout(
                    children = [View.Button(); View.Button(); View.Button()]);
                
                View.StackLayout(
                    children = [View.Button(); View.Button(); View.Button()])]))

[<Fact>]
let ``descendantsAndSelf returns the correct number of elements`` () =
    let desc = view |> descendentsAndSelf
    desc |> should haveLength 10

[<Fact>]
let ``descendants returns the correct number of elements`` () =
    let desc = view |> descendents
    desc |> should haveLength 9
    desc |> should not' (contain view)