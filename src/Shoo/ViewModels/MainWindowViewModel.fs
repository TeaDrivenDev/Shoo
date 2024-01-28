namespace Shoo.ViewModels

open System

open ReactiveElmish
open ReactiveUI

open Shoo
open Shoo.Domain

open App

type FileViewModel(file: File) =
    inherit ReactiveElmishViewModel()

    let mutable progress = file.Progress
    let mutable status = file.Status

    member this.FullName = file.FullName
    member this.FileName = file.FileName
    member this.FileSize = file.FileSize
    member this.Time = file.Time

    member this.Progress
        with get () = progress
        and set value =
            this.RaiseAndSetIfChanged(&progress, value) |> ignore

    member this.Status
        with get () = status
        and set value =
            this.RaiseAndSetIfChanged(&status, value) |> ignore

    member this.RemoveFile() = store.Dispatch (RemoveFile this.FullName)

type MainWindowViewModel(folderPicker: Services.FolderPickerService) as this =
    inherit ReactiveElmishViewModel()

    let progress = Progress(UpdateFileStatus >> store.Dispatch)

    let copyEngine = CopyFileEngine.create progress
    do this.AddDisposable copyEngine

    member this.SourceDirectory
        with get () = this.Bind(store, _.SourceDirectory.Path)
        and set value = store.Dispatch(UpdateSourceDirectory (Some value))

    member this.DestinationDirectory
        with get () = this.Bind(store, _.DestinationDirectory.Path)
        and set value = store.Dispatch(UpdateDestinationDirectory (Some value))

    member this.IsSourceDirectoryValid = this.Bind(store, _.SourceDirectory.PathExists)
    member this.IsDestinationDirectoryValid = this.Bind(store, _.DestinationDirectory.PathExists)
    member this.ReplacementsFileName = this.Bind(store, _.ReplacementsFileName)

    member this.FileTypes
        with get () = this.Bind(store, _.FileTypes)
        and set value = store.Dispatch(UpdateFileTypes value)

    member this.CanActivate =
        this.Bind(
            store,
            fun model ->
                model.SourceDirectory.PathExists
                && model.DestinationDirectory.PathExists
                && model.DestinationDirectory.Path <> model.SourceDirectory.Path)

    member this.IsActive
        with get () = this.Bind(store, _.IsActive)
        and set value = store.Dispatch(ChangeActive value)

    member this.CanClearCompletedFiles =
        this.Bind(
            store,
            fun model ->
                model.FileQueue
                |> Map.toSeq
                |> Seq.map snd
                |> Seq.exists (fun file -> file.Status = Complete))

    member this.FileQueue = 
        this.BindKeyedList(
            store
            , _.FileQueue
            , map = 
                fun file ->
                    file |> mkCopyOperation |> copyEngine.Queue
                    new FileViewModel(file)
            , getKey = _.FullName
            , update = 
                fun file viewModel ->
                    viewModel.Progress <- file.Progress
                    viewModel.Status <- file.Status
            , sortBy = _.Time
        )

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

    member this.ClearCompletedFiles () = 
        store.Dispatch ClearCompleted

    static member DesignVM = 
        new MainWindowViewModel(Design.stub)
