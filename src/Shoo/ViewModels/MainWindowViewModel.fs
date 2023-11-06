namespace Shoo.ViewModels

open Elmish
open Elmish.Avalonia

open TeaDrivenDev.Prelude
open TeaDrivenDev.Prelude.IO

module MainWindowViewModel =
    type Model =
        {
            SourceDirectory: ConfiguredDirectory
            DestinationDirectory: ConfiguredDirectory
        }

    type Message =
        | UpdateSourceDirectory of string option
        | UpdateDestinationDirectory of string option
        | SelectSourceDirectory
        | SelectDestinationDirectory
        | Terminate

    let init () =
        {
            SourceDirectory = ConfiguredDirectory.Empty
            DestinationDirectory = ConfiguredDirectory.Empty
        },
        Cmd.none

    let update tryPickFolder message model =
        match message with
        | UpdateSourceDirectory value ->
            value
            |> Option.map
                (fun path ->
                    {
                        model with
                            SourceDirectory = createConfiguredDirectory path
                    })
            |> Option.defaultValue model
            |> asFst Cmd.none
        | UpdateDestinationDirectory value ->
            value
            |> Option.map
                (fun path ->
                    {
                        model with
                            DestinationDirectory = createConfiguredDirectory path

                    })
            |> Option.defaultValue model
            |> asFst Cmd.none
        | SelectSourceDirectory ->
            model, Cmd.OfTask.perform tryPickFolder () UpdateSourceDirectory
        | SelectDestinationDirectory ->
            model, Cmd.OfTask.perform tryPickFolder () UpdateDestinationDirectory
        | Terminate -> model, Cmd.none

    let bindings () =
        [
            "SourceDirectory" |> Binding.twoWay((fun m -> m.SourceDirectory.Path), Some >> UpdateSourceDirectory)
            "DestinationDirectory" |> Binding.twoWay((fun m -> m.DestinationDirectory.Path), Some >> UpdateDestinationDirectory)
            "IsSourceDirectoryValid" |> Binding.oneWay(fun m -> m.SourceDirectory.PathExists)
            "IsDestinationDirectoryValid" |> Binding.oneWay(fun m -> m.DestinationDirectory.PathExists)
            "SelectSourceDirectory" |> Binding.cmd SelectSourceDirectory
            "SelectDestinationDirectory" |> Binding.cmd SelectDestinationDirectory
        ]

    let designVM = ViewModel.designInstance (fst (init())) (bindings())

    let vm () =
        let tryPickFolder () =
            let fileProvider = Shoo.Services.Get<Shoo.FolderPickerService>()
            fileProvider.TryPickFolder()

        AvaloniaProgram.mkProgram init (update tryPickFolder) bindings
        |> ElmishViewModel.create
        |> ElmishViewModel.terminateOnViewUnloaded Terminate
        :> IElmishViewModel
