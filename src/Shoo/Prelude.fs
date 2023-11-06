module TeaDrivenDev.Prelude

open System
open System.IO

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
