namespace Shoo.ViewModels

open System
open System.IO
open Elmish.Avalonia
open Elmish

module File =
    type MoveFileStatus = Waiting | Moving | Complete | Failed

    type Model =
        {
            FullName: string
            FileName: string
            Time: DateTime
            Size: int64
            MoveProgress: int
            MoveStatus: MoveFileStatus
        }

    type Message =
        | UpdateProgress of int
        | UpdateStatus of MoveFileStatus
        | Terminate

    let update message model =
        match message with
        | UpdateProgress progress -> { model with MoveProgress = progress }
        | UpdateStatus status -> { model with MoveStatus = status }
        | Terminate -> model

    let init path () =
        let fileInfo = FileInfo path

        {
            FullName = fileInfo.FullName
            FileName = fileInfo.Name
            Time = fileInfo.LastWriteTime
            Size = fileInfo.Length
            MoveProgress = 0
            MoveStatus = Waiting
        }

open File

type FileViewModel(path: string) = 
    inherit ReactiveElmishViewModel<Model, Message>(init path ())

    member this.FileName = this.Bind _.FileName
    member this.Time = this.Bind _.Time
    member this.Size = this.Bind _.Size
    member this.MoveProgress = this.Bind _.MoveProgress
        
    override this.StartElmishLoop(view: Avalonia.Controls.Control) = 
        Program.mkAvaloniaSimple (init path) update
        |> Program.withErrorHandler (fun (_, ex) -> printfn "Error: %s" ex.Message)
        |> Program.withConsoleTrace
        |> Program.runView this view

    
    static member DesignVM = new FileViewModel(@"c:/path/file.txt")
