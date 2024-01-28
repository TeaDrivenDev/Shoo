namespace Shoo.ViewModels

open System
open System.IO

open Elmish
open FSharp.Control.Reactive
open ReactiveElmish.Avalonia

open Shoo.Domain

module App =
    let withoutCommand model = model, Cmd.none

    let createConfiguredDirectory path =
        {
            Path = path
            PathExists = not <| String.IsNullOrWhiteSpace path && Directory.Exists path
        }

    let mkCopyOperation (file: File) =
        let source = file.FullName
        let destinationFileName = Path.GetFileNameWithoutExtension source
        let destination =
            Path.Combine(
                file.DestinationDirectory,
                destinationFileName + Constants.ShooFileNameExtension)

        {
            Source = source
            FileSize = file.FileSize
            Time = file.Time
            Destination = destination
            Extension = Path.GetExtension source
            File = file
        }

    let mkFile (path: string) destinationDirectory =
        let fileInfo = FileInfo path

        {
            FullName = fileInfo.FullName
            FileName = fileInfo.Name
            DestinationDirectory = destinationDirectory
            Time = fileInfo.LastWriteTime
            FileSize = fileInfo.Length
            Progress = 0
            Status = Waiting
        }

    type Model =
        {
            SourceDirectory: ConfiguredDirectory
            DestinationDirectory: ConfiguredDirectory
            FileTypes: string
            ReplacementsFileName: string
            IsActive: bool
            FileQueue: Map<string, File>
        }

    type Message =
        | UpdateSourceDirectory of string option
        | UpdateDestinationDirectory of string option
        | UpdateFileTypes of string
        | ChangeActive of bool
        | QueueFileCopy of string
        | UpdateFileStatus of (string * int * MoveFileStatus)
        | RemoveFile of string
        | ClearCompleted
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
            FileQueue = Map.empty
        }
        |> withoutCommand

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
            let file = mkFile path model.DestinationDirectory.Path
            { model with
                FileQueue = model.FileQueue.Add(file.FullName, file)
            } |> withoutCommand
        | UpdateFileStatus (fileName, progress, moveFileStatus) ->
            let file = model.FileQueue[fileName]

            let updatedFile = { file with Progress = progress; Status = moveFileStatus }
            { model with
                FileQueue = model.FileQueue.Add(updatedFile.FullName, updatedFile)
            } |> withoutCommand
        | RemoveFile fileFullName ->
            { model with
                FileQueue = model.FileQueue.Remove fileFullName
            } |> withoutCommand
        | ClearCompleted ->
            let completedFiles =
                model.FileQueue
                |> Map.toSeq
                |> Seq.map snd
                |> Seq.filter (fun file -> file.Status = Complete)
                |> Seq.map _.FullName
                
            // Remove completed files from the queue
            let fileQueue: Map<string, File> = 
                completedFiles 
                |> Seq.fold (fun queue file -> queue.Remove file) model.FileQueue

            { model with FileQueue = fileQueue } |> withoutCommand

        | Terminate -> model |> withoutCommand

    let subscriptions (model: Model) : Sub<Message> =

        /// Watches for file renames and adds them to the file copy queue.
        let watchFileSystemSub dispatch =
            let watcher = new FileSystemWatcher(EnableRaisingEvents = false)
            let subscription =
                watcher.Renamed
                |> Observable.subscribe (_.FullPath >> QueueFileCopy >> dispatch)

            watcher.Path <- model.SourceDirectory.Path

            model.FileTypes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (sprintf "*.%s")
            |> Array.iter watcher.Filters.Add

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
