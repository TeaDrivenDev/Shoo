namespace Shoo.ViewModels

open System
open System.IO

open Elmish
open Elmish.Avalonia

module private FileOperation =
    type Model =
        {
            FullName: string
            FileName: string
            Time: DateTime
            FileSize: int64
            Progress: int
            Status: MoveFileStatus
        }

    type Message =
        | UpdateProgress of int
        | UpdateStatus of MoveFileStatus

    let update message model =
        match message with
        | UpdateProgress progress -> { model with Progress = progress }
        | UpdateStatus status -> { model with Status = status }

    let init path () =
        let fileInfo = FileInfo path

        {
            FullName = fileInfo.FullName
            FileName = fileInfo.Name
            Time = fileInfo.LastWriteTime
            FileSize = fileInfo.Length
            Progress = 0
            Status = Waiting
        }

type FileOperationExternalMessage =
    | Remove
    | Retry

open FileOperation

type FileOperationViewModel(path: string) =
    inherit ReactiveElmishViewModel()

    let store =
        Program.mkAvaloniaSimple (init path) update
        |> Program.withErrorHandler (fun (_, ex) -> printfn "Error: %s" ex.Message)
        |> Program.withConsoleTrace
        |> Program.mkStore

    member this.FullName = this.Bind(store, _.FullName)
    member this.FileName = this.Bind(store, _.FileName)
    member this.Time = this.Bind(store, _.Time)
    member this.FileSize = this.Bind(store, _.FileSize)

    member this.Progress
        with get () = this.Bind(store, _.Progress)
        and set value = store.Dispatch (UpdateProgress value)

    member this.Status
        with get () = this.Bind(store, _.Status)
        and set value = store.Dispatch (UpdateStatus value)

    static member DesignVM =
        new FileOperationViewModel(@"c:\hiberfil.sys", Progress = 12, Status = Failed)