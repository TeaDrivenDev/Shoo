namespace Shoo.ViewModels

open System
open System.Collections.ObjectModel

open Elmish
open Elmish.Avalonia

open TeaDrivenDev.Prelude
open TeaDrivenDev.Prelude.IO

module MainWindowViewModel =
    type File = { Name: string }

    type Model =
        {
            SourceDirectory: ConfiguredDirectory
            DestinationDirectory: ConfiguredDirectory
            FileTypes: string
            ReplacementsFileName: string
            IsActive: bool
            Files: ObservableCollection<File>
        }

    type Message =
        | UpdateSourceDirectory of string option
        | UpdateDestinationDirectory of string option
        | SelectSourceDirectory
        | SelectDestinationDirectory
        | UpdateFileTypes of string
        | ChangeActive of bool
        | Terminate
        // TODO Temporary
        | AddFile
        | RemoveFile

    let init () =
        {
            SourceDirectory = ConfiguredDirectory.Empty
            DestinationDirectory = ConfiguredDirectory.Empty
            FileTypes = ""
            ReplacementsFileName = ""
            IsActive = false
            Files = ObservableCollection()
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
        | UpdateFileTypes fileTypes -> { model with FileTypes = fileTypes }, Cmd.none
        | ChangeActive active -> { model with IsActive = active }, Cmd.none
        | Terminate -> model, Cmd.none
        // TODO Temporary
        | AddFile ->
            model.Files.Add({ Name = string DateTime.Now})
            model, Cmd.none
        | RemoveFile ->
            if model.Files.Count > 0
            then model.Files.RemoveAt 0

            model, Cmd.none

    let bindings () =
        [
            "SourceDirectory" |> Binding.twoWay((fun m -> m.SourceDirectory.Path), Some >> UpdateSourceDirectory)
            "DestinationDirectory" |> Binding.twoWay((fun m -> m.DestinationDirectory.Path), Some >> UpdateDestinationDirectory)
            "IsSourceDirectoryValid" |> Binding.oneWay(fun m -> m.SourceDirectory.PathExists)
            "IsDestinationDirectoryValid" |> Binding.oneWay(fun m -> m.DestinationDirectory.PathExists)
            "SelectSourceDirectory" |> Binding.cmd SelectSourceDirectory
            "SelectDestinationDirectory" |> Binding.cmd SelectDestinationDirectory
            "FileTypes" |> Binding.twoWay((fun m -> m.FileTypes), UpdateFileTypes)
            "CanActivate" |> Binding.oneWay(fun m -> m.SourceDirectory.PathExists && m.DestinationDirectory.PathExists)
            "IsActive" |> Binding.twoWay((fun m -> m.IsActive), ChangeActive)
            "Files" |> Binding.oneWay(fun m -> m.Files)

            // TODO Temporary
            "AddFile" |> Binding.cmd AddFile
            "RemoveFile" |> Binding.cmd RemoveFile
        ]

    let designVM =
        let model, _ = init ()
        model.Files.Add({ Name = string DateTime.Now })

        ViewModel.designInstance model (bindings ())

    let vm () =
        let tryPickFolder () =
            let fileProvider = Shoo.Services.Get<Shoo.FolderPickerService>()
            fileProvider.TryPickFolder()

        AvaloniaProgram.mkProgram init (update tryPickFolder) bindings
        |> ElmishViewModel.create
        |> ElmishViewModel.terminateOnViewUnloaded Terminate
        :> IElmishViewModel
