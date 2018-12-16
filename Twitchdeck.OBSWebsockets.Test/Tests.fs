module Tests

open System
open FsUnit.Xunit
open FsCheck.Xunit
open Twitchdeck.OBSWebsockets.Dto
open Twitchdeck.OBSWebsockets

[<Property>]
let ``Serialisation of a GetAuthRequired request should work`` (id : Guid) =
    let request = { requestType = GetAuthRequired ; messageId = id }
    let expected =
        sprintf """{"message-id":"%s","request-type":"GetAuthRequired"}""" (id |> string)

    OBS.serialiseRequest request
    |> should equal expected

[<Property>]
let ``Serialisation of a SetCurrentScene request should work`` (id : Guid) =
    let request = { requestType = SetCurrentScene "Scene 1" ; messageId = id }
    let expected =
        sprintf """{"message-id":"%s","request-type":"SetCurrentScene","scene-name":"Scene 1"}""" (id |> string)

    OBS.serialiseRequest request
    |> should equal expected

[<Property>]
let ``When we aren't required to authenticate then we always get Ok when we try`` (id : Guid) =
    let challenge = sprintf """{
                        "authRequired": false,
                        "message-id": "%s",
                        "status": "ok"
                      }""" (id |> string)
    OBS.authenticateFromChallenge None challenge
    |> Async.RunSynchronously
    |> should equal (Result<unit, string>.Ok ())
