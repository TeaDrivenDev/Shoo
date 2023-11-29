namespace Shoo.ViewModels

open System
open System.IO

open DynamicData
open Elmish
open FSharp.Control.Reactive
open ReactiveElmish
open ReactiveElmish.Avalonia

open TeaDrivenDev.Prelude
open TeaDrivenDev.Prelude.IO

type MoveFileStatus = Waiting | Moving | Complete | Failed

module App =

    [<Literal>]
    let shooFileNameExtension = ".__shoo__"

    [<Literal>]
    let bufferSize = 1024 * 1024

    type CreateMode = Create | Replace

    type File =
        {
            FullName: string
            FileName: string
            Time: DateTime
            FileSize: int64
            Progress: int
            Status: MoveFileStatus
        }

    let mkFile (path: string) =
        let fileInfo = FileInfo path

        {
            FullName = fileInfo.FullName
            FileName = fileInfo.Name
            Time = fileInfo.LastWriteTime
            FileSize = fileInfo.Length
            Progress = 0
            Status = Waiting
        }

    type CopyOperation =
        {
            Source: string
            Destination: string
            Extension: string
            File: File
        }

    let mkCopyOperation (file: File) destinationDirectory =
        let source = file.FullName
        let destinationFileName = Path.GetFileNameWithoutExtension source
        let destination =
            Path.Combine(destinationDirectory, destinationFileName + shooFileNameExtension)

        {
            Source = source
            Destination = destination
            Extension = Path.GetExtension source
            File = file
        }

    type Model =
        {
            SourceDirectory: ConfiguredDirectory
            DestinationDirectory: ConfiguredDirectory
            FileTypes: string
            ReplacementsFileName: string
            IsActive: bool
            FileQueue: SourceCache<File, string>
        }

    type Message =
        | UpdateSourceDirectory of string option
        | UpdateDestinationDirectory of string option
        | UpdateFileTypes of string
        | ChangeActive of bool
        | QueueFileCopy of string
        | UpdateFileStatus of (File * int * MoveFileStatus)
        | RemoveFile of File
        | Terminate

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
            FileQueue = SourceCache.create _.FullName
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

        copyOperation.File, 100, moveStatus

    let update message model =
        match message with
        | UpdateSourceDirectory value ->
            value
            |> Option.map
                (fun path -> { model with SourceDirectory = createConfiguredDirectory path })
            |> Option.defaultValue model
            |> withoutCommand
        | UpdateDestinationDirectory value ->
            value
            |> Option.map
                (fun path -> { model with DestinationDirectory = createConfiguredDirectory path})
            |> Option.defaultValue model
            |> withoutCommand
        | UpdateFileTypes fileTypes -> { model with FileTypes = fileTypes } |> withoutCommand
        | ChangeActive active -> { model with IsActive = active } |> withoutCommand
        | QueueFileCopy path ->
            let file = mkFile path
            let operation = mkCopyOperation file model.DestinationDirectory.Path
            { model with
                FileQueue = model.FileQueue |> SourceCache.addOrUpdate file
            }, Cmd.OfFunc.perform copyFile operation UpdateFileStatus
        | UpdateFileStatus (file, progress, moveFileStatus) ->
            let updatedFile = { file with Progress = progress; Status = moveFileStatus }
            { model with
                FileQueue = model.FileQueue |> SourceCache.addOrUpdate updatedFile
            } |> withoutCommand
        | RemoveFile file ->
            { model with
                FileQueue = model.FileQueue |> SourceCache.removeKey file.FullName
            } |> withoutCommand
        | Terminate -> model |> withoutCommand

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

        [
            if model.IsActive then
                [ nameof watchFileSystemSub ], watchFileSystemSub
        ]

    let store =
        Program.mkAvaloniaProgram init update
        |> Program.withSubscription subscriptions
        |> Program.withErrorHandler (fun (_, ex) -> printfn $"Error: %s{ex.Message}")
        |> Program.withConsoleTrace
        |> Program.mkStore
