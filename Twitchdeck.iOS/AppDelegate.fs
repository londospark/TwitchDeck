// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
namespace Twitchdeck.iOS

open System
open UIKit
open Foundation
open Xamarin.Forms
open Xamarin.Forms.Platform.iOS
open System.Diagnostics

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit FormsApplicationDelegate ()

    override this.FinishedLaunching (app, options) =
        Forms.Init()
        let appcore = new Twitchdeck.App()
        this.LoadApplication (appcore)
        base.FinishedLaunching(app, options)

module Main =
    [<EntryPoint>]
    let main args =
        try 
            UIApplication.Main(args, null, "AppDelegate")
            0
        with
        | ex ->
            Debug.WriteLine(ex.Message)
            reraise ()
