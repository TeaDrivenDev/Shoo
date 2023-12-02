namespace Shoo.ViewModels

open System
open System.Collections.Generic
open System.Reactive.Linq

open DynamicData

open FSharp.Control.Reactive

open ReactiveElmish

open TeaDrivenDev.Prelude.IO

open Shoo

open App

type FileViewModel(file: File) =
    inherit ReactiveElmishViewModel()

    member this.FullName = file.FullName
    member this.FileName = file.FileName
    member this.FileSize = file.FileSize
    member this.Time = file.Time
    member this.Progress = file.Progress
    member this.Status = file.Status

    member this.RemoveFile() = store.Dispatch (RemoveFile this.FullName)

open System.IO

type WriteActorMessage =
    | Start of CopyOperation
    | Bytes of {| FileName: string; Bytes: ReadOnlyMemory<byte> |}
    | Finish of fileName: string

type MainWindowViewModel(folderPicker: Services.FolderPickerService) =
    inherit ReactiveElmishViewModel()

    let mutable fileQueue = Unchecked.defaultof<_>

    let createFileViewModel (file: App.File) = new FileViewModel(file)

    // TODO Report progress
    let writeActor =
        let validateCurrentState state name messagePrefix =
            state
            |> Option.map
                (fun (copyOperation, stream) ->
                    if name = copyOperation.Destination
                    then (copyOperation, stream)
                    else failwithf "%s, but active stream is %s" (sprintf messagePrefix name) copyOperation.Destination)
            |> Option.defaultWith
                (fun () -> failwithf "%s, but no stream is active" (sprintf messagePrefix name))

        MailboxProcessor<_>.Start(
            fun inbox ->
                let rec loop state =
                    async {
                        let! message = inbox.Receive()

                        match message with
                        | Start copyOperation ->
                            let fileStream =
                                state
                                |> Option.map
                                    (fun (copyOperation, stream) ->
                                        failwithf "Starting new file although %s was not finished" copyOperation.Destination)
                                |> Option.defaultWith
                                    (fun () ->
                                        new FileStream(
                                            copyOperation.Source,
                                            FileStreamOptions(
                                                Access = FileAccess.Write,
                                                BufferSize = bufferSize,
                                                Mode = FileMode.CreateNew,
                                                Options = FileOptions.Asynchronous,
                                                PreallocationSize = copyOperation.FileSize,
                                                Share = FileShare.None)))

                            return! loop (Some (copyOperation, fileStream))

                        | Bytes bytes ->
                            let copyOperation, stream =
                                validateCurrentState state bytes.FileName "Received bytes for %s"

                            do! stream.WriteAsync(bytes.Bytes).AsTask() |> Async.AwaitTask

                            return! loop (Some (copyOperation, stream))

                        | Finish fileName ->
                            let copyOperation, stream =
                                validateCurrentState state fileName "Trying to finish %s"

                            // TODO Rename file

                            stream.Close()
                            stream.Dispose()

                            return! loop None
                    }

                loop None)

    let readActor =
        MailboxProcessor<_>.Start(
            fun inbox ->
                let rec loop () =
                    async {
                        let! message = inbox.Receive()

                        use fileStream =
                            new FileStream(
                                message.Source,
                                FileStreamOptions(
                                    Access = FileAccess.Read,
                                    BufferSize = bufferSize,
                                    Mode = FileMode.Open,
                                    Options =
                                        (FileOptions.Asynchronous ||| FileOptions.SequentialScan),
                                    Share = FileShare.Read))

                        writeActor.Post(Start message)

                        let rec innerLoop (bytesRead, buffer)  =
                            async {
                                match bytesRead with
                                | 0 ->
                                    fileStream.Close()
                                    fileStream.Dispose()

                                    writeActor.Post (Finish message.Destination)
                                | _ ->
                                    writeActor.Post
                                        (Bytes {| FileName = message.Destination; Bytes = buffer |})

                                    let buffer = (Array.zeroCreate bufferSize).AsMemory()
                                    let! bytesRead =
                                        fileStream.ReadAsync(buffer).AsTask() |> Async.AwaitTask

                                    return! innerLoop (bytesRead, buffer)
                            }

                        let buffer = (Array.zeroCreate bufferSize).AsMemory()
                        let! bytesRead =
                            fileStream.ReadAsync(buffer).AsTask() |> Async.AwaitTask

                        do! innerLoop (bytesRead, buffer)

                        return! loop ()
                    }

                loop ())

    do
        writeActor.Error.Add(fun ex -> raise ex)
        readActor.Error.Add(fun ex -> raise ex)

        // TODO Dispose
        let connect = store.Model.FileQueue.Connect()

        connect
            .WhereReasonsAre(ChangeReason.Add)
            .Flatten()
            .Select(fun change -> change.Current)
        |> Observable.subscribe
            (fun file ->
                let copyOperation = mkCopyOperation file

                readActor.Post copyOperation

                ())

        |> ignore

        connect
            .Transform(fun file -> new FileViewModel(file))
            .Sort(Comparer.Create(fun (x: FileViewModel) y -> DateTime.Compare(x.Time, y.Time)))
            .Bind(&fileQueue)
            .Subscribe()
        |> ignore

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

    member this.FileQueue = fileQueue

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

    member this.Remove(file: obj)  =
        file |> unbox |> RemoveFile |> store.Dispatch

    static member DesignVM =
        new MainWindowViewModel(Design.stub)
