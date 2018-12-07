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
    dynamicOBSConfig: Map<string, string>
    Sfx: string list
    Muted: bool
}

//TODO: Strong typing of scenes?
type Msg =
    // OBS Config
    | SetOBSConfig of OBSConfiguration
    | OBSConfigUpdate of string * string

    // OBS Usage
    | UpdateScenes of string * string list
    | SelectScene of string
    | SceneChanged of string

    | MuteStream
    | UnmuteStream

    //SFX
    | GetSounds of string list
    | PlaySound of string
