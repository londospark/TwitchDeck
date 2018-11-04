module Twitchdeck.Domain

type ServiceConfig<'a> =
    | Configuration of 'a
    | NotConfigured

type OBSConfiguration = {
    IPAddress: string
    Port: int
    Password: string option
}

type Model = {
    SceneNames: string list
    SelectedScene: string
    OBSConfig: ServiceConfig<OBSConfiguration>
}

//TODO: Strong typing of scenes?
type Msg =
    | UpdateScenes of string * string list
    | SelectScene of string
    | SceneChanged of string