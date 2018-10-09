module TestHelpers

open Fabulous.DynamicViews

let rendersAs<'TView> (viewElement: ViewElement) : bool =
    viewElement.TargetType = typedefof<'TView>

let hydrate<'TView> (viewElement: ViewElement) : 'TView =
    viewElement.Create() :?> 'TView

let attr<'T> name (parent : ViewElement) =
    let (c : ValueOption<'T>) = parent.TryGetAttribute name
    match c with
    | ValueSome x -> x
    | ValueNone -> raise (System.Exception(sprintf "Attribute %s not found" name))

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

let descendents (view: ViewElement) : ViewElement list =
    descendentsAndSelf view
    |> List.filter (fun item -> not (item = view))


