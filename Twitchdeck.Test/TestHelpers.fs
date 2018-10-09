module TestHelpers

open Fabulous.DynamicViews

let rendersAs<'TView> (viewElement: ViewElement) : bool =
    viewElement.TargetType = typedefof<'TView>

let hydrate<'TView> (viewElement: ViewElement) : 'TView =
    viewElement.Create() :?> 'TView

let tryGetAttr<'T> name (parent : ViewElement) =
    let (c : ValueOption<'T>) = parent.TryGetAttribute name
    match c with
    | ValueSome x -> Some x
    | ValueNone -> None

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

let tryFindElementById (lookingFor: string) (view: ViewElement) : ViewElement option =
    view
    |> descendentsAndSelf
    |> List.tryFind (fun v ->
        match v |> tryGetAttr<string> "AutomationId" with
        | Some id -> lookingFor = id
        | None    -> false)