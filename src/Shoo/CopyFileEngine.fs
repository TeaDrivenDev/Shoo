namespace Shoo

open System
open System.IO

open Shoo.Domain
open Shoo.Prelude

[<RequireQualifiedAccess>]
module CopyFileEngine =
    type ICopyFileEngine =
        abstract member Queue: copyOperation: CopyOperation -> unit

    type private WriteActorMessage =
        | Start of CopyOperation
        | Bytes of {| FileName: string; Bytes: ReadOnlyMemory<byte> |}
        | Finish of fileName: string

    let private createReadActor (writeActor: MailboxProcessor<_>) =
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
                                    BufferSize = Constants.BufferSize,
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

                                    let buffer = (Array.zeroCreate Constants.BufferSize).AsMemory()
                                    let! bytesRead =
                                        fileStream.ReadAsync(buffer).AsTask() |> Async.AwaitTask

                                    return! innerLoop (bytesRead, buffer)
                            }

                        let buffer = (Array.zeroCreate Constants.BufferSize).AsMemory()
                        let! bytesRead =
                            fileStream.ReadAsync(buffer).AsTask() |> Async.AwaitTask

                        do! innerLoop (bytesRead, buffer)

                        return! loop ()
                    }

                loop ())

    // TODO Report progress
    let private createWriteActor () =
        let validateCurrentState state name messagePrefix =
            state
            |> Option.map
                (fun (copyOperation, stream) ->
                    if name = copyOperation.Destination
                    then (copyOperation, stream)
                    else failwithf "%s, but active stream is %s" (sprintf messagePrefix name) copyOperation.Destination)
            |> Option.defaultWith
                (fun () -> failwithf "%s, but no stream is active" (sprintf messagePrefix name))

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

        MailboxProcessor<_>.Start(
            fun inbox ->
                let rec loop state =
                    async {
                        let! message = inbox.Receive()

                        try
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
                                                copyOperation.Destination,
                                                FileStreamOptions(
                                                    Access = FileAccess.Write,
                                                    BufferSize = Constants.BufferSize,
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

                                stream.Close()
                                stream.Dispose()

                                File.SetLastWriteTimeUtc(copyOperation.Destination, copyOperation.Time)
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

                                return! loop None
                        with _ -> return! loop None
                    }

                loop None)

    let create () =
        let writeActor = createWriteActor ()
        let readActor = createReadActor writeActor

        // TODO Handle errors

        {
            new ICopyFileEngine with
                member _.Queue(copyOperation: CopyOperation) = readActor.Post(copyOperation)
        }
