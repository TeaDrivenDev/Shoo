namespace Shoo.ViewModels

open System
open System.Collections.ObjectModel
open System.IO

open FSharp.Control.Reactive

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
            Files: ObservableCollection<obj>
        }

    type Message =
        | UpdateSourceDirectory of string option
        | UpdateDestinationDirectory of string option
        | SelectSourceDirectory
        | SelectDestinationDirectory
        | UpdateFileTypes of string
        | ChangeActive of bool
        | Terminate
        | AddFile of string
        // TODO Temporary
        | RemoveFile

    let init () =
        {
            SourceDirectory = ConfiguredDirectory.Empty
            DestinationDirectory = ConfiguredDirectory.Empty
            FileTypes = ""
            ReplacementsFileName = ""
            IsActive = false
            Files = ObservableCollection()
        }
        |> withoutCommand

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
            |> withoutCommand
        | UpdateDestinationDirectory value ->
            value
            |> Option.map
                (fun path ->
                    {
                        model with
                            DestinationDirectory = createConfiguredDirectory path
                    })
            |> Option.defaultValue model
            |> withoutCommand
        | SelectSourceDirectory ->
            model, Cmd.OfTask.perform tryPickFolder () UpdateSourceDirectory
        | SelectDestinationDirectory ->
            model, Cmd.OfTask.perform tryPickFolder () UpdateDestinationDirectory
        | UpdateFileTypes fileTypes -> { model with FileTypes = fileTypes } |> withoutCommand
        | ChangeActive active -> { model with IsActive = active } |> withoutCommand
        | Terminate -> model |> withoutCommand
        | AddFile path ->
            let vm = FileViewModel.vm path

            model.Files.Add(vm)
            model |> withoutCommand
        // TODO Temporary
        | RemoveFile ->
            if model.Files.Count > 0
            then model.Files.RemoveAt 0

            model |> withoutCommand

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
            "RemoveFile" |> Binding.cmd RemoveFile
        ]

    let designVM =
        let model, _ = init ()
        model.Files.Add(FileViewModel.designVM)

        ViewModel.designInstance model (bindings ())

    let subscriptions (watcher: FileSystemWatcher) (model: Model) : Sub<Message> =
        let watchFileSystem dispatch =
            let subscription =
                watcher.Renamed
                |> Observable.subscribe (fun e -> e.FullPath |> AddFile |> dispatch)

            watcher.Path <- model.SourceDirectory.Path
            watcher.EnableRaisingEvents <- true

            {
                new IDisposable with
                    member _.Dispose() =
                        watcher.EnableRaisingEvents <- false
                        subscription.Dispose()
            }

        [
            if model.IsActive then [ nameof watchFileSystem ], watchFileSystem
        ]

    let vm () =
        let tryPickFolder () =
            let fileProvider = Shoo.Services.Get<Shoo.FolderPickerService>()
            fileProvider.TryPickFolder()

        let watcher = new FileSystemWatcher(EnableRaisingEvents = false)

        AvaloniaProgram.mkProgram init (update tryPickFolder) bindings
        |> AvaloniaProgram.withSubscription (subscriptions watcher)
        |> ElmishViewModel.create
        |> ElmishViewModel.terminateOnViewUnloaded Terminate
        :> IElmishViewModel
