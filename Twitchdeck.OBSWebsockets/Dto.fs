namespace Twitchdeck.OBSWebsockets

module Dto =
    
    open System
    open Chiron
    open Chiron.Operators
    open Microsoft.FSharp.Reflection

    //TODO: Finad a home for this.
    let getUnionCaseName (x:'a) = 
        match FSharpValue.GetUnionFields(x, typeof<'a>) with
        | case, _ -> case.Name
    
    type RequestType =
        | GetAuthRequired
        | GetSceneList
        | SetCurrentScene of string
        | SetMute of string * bool
        | Authenticate of string
    
    type Request =
        { requestType : RequestType;
            messageId : Guid }

        static member ToJson(request: Request) =
            let common =
                Json.write "request-type" (request.requestType |> getUnionCaseName)
                *> Json.write "message-id" (request.messageId.ToString())

            match request.requestType with
            | SetCurrentScene sceneName -> common *> Json.write "scene-name" sceneName
            | SetMute (source, mute) ->
                common
                *> Json.write "source" source
                *> Json.write "mute" mute
            
            | Authenticate auth ->
                common *> Json.write "auth" auth
            | _ -> common
    
    type Response = 
        { responseId : Guid; error: string option }

        static member New responseId error =
            { responseId = responseId |> Guid.Parse; error = error}

        static member Decoder =
            Response.New
            <!> Json.read "message-id"
            <*> Json.readOrDefault "error" None
        
        static member FromJson (_:Response) = Response.Decoder
    
    type Event = 
        { updateType : string }

        static member New updateType = { updateType = updateType }

        static member Decoder =
            Event.New
            <!> Json.read "update-type"
        
        static member FromJson (_:Event) = Event.Decoder

    type ReceivedFromOBS =
        | Response of Response
        | Event of Event

        static member Decoder =
            (Response.Decoder |>> Response) <|>
            (Event.Decoder |>> Event)
    
        static member FromJson(_:ReceivedFromOBS) = ReceivedFromOBS.Decoder
    
    type SwitchSceneEvent =
        { scene : string }
        static member FromJson (_:SwitchSceneEvent) =
                fun sceneName ->
                    { scene = sceneName }
            <!> Json.read "scene-name"
    
    type AuthRequired =
        {messageId: Guid; status: string; challenge: string; salt: string}
    
        static member New messageId status challenge salt = 
            {
                messageId = messageId |> Guid.Parse;
                status = status;
                challenge = challenge;
                salt = salt
            }
    
        static member Decoder =
            AuthRequired.New
            <!> Json.read "message-id"
            <*> Json.read "status"
            <*> Json.read "challenge"
            <*> Json.read "salt"
        
        static member FromJson (_:AuthRequired) = AuthRequired.Decoder
    
    type NoAuthRequired =
        {messageId: Guid; status: string}
    
        static member New messageId status = {messageId = messageId |> Guid.Parse; status = status}
    
        static member Decoder =
            NoAuthRequired.New
            <!> Json.read "message-id"
            <*> Json.read "status"
       
        static member FromJson (_:NoAuthRequired) = NoAuthRequired.Decoder
    
    type AuthChallenge =
        | AuthRequired of AuthRequired
        | NoAuthRequired of NoAuthRequired
    
        static member Decoder =
            (AuthRequired.Decoder |>> AuthRequired) <|>
            (NoAuthRequired.Decoder |>> NoAuthRequired)
    
        static member FromJson(_:AuthChallenge) = AuthChallenge.Decoder
     