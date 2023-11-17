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

module MainWindowViewModel =
    [<Literal>]
    let shooFileNameExtension = ".__shoo__"

    [<Literal>]
    let bufferSize = 1024 * 1024

    type CreateMode = Create | Replace

    type MoveOperation =
        {
            Source: string
            Destination: string
            Extension: string
            FileViewModel: FileViewModel
        }

    type Context =
        {
            TryPickFolder: unit -> System.Threading.Tasks.Task<string option>
            QueueMoveOperation: MoveOperation -> unit
            Cleanup: unit -> unit
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
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
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

    let startMoveFile queueMoveOperation (fileViewModel: FileViewModel) destinationDirectory =
        let source = fileViewModel.FullName
        let destinationFileName = Path.GetFileNameWithoutExtension source
        let destination = Path.Combine(destinationDirectory, destinationFileName + shooFileNameExtension)

        {
            Source = source
            Destination = destination
            Extension = Path.GetExtension source
            FileViewModel = fileViewModel
        }
        |> queueMoveOperation

    let completeMoveFile copyOperation =
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

    let update context message model =
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
            model, Cmd.OfTask.perform context.TryPickFolder () UpdateSourceDirectory
        | SelectDestinationDirectory ->
            model, Cmd.OfTask.perform context.TryPickFolder () UpdateDestinationDirectory
        | UpdateFileTypes fileTypes -> { model with FileTypes = fileTypes } |> withoutCommand
        | ChangeActive active -> { model with IsActive = active } |> withoutCommand
        | Terminate ->
            context.Cleanup ()

            model |> withoutCommand
        | AddFile path ->
            let vm = FileViewModel path

            model.Files.Add(vm)
            startMoveFile context.QueueMoveOperation vm model.DestinationDirectory.Path

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

    let bindings () =
        [
            "SourceDirectory" |> Binding.twoWay(_.SourceDirectory.Path, Some >> UpdateSourceDirectory)
            "DestinationDirectory" |> Binding.twoWay(_.DestinationDirectory.Path, Some >> UpdateDestinationDirectory)
            "IsSourceDirectoryValid" |> Binding.oneWay(_.SourceDirectory.PathExists)
            "IsDestinationDirectoryValid" |> Binding.oneWay(_.DestinationDirectory.PathExists)
            "SelectSourceDirectory" |> Binding.cmd SelectSourceDirectory
            "SelectDestinationDirectory" |> Binding.cmd SelectDestinationDirectory
            "FileTypes" |> Binding.twoWay(_.FileTypes, UpdateFileTypes)

            "CanActivate"
            |> Binding.oneWay
                (fun m ->
                    m.SourceDirectory.PathExists
                    && m.DestinationDirectory.PathExists
                    && m.SourceDirectory.Path <> m.DestinationDirectory.Path)

            "IsActive" |> Binding.twoWay(_.IsActive, ChangeActive)
            "Files" |> Binding.oneWay(_.Files)

            // TODO Temporary
            "RemoveFile" |> Binding.cmd RemoveFile
        ]

    let designVM =
        let model, _ = init ()

        let fileViewModel = FileViewModel @"c:\hiberfil.sys"
        fileViewModel.MoveProgress <- 12
        fileViewModel.MoveStatus <- Complete
        model.Files.Add(fileViewModel)

        ViewModel.designInstance model (bindings ())

    let subscriptions
        (watcher: FileSystemWatcher)
        (copyOperations: Subject<MoveOperation>)
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
                |> Observable.subscribe (completeMoveFile >> UpdateFileStatus >> dispatch)

            subscription

        [
            if model.IsActive then
                yield!
                    [
                        [ nameof watchFileSystem ], watchFileSystem
                        [ nameof copyFile ], copyFile
                    ]
        ]

    let vm () =
        let tryPickFolder () =
            let fileProvider = Shoo.Services.Get<Shoo.FolderPickerService>()
            fileProvider.TryPickFolder()

        let watcher = new FileSystemWatcher(EnableRaisingEvents = false)
        let moveOperations = new System.Reactive.Subjects.Subject<MoveOperation>()

        let compositeDisposable = new CompositeDisposable()
        compositeDisposable.Add watcher
        compositeDisposable.Add moveOperations

        let cleanup () = compositeDisposable.Dispose()

        let context =
            {
                TryPickFolder = tryPickFolder
                QueueMoveOperation = moveOperations.OnNext
                Cleanup = cleanup
            }

        AvaloniaProgram.mkProgram init (update context) bindings
        |> AvaloniaProgram.withSubscription (subscriptions watcher moveOperations)
        |> ElmishViewModel.create
        |> ElmishViewModel.terminateOnViewUnloaded Terminate
        :> IElmishViewModel
