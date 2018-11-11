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

//TODO: Gareth - make this go bang with a nicer error message.
let getAttr name (parent : ViewElement) = 
    let attr = parent.Attributes |> Array.find (fun kvp -> kvp.Key = name)
    attr.Value
    

//TODO: Gareth - Test this in the TestHelperTests.
let rec descendentsAndSelf (view: ViewElement) : ViewElement list =

    let itemsAndDescendentsOf attrName = 
        let sections = view.TryGetAttribute attrName
        [ match sections with
          | ValueSome s -> yield! [
            for section : string * ViewElement[] in s |> Array.toList do
            for item in (snd section) |> Array.toList do
                  yield! descendentsAndSelf item ]
          | ValueNone -> ()
        ]
        

    let contentAndDescendentsOf attrName =
        let (content : ValueOption<ViewElement>) = view.TryGetAttribute attrName
        [ match content with
          | ValueSome content' -> yield! (descendentsAndSelf content')
          | ValueNone -> () ]
    
    let childrenAndDescendentsOf attrName =
        let (children : ValueOption<ViewElement[]>) = view.TryGetAttribute attrName
        [ match children with
          | ValueSome children' ->
            for child in children' do
                yield! descendentsAndSelf child
          | ValueNone -> () ]

    [ yield! contentAndDescendentsOf "Content"
      yield! contentAndDescendentsOf "Master"
      yield! contentAndDescendentsOf "ContentOf"
      yield! childrenAndDescendentsOf "Children"
      yield! itemsAndDescendentsOf "TableRoot"
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

let findElementById (lookingFor: string) (view: ViewElement) : ViewElement =
    match tryFindElementById lookingFor view with
    | Some element -> element
    | None -> failwithf "The element with id: '%s' could not be found" lookingFor