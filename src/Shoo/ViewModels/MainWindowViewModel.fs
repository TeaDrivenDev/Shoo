namespace Shoo.ViewModels

open System
open System.Collections.ObjectModel
open System.IO
open System.Reactive.Disposables

open FSharp.Control.Reactive

open Elmish
open Elmish.Avalonia

open TeaDrivenDev.Prelude
open TeaDrivenDev.Prelude.IO

type Subject<'T> = System.Reactive.Subjects.Subject<'T>

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
            FileViewModel: FileViewModel
        }

    type Model =
        {
            SourceDirectory: ConfiguredDirectory
            DestinationDirectory: ConfiguredDirectory
            FileTypes: string
            ReplacementsFileName: string
            IsActive: bool
            Files: ObservableCollection<FileViewModel>
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
        | UpdateFileStatus of (FileViewModel * int * MoveFileStatus)
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
            Files = ObservableCollection()
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

    let moveFile startCopyOperation (fileViewModel: FileViewModel) destinationDirectory =
        let source = fileViewModel.FullName
        let destinationFileName = Path.GetFileNameWithoutExtension source
        let destination = Path.Combine(destinationDirectory, destinationFileName + shooFileNameExtension)

        {
            Source = source
            Destination = destination
            Extension = Path.GetExtension source
            FileViewModel = fileViewModel
        }
        |> startCopyOperation

    let moveFile2 copyOperation =
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

    let update tryPickFolder (fileViewModels: Subject<CopyOperation>) message model =
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
            let vm = new FileViewModel(path)
            // vm.StartElmishLoop null

            model.Files.Add(vm)
            moveFile fileViewModels.OnNext vm model.DestinationDirectory.Path

            model |> withoutCommand
        | UpdateFileStatus (fileViewModel, progress, moveFileStatus) ->
            fileViewModel.MoveProgress <- progress
            fileViewModel.MoveStatus <- moveFileStatus

            model |> withoutCommand
        // TODO Temporary
        | RemoveFile ->
            if model.Files.Count > 0
            then model.Files.RemoveAt 0

            model |> withoutCommand

    let subscriptions
        (watcher: FileSystemWatcher)
        (copyOperations: Subject<CopyOperation>)
        (model: Model)
        : Sub<Message> =
        let watchFileSystem dispatch =
            let subscription =
                watcher.Renamed
                |> Observable.subscribe (_.FullPath >> AddFile >> dispatch)

            watcher.Path <- model.SourceDirectory.Path
            watcher.EnableRaisingEvents <- true

            {
                new IDisposable with
                    member _.Dispose() =
                        watcher.EnableRaisingEvents <- false
                        subscription.Dispose()
            }

        let copyFile dispatch =
            let subscription =
                copyOperations
                |> Observable.subscribe (moveFile2 >> UpdateFileStatus >> dispatch)

            subscription

        [
            if model.IsActive then
                yield!
                    [
                        [ nameof watchFileSystem ], watchFileSystem
                        [ nameof copyFile ], copyFile
                    ]
        ]

open Main
open Shoo

type MainWindowViewModel(folderPicker: Services.FolderPickerService) as this =
    inherit ReactiveElmishViewModel()

    let watcher = new FileSystemWatcher(EnableRaisingEvents = false)
    let copyOperations = new System.Reactive.Subjects.Subject<CopyOperation>()

    let store = 
        Program.mkAvaloniaProgram init (update folderPicker.TryPickFolder copyOperations)
        |> Program.withSubscription (subscriptions watcher copyOperations)
        |> Program.withErrorHandler (fun (_, ex) -> printfn "Error: %s" ex.Message)
        |> Program.withConsoleTrace
        //|> Program.terminateOnViewUnloaded this Terminate
        |> Program.mkStore


    do this.AddDisposable watcher
       this.AddDisposable copyOperations

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

    member this.Files = this.Bind(store, _.Files)

    member this.SelectSourceDirectory() = store.Dispatch(SelectSourceDirectory)
    member this.SelectDestinationDirectory() = store.Dispatch(SelectDestinationDirectory)

    static member DesignVM =
        new MainWindowViewModel(Design.stub)