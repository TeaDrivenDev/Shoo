namespace Shoo.ViewModels

open System
open System.IO

open Elmish
open Elmish.Avalonia

type MoveFileStatus = Waiting | Moving | Complete | Failed

module File =
    type Model =
        {
            FullName: string
            FileName: string
            Time: DateTime
            FileSize: int64
            MoveProgress: int
            MoveStatus: MoveFileStatus
        }

    type Message =
        | UpdateProgress of int
        | UpdateStatus of MoveFileStatus

    let update message model =
        match message with
        | UpdateProgress progress -> { model with MoveProgress = progress }
        | UpdateStatus status -> { model with MoveStatus = status }

    let init path () =
        let fileInfo = FileInfo path

        {
            FullName = fileInfo.FullName
            FileName = fileInfo.Name
            Time = fileInfo.LastWriteTime
            FileSize = fileInfo.Length
            MoveProgress = 0
            MoveStatus = Waiting
        }

open File

type FileViewModel(path: string) =
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

    member this.MoveProgress
        with get () = this.Bind(store, _.MoveProgress)
        and set value = store.Dispatch (UpdateProgress value)

    member this.MoveStatus
        with get () = this.Bind(store, _.MoveStatus)
        and set value = store.Dispatch (UpdateStatus value)

    static member DesignVM = new FileViewModel(@"c:\hiberfil.sys", MoveProgress = 12, MoveStatus = Failed)