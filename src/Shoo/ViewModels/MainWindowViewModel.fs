namespace Shoo.ViewModels

open System
open System.Collections.ObjectModel
open System.IO
open FSharp.Control.Reactive
open Elmish
open Elmish.Avalonia
open TeaDrivenDev.Prelude
open TeaDrivenDev.Prelude.IO

module MainWindow =
    type File = { Name: string }

    type Model =
        {
            SourceDirectory: ConfiguredDirectory
            DestinationDirectory: ConfiguredDirectory
            FileTypes: string
            ReplacementsFileName: string
            IsActive: bool
            Files: ObservableCollection<FileViewModel>
        }

    type Message =
        | UpdateSourceDirectory of string option
        | UpdateDestinationDirectory of string option
        | SelectSourceDirectory
        | SelectDestinationDirectory
        | UpdateFileTypes of string
        | ChangeActive of bool
        | Terminate
        | AddFile of string
        // TODO Temporary
        | RemoveFile

    let init () =
        {
            SourceDirectory = createConfiguredDirectory @"C:\Users\jmarr\AppData\Local\Temp\Shoo\A"
            DestinationDirectory = createConfiguredDirectory @"C:\Users\jmarr\AppData\Local\Temp\Shoo\B"
            FileTypes = "*.png"
            ReplacementsFileName = ""
            IsActive = false
            Files = ObservableCollection()
        }
        |> withoutCommand

    let update tryPickFolder message model =
        match message with
        | UpdateSourceDirectory value ->
            value
            |> Option.map
                (fun path ->
                    {
                        model with
                            SourceDirectory = createConfiguredDirectory path
                    })
            |> Option.defaultValue model
            |> withoutCommand
        | UpdateDestinationDirectory value ->
            value
            |> Option.map
                (fun path ->
                    {
                        model with
                            DestinationDirectory = createConfiguredDirectory path
                    })
            |> Option.defaultValue model
            |> withoutCommand
        | SelectSourceDirectory ->
            model, Cmd.OfTask.perform tryPickFolder () UpdateSourceDirectory
        | SelectDestinationDirectory ->
            model, Cmd.OfTask.perform tryPickFolder () UpdateDestinationDirectory
        | UpdateFileTypes fileTypes -> { model with FileTypes = fileTypes } |> withoutCommand
        | ChangeActive active -> { model with IsActive = active } |> withoutCommand
        | Terminate -> model |> withoutCommand
        | AddFile path ->
            let vm = new FileViewModel(path)
            model.Files.Add(vm)
            model |> withoutCommand
        // TODO Temporary
        | RemoveFile ->
            if model.Files.Count > 0
            then model.Files.RemoveAt 0

            model |> withoutCommand

    let subscriptions (watcher: FileSystemWatcher) (model: Model) : Sub<Message> =
        let watchFileSystem dispatch =
            let subscription =
                watcher.Renamed
                |> Observable.subscribe (fun e -> e.FullPath |> AddFile |> dispatch)

            watcher.Path <- model.SourceDirectory.Path
            watcher.EnableRaisingEvents <- true

            {
                new IDisposable with
                    member _.Dispose() =
                        watcher.EnableRaisingEvents <- false
                        subscription.Dispose()
            }

        [
            if model.IsActive then [ nameof watchFileSystem ], watchFileSystem
        ]

open MainWindow

type MainWindowViewModel() = 
    inherit ReactiveElmishViewModel<Model, Message>(init() |> fst)

    let tryPickFolder () =
        let fileProvider = Shoo.Services.Get<Shoo.FolderPickerService>()
        fileProvider.TryPickFolder()

    let watcher = new FileSystemWatcher(EnableRaisingEvents = false)

    member this.SourceDirectory = this.BindModel(fun m -> m.SourceDirectory)
    member this.DestinationDirectory = this.BindModel(fun m -> m.DestinationDirectory)
    member this.IsSourceDirectoryValid = this.BindModel(nameof this.IsSourceDirectoryValid, fun m -> m.SourceDirectory.PathExists)
    member this.IsDestinationDirectoryValid = this.BindModel(nameof this.IsDestinationDirectoryValid, fun m -> m.DestinationDirectory.PathExists)
    member this.ReplacementsFileName = this.BindModel(fun m -> m.ReplacementsFileName)
    member this.FileTypes 
        with get () = this.BindModel(fun m -> m.FileTypes)
        and set value = this.Dispatch(UpdateFileTypes value)
        
    member this.CanActivate = this.BindModel(nameof this.CanActivate, fun m -> m.SourceDirectory.PathExists && m.DestinationDirectory.PathExists)
    member this.IsActive 
        with get () = this.BindModel(fun m -> m.IsActive)
        and set value = this.Dispatch(ChangeActive value)
        
    member this.Files = this.BindModel(fun m -> m.Files)

    member this.SelectSourceDirectory() = this.Dispatch(SelectSourceDirectory)
    member this.SelectDestinationDirectory() = this.Dispatch(SelectDestinationDirectory)

    override this.StartElmishLoop(view: Avalonia.Controls.Control) = 
        Program.mkAvaloniaProgram init (update tryPickFolder)
        |> Program.withSubscription (subscriptions watcher)
        |> Program.terminateOnViewUnloaded this Terminate
        |> Program.withErrorHandler (fun (_, ex) -> printfn "Error: %s" ex.Message)
        |> Program.withConsoleTrace
        |> Program.runView this view

    static member DesignVM = new MainWindowViewModel()
