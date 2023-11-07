﻿[<AutoOpen>]
module TeaDrivenDev.Prelude

open System
open System.IO

open Elmish

let asFst second first = first, second
let asSnd first second = first, second

let withoutCommand model = model, Cmd.none

module IO =
    type ConfiguredDirectory =
        {
            Path: string
            PathExists: bool
        } with
        static member Empty =
            {
                Path = ""
                PathExists = false
            }

    let createConfiguredDirectory path =
        {
            Path = path
            PathExists = not <| String.IsNullOrWhiteSpace path && Directory.Exists path
        }
