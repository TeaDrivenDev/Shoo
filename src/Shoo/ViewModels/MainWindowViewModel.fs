namespace Shoo.ViewModels

open System
open System.Collections.Generic
open System.Reactive.Linq

open DynamicData
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

type MainWindowViewModel(folderPicker: Services.FolderPickerService) =
    inherit ReactiveElmishViewModel()

    let mutable fileQueue = Unchecked.defaultof<_>

    let createFileViewModel (file: File) = new FileViewModel(file)

    do
        store.Model.FileQueue.Connect()
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
