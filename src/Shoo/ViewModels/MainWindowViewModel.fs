﻿namespace Shoo.ViewModels

open System
open System.Collections.Generic
open System.Reactive.Linq

open FSharp.Control.Reactive

open DynamicData
open ReactiveElmish
open ReactiveUI

open Shoo
open Shoo.Domain

open App
open Avalonia.Threading

type FileViewModel(file: File) =
    inherit ReactiveElmishViewModel()

    //let mutable progress = file.Progress
    //let mutable status = file.Status

    let id = Guid.NewGuid()

    member this.Id = id
    member this.FullName = file.FullName
    member this.FileName = file.FileName + " " + id.ToString()
    member this.FileSize = file.FileSize
    member this.Time = file.Time
    member this.Progress = file.Progress
    member this.Status = file.Status

    //member this.Progress
    //    with get () = progress
    //    and set value =
    //        //printfn "Progress <- %i" value
    //        this.RaiseAndSetIfChanged(&progress, value) |> ignore

    //member this.Status
    //    with get () = status
    //    and set value =
    //        //printfn "status <- %a" value
    //        this.RaiseAndSetIfChanged(&status, value) |> ignore

    member this.RemoveFile() = store.Dispatch (RemoveFile this.FullName)

type MainWindowViewModel(folderPicker: Services.FolderPickerService) as this =
    inherit ReactiveElmishViewModel()

    let mutable fileQueue = Unchecked.defaultof<_>

    let createFileViewModel (file: Shoo.Domain.File) = new FileViewModel(file)

    do
        let progress = Progress(UpdateFileStatus >> store.Dispatch)

        let copyEngine = CopyFileEngine.create progress
        this.AddDisposable copyEngine

        let connect = store.Model.FileQueue.Connect()

        connect
            .WhereReasonsAre(ChangeReason.Add)
            .Flatten()
            .Select(fun change -> change.Current)
        |> Observable.subscribe (mkCopyOperation >> copyEngine.Queue)
        |> this.AddDisposable

        connect
            .Transform(fun file -> new FileViewModel(file))
            //.TransformWithInlineUpdate(
            //    (fun file -> new FileViewModel(file)),
            //    (fun viewModel file ->
            //        //printfn "%s" (DateTime.Now.ToString())

            //        viewModel.Progress <- file.Progress
            //        viewModel.Status <- file.Status))
            .Sort(Comparer.Create(fun (x: FileViewModel) y -> DateTime.Compare(x.Time, y.Time)))
            .Bind(&fileQueue)
            .DisposeMany()
            .Subscribe()
        |> this.AddDisposable

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

    member this.AddRandomFile() = store.Dispatch AddRandomFile

    member this.UpdateFile() = store.Dispatch UpdateFile

    static member DesignVM =
        new MainWindowViewModel(Design.stub)
