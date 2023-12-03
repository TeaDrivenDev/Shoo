namespace Shoo.ViewModels

open System
open System.Collections.Generic
open System.Reactive.Linq

open DynamicData

open FSharp.Control.Reactive

open ReactiveElmish

open Shoo
open Shoo.Domain

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

type MainWindowViewModel(folderPicker: Services.FolderPickerService) =
    inherit ReactiveElmishViewModel()

    let mutable fileQueue = Unchecked.defaultof<_>

    let createFileViewModel (file: Shoo.Domain.File) = new FileViewModel(file)

    do
        let progress = Progress(UpdateFileStatus >> store.Dispatch)

        // TODO Dispose
        let copyEngine = CopyFileEngine.create progress

        let connect = store.Model.FileQueue.Connect()

        // TODO Dispose
        connect
            .WhereReasonsAre(ChangeReason.Add)
            .Flatten()
            .Select(fun change -> change.Current)
        |> Observable.subscribe (mkCopyOperation >> copyEngine.Queue)
        |> ignore

        // TODO Dispose
        connect
            .Transform(fun file -> new FileViewModel(file))
            .Sort(Comparer.Create(fun (x: FileViewModel) y -> DateTime.Compare(x.Time, y.Time)))
            .Bind(&fileQueue)
            .Subscribe()
        |> ignore

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
                model.FileQueue.Items
                |> Seq.exists (fun file -> file.Status = Complete))

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

    member this.ClearCompletedFiles () = store.Dispatch ClearCompleted

    static member DesignVM =
        new MainWindowViewModel(Design.stub)
