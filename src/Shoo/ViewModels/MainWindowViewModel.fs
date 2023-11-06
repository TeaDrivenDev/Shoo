namespace Shoo.ViewModels

open System
open System.IO

open Elmish.Avalonia

module MainWindowViewModel =
    type Model =
        {
            SourceDirectory: string
            DestinationDirectory: string
            IsSourceDirectoryValid: bool
            IsDestinationDirectoryValid: bool
        }

    type Message =
        | UpdateSourceDirectory of string
        | UpdateDestinationDirectory of string
        | Terminate

    let init () =
        {
            SourceDirectory = ""
            DestinationDirectory = ""
            IsSourceDirectoryValid = false
            IsDestinationDirectoryValid = false
        }

    let update message model =
        match message with
        | UpdateSourceDirectory value ->
            {
                model with
                    SourceDirectory = value
                    IsSourceDirectoryValid =
                        not <| String.IsNullOrWhiteSpace value
                        && Directory.Exists value
            }
        | UpdateDestinationDirectory value ->
            {
                model with
                    DestinationDirectory = value
                    IsDestinationDirectoryValid =
                        not <| String.IsNullOrWhiteSpace value
                        && Directory.Exists value
            }
        | Terminate -> model

    let bindings () =
        [
            "SourceDirectory" |> Binding.twoWay((fun m -> m.SourceDirectory), (fun s -> UpdateSourceDirectory s))
            "DestinationDirectory" |> Binding.twoWay((fun m -> m.DestinationDirectory), (fun s -> UpdateDestinationDirectory s))
            "IsSourceDirectoryValid" |> Binding.oneWay(fun m -> m.IsSourceDirectoryValid)
            "IsDestinationDirectoryValid" |> Binding.oneWay(fun m -> m.IsDestinationDirectoryValid)
        ]

    let designVM = ViewModel.designInstance (init()) (bindings())

    let vm =
        AvaloniaProgram.mkSimple init update bindings
        |> ElmishViewModel.create
        |> ElmishViewModel.terminateOnViewUnloaded Terminate
        :> IElmishViewModel
