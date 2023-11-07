namespace Shoo.ViewModels

open System
open System.IO
open Elmish.Avalonia

module FileViewModel =
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

    let bindings () =
        [
            "FileName" |> Binding.oneWay (fun m -> m.FileName)
            "Time" |> Binding.oneWay (fun m -> m.Time)
            "Size" |> Binding.oneWay (fun m -> m.Size)
            "MoveProgress" |> Binding.oneWay (fun m -> m.MoveProgress)
        ]

    let designVM =
        let model =
            {
                FullName = "AA"
                FileName = "AAA"
                Time = DateTime.Now
                Size = 1234567890L
                MoveProgress = 37
                MoveStatus = Moving
            }

        ViewModel.designInstance model (bindings ())

    let vm path =
        AvaloniaProgram.mkSimple (init path) update bindings
        |> ElmishViewModel.create
        |> ElmishViewModel.terminateOnViewUnloaded Terminate
        :> IElmishViewModel
