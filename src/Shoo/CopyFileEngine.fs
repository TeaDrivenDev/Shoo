namespace Shoo

open System
open System.IO

open Shoo.Domain
open Shoo.Prelude

[<RequireQualifiedAccess>]
module CopyFileEngine =
    type ICopyFileEngine =
        abstract member Queue: copyOperation: CopyOperation -> unit

        inherit IDisposable

    type private WriteActorMessage =
        | Start of CopyOperation
        | Bytes of {| FileName: string; Bytes: ReadOnlyMemory<byte> |}
        | Finish of fileName: string

    type private WriteActorState =
        {
            CopyOperation: CopyOperation
            FileStream: FileStream
            BytesWritten: int64
            StartTime: DateTime
        }

    let private createReadActor (writeActor: MailboxProcessor<_>) =
        let readChunk (fileStream: Stream) =
            async {
                let buffer = Array.zeroCreate Constants.ChunkSize
                let! bytesRead =
                    fileStream.ReadAsync(buffer, 0, Constants.ChunkSize)
                    |> Async.AwaitTask

                return bytesRead, buffer.AsMemory(0, bytesRead)
            }

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
                                    BufferSize = Constants.ChunkSize,
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

                                    let! bytesRead, buffer = readChunk fileStream

                                    return! innerLoop (bytesRead, buffer)
                            }

                        let! bytesRead, buffer = readChunk fileStream

                        do! innerLoop (bytesRead, buffer)

                        return! loop ()
                    }

                loop ())

    // TODO Report progress
    let private createWriteActor (progress: IProgress<_>) =
        let validateCurrentState state name messagePrefix =
            state
            |> Option.map
                (fun state ->
                    if name = state.CopyOperation.Destination
                    then state
                    else failwithf "%s, but active stream is %s" (sprintf messagePrefix name) state.CopyOperation.Destination)
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
                                        (fun state ->
                                            failwithf "Starting new file although %s was not finished" copyOperation.Destination)
                                    |> Option.defaultWith
                                        (fun () ->
                                            new FileStream(
                                                copyOperation.Destination,
                                                FileStreamOptions(
                                                    Access = FileAccess.Write,
                                                    BufferSize = Constants.ChunkSize,
                                                    Mode = FileMode.CreateNew,
                                                    Options = FileOptions.Asynchronous,
                                                    PreallocationSize = copyOperation.FileSize,
                                                    Share = FileShare.None)))

                                progress.Report((copyOperation.Source, 0, Waiting))

                                let state =
                                    {
                                        CopyOperation = copyOperation
                                        FileStream = fileStream
                                        BytesWritten = 0L
                                        StartTime = DateTime.Now
                                    }

                                return! loop (Some state)

                            | Bytes bytes ->
                                let state =
                                    validateCurrentState state bytes.FileName "Received bytes for %s"

                                do! state.FileStream.WriteAsync(bytes.Bytes).AsTask() |> Async.AwaitTask

                                let bytesWritten = state.BytesWritten + int64 bytes.Bytes.Length

                                let progressPercentage =
                                    (float bytesWritten / float state.CopyOperation.FileSize) * 100. |> int

                                progress.Report(
                                    state.CopyOperation.Source,
                                    progressPercentage,
                                    Moving)

                                let newState = { state with BytesWritten = bytesWritten }

                                return! loop (Some newState)

                            | Finish fileName ->
                                let state =
                                    validateCurrentState state fileName "Trying to finish %s"

                                state.FileStream.Close()
                                state.FileStream.Dispose()

                                File.SetLastWriteTimeUtc(
                                    state.CopyOperation.Destination,
                                    state.CopyOperation.Time)

                                let finalDestination, createMode =
                                    getSafeDestinationFileName
                                        state.CopyOperation.Destination
                                        state.CopyOperation.Extension

                                if createMode = Replace
                                then File.Delete finalDestination

                                File.Move(state.CopyOperation.Destination, finalDestination)

                                let moveStatus =
                                    if FileInfo(finalDestination).Length = state.CopyOperation.FileSize
                                    then
                                        File.Delete state.CopyOperation.Source
                                        Complete
                                    else Failed

                                progress.Report((state.CopyOperation.Source, 100, moveStatus))

                                return! loop None
                        with _ -> return! loop None
                    }

                loop None)

    let create progress =
        let writeActor = createWriteActor progress
        let readActor = createReadActor writeActor

        // TODO Handle errors

        {
            new ICopyFileEngine with
                member _.Queue(copyOperation: CopyOperation) = readActor.Post(copyOperation)

                member _.Dispose() = readActor.Dispose(); writeActor.Dispose()
        }
