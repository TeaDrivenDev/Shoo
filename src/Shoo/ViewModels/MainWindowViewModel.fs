namespace Shoo.ViewModels

open System
open System.IO

open Elmish.Avalonia

module MainWindowViewModel =
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

    type Model =
        {
            SourceDirectory: ConfiguredDirectory
            DestinationDirectory: ConfiguredDirectory
        }

    type Message =
        | UpdateSourceDirectory of string
        | UpdateDestinationDirectory of string
        | Terminate

    let createConfiguredDirectory path =
        {
            Path = path
            PathExists = not <| String.IsNullOrWhiteSpace path && Directory.Exists path
        }

    let init () =
        {
            SourceDirectory = ConfiguredDirectory.Empty
            DestinationDirectory = ConfiguredDirectory.Empty
        }

    let update message model =
        match message with
        | UpdateSourceDirectory value ->
            {
                model with
                    SourceDirectory = createConfiguredDirectory value
            }
        | UpdateDestinationDirectory value ->
            {
                model with
                    DestinationDirectory = createConfiguredDirectory value
            }
        | Terminate -> model

    let bindings () =
        [
            "SourceDirectory" |> Binding.twoWay((fun m -> m.SourceDirectory.Path), (fun s -> UpdateSourceDirectory s))
            "DestinationDirectory" |> Binding.twoWay((fun m -> m.DestinationDirectory.Path), (fun s -> UpdateDestinationDirectory s))
            "IsSourceDirectoryValid" |> Binding.oneWay(fun m -> m.SourceDirectory.PathExists)
            "IsDestinationDirectoryValid" |> Binding.oneWay(fun m -> m.DestinationDirectory.PathExists)
        ]

    let designVM = ViewModel.designInstance (init()) (bindings())

    let vm =
        AvaloniaProgram.mkSimple init update bindings
        |> ElmishViewModel.create
        |> ElmishViewModel.terminateOnViewUnloaded Terminate
        :> IElmishViewModel
