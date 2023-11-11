namespace Shoo.ViewModels

open System.IO

open ReactiveUI

type MoveFileStatus = Waiting | Moving | Complete | Failed

type FileViewModel(path) =
    inherit ReactiveViewModelBase()

    let mutable moveProgress = 0
    let mutable moveStatus = Waiting

    let fileInfo = FileInfo path

    let fullName = fileInfo.FullName
    let fileName = fileInfo.Name
    let fileSize = fileInfo.Length
    let time = fileInfo.LastWriteTime

    member _.FileName = fileName
    member _.FileSize = fileSize
    member _.Time = time

    member this.MoveProgress
        with get () = moveProgress
        and set value =
            this.RaiseAndSetIfChanged(&moveProgress,  value) |> ignore

    member this.MoveStatus
        with get () = moveStatus
        and set value =
            this.RaiseAndSetIfChanged(&moveStatus, value) |> ignore
