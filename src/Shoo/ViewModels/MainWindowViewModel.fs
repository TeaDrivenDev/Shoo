namespace Shoo.ViewModels

open System
open System.Collections.ObjectModel
open System.IO
open System.Reactive.Disposables

open FSharp.Control.Reactive

open Elmish
open Elmish.Avalonia

open Shoo
open TeaDrivenDev.Prelude
open TeaDrivenDev.Prelude.IO

module Main =
    [<Literal>]
    let shooFileNameExtension = ".__shoo__"

    [<Literal>]
    let bufferSize = 1024 * 1024

    type CreateMode = Create | Replace

    type CopyOperation =
        {
            Source: string
            Destination: string
            Extension: string
            FileViewModel: FileOperationViewModel
        }

    type Model =
        {
            SourceDirectory: ConfiguredDirectory
            DestinationDirectory: ConfiguredDirectory
            FileTypes: string
            ReplacementsFileName: string
            IsActive: bool
            FileQueue: ObservableCollection<FileOperationViewModel>
        }

    type Message =
        | UpdateSourceDirectory of string option
        | UpdateDestinationDirectory of string option
        | UpdateFileTypes of string
        | ChangeActive of bool
        | Terminate
        | QueueFileCopy of string
        | UpdateFileStatus of (FileOperationViewModel * int * MoveFileStatus)
        // TODO Temporary
        | RemoveFile

    let init () =
        {
            SourceDirectory =
                Path.Combine(
                    Environment.GetFolderPath Environment.SpecialFolder.UserProfile,
                    "Downloads")
                |> createConfiguredDirectory
            DestinationDirectory = ConfiguredDirectory.Empty
            FileTypes = ""
            ReplacementsFileName = ""
            IsActive = false
            FileQueue = ObservableCollection()
        }
        |> withoutCommand

    let getSafeDestinationFileName (filePath: string) extension =
        let directory = Path.GetDirectoryName filePath
        let fileName = Path.GetFileNameWithoutExtension filePath

        let rec getFileName count =
            let name =
                match count with
                | 1 -> fileName + extension
                | _ -> sprintf "%s (%i)%s" fileName count extension
                |> asSnd directory
                |> Path.Combine

            let file = FileInfo name

            if file.Exists
            then
                if file.Length = 0L
                then name, Replace
                else getFileName (count + 1)
            else name, Create

        getFileName 1

    let mkCopyOperation (fileViewModel: FileOperationViewModel) destinationDirectory =
        let source = fileViewModel.FullName
        let destinationFileName = Path.GetFileNameWithoutExtension source
        let destination = Path.Combine(destinationDirectory, destinationFileName + shooFileNameExtension)
        {
            Source = source
            Destination = destination
            Extension = Path.GetExtension source
            FileViewModel = fileViewModel
        }

    let copyFile copyOperation =
        File.Copy(copyOperation.Source, copyOperation.Destination)

        let time = (FileInfo copyOperation.Source).LastWriteTimeUtc

        File.SetLastWriteTimeUtc(copyOperation.Destination, time)
        let finalDestination, createMode =
            getSafeDestinationFileName copyOperation.Destination copyOperation.Extension

        if createMode = Replace
        then File.Delete finalDestination

        File.Move(copyOperation.Destination, finalDestination)

        let moveStatus =
            if FileInfo(finalDestination).Length > 0L
            then
                File.Delete copyOperation.Source
                Complete
            else Failed

        copyOperation.FileViewModel, 100, moveStatus

    let hasPendingOperations (fileQueue: FileOperationViewModel seq) =
        fileQueue |> Seq.exists (fun f -> f.Progress < 100)

    let update message model =
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
        | UpdateFileTypes fileTypes -> { model with FileTypes = fileTypes } |> withoutCommand
        | ChangeActive active -> { model with IsActive = active } |> withoutCommand
        | Terminate -> model |> withoutCommand
        | QueueFileCopy path ->
            let fileVM = new FileOperationViewModel(path)
            model.FileQueue.Add(fileVM)
            model |> withoutCommand
        | UpdateFileStatus (fileViewModel, progress, moveFileStatus) ->
            fileViewModel.Progress <- progress
            fileViewModel.Status <- moveFileStatus

            model |> withoutCommand
        // TODO Temporary
        | RemoveFile ->
            if model.FileQueue.Count > 0
            then model.FileQueue.RemoveAt 0

            model |> withoutCommand

    let subscriptions (model: Model) : Sub<Message> =

        /// Watches for file renames and adds them to the file copy queue.
        let watchFileSystemSub dispatch =
            let watcher = new FileSystemWatcher(EnableRaisingEvents = false)
            let subscription =
                watcher.Renamed
                |> Observable.subscribe (_.FullPath >> QueueFileCopy >> dispatch)

            watcher.Path <- model.SourceDirectory.Path
            watcher.EnableRaisingEvents <- true

            Disposable.create (fun () -> 
                watcher.Dispose()
                subscription.Dispose())

        /// Processes the pending file operation.
        let processQueueSub dispatch =
            model.FileQueue 
            |> Seq.tryFind (fun f -> f.Progress < 100)
            |> Option.iter (fun pfo ->
                mkCopyOperation pfo model.DestinationDirectory.Path
                |> copyFile
                |> UpdateFileStatus
                |> dispatch
            )
            Disposable.empty

        [
            if model.IsActive then
                [ nameof watchFileSystemSub ], watchFileSystemSub

                if model.FileQueue |> hasPendingOperations then
                    [ nameof processQueueSub ], processQueueSub
        ]

open Main

type MainWindowViewModel(folderPicker: Services.FolderPickerService) as this =
    inherit ReactiveElmishViewModel()

    let store = 
        Program.mkAvaloniaProgram init update
        |> Program.withSubscription subscriptions
        |> Program.withErrorHandler (fun (_, ex) -> printfn $"Error: %s{ex.Message}")
        |> Program.withConsoleTrace
        |> Program.mkStoreWithTerminate this Terminate

    member this.SourceDirectory = this.Bind(store, _.SourceDirectory.Path)
    member this.DestinationDirectory = this.Bind(store, _.DestinationDirectory.Path)
    member this.IsSourceDirectoryValid = this.Bind(store, _.SourceDirectory.PathExists)
    member this.IsDestinationDirectoryValid = this.Bind(store, _.DestinationDirectory.PathExists)
    member this.ReplacementsFileName = this.Bind(store, _.ReplacementsFileName)
    member this.FileTypes
        with get () = this.Bind(store, _.FileTypes)
        and set value = store.Dispatch(UpdateFileTypes value)

    member this.CanActivate =
        this.Bind (store, fun m ->
            m.SourceDirectory.PathExists
            && m.DestinationDirectory.PathExists
            && m.DestinationDirectory.Path <> m.SourceDirectory.Path)

    member this.IsActive
        with get () = this.Bind(store, _.IsActive)
        and set value = store.Dispatch(ChangeActive value)

    member this.FileQueue = this.Bind(store, _.FileQueue)

    member this.SelectSourceDirectory() = 
        task {
            let! path = folderPicker.TryPickFolder()
            return store.Dispatch(UpdateSourceDirectory path)
        }

    member this.SelectDestinationDirectory() =
        task {
            let! path = folderPicker.TryPickFolder()
            return store.Dispatch(UpdateDestinationDirectory path)
        }

    static member DesignVM =
        new MainWindowViewModel(Design.stub)