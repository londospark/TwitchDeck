module Tests

open System
open Xunit
open Twitchdeck.OBSWebsockets
open FsUnit.Xunit
open FsCheck.Xunit
open FSharp.Data

[<Property>]
let ``Serialisation of a GetAuthRequired request should work`` (id : Guid) =
    let request = { requestType = GetAuthRequired ; messageId = id }
    let expected =
        sprintf """{"request-type":"GetAuthRequired","message-id":"%s"}""" (id |> string)

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
    |> should equal (Result<unit, string>.Ok ())