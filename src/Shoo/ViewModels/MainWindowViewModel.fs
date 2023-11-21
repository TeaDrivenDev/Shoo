namespace Shoo.ViewModels

open Elmish.Avalonia
open Shoo
open TeaDrivenDev.Prelude.IO
open App
open System.Collections.ObjectModel

type MainWindowViewModel(folderPicker: Services.FolderPickerService) as this =
    inherit ReactiveElmishViewModel()

    let _fileQueue = ObservableCollection<File>()

    // Sync model FileQueue with the VM FileQueue ObservableCollection.
    do  this.Subscribe(
            store.Observable |> Observable.map (fun m -> m.FileQueue), 
            fun fileQueueMap ->
                _fileQueue // Remove files from ObservableCollection
                |> Seq.filter (fun f -> not (fileQueueMap.ContainsKey f.FullName))
                |> Seq.iter (fun f -> _fileQueue.Remove(f) |> ignore)
                    
                fileQueueMap // Add or update files in ObservableCollection
                |> Map.toSeq
                |> Seq.sortBy (fun (_, file) -> file.FullName)
                |> Seq.iter (fun (_, file) -> 
                    if _fileQueue |> Seq.exists (fun f -> f.FullName = file.FullName) then
                        let index = this.FileQueue |> Seq.findIndex (fun f -> f.FullName = file.FullName)
                        _fileQueue[index] <- file
                    else
                        _fileQueue.Add(file)
                )
            )

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

    member this.FileQueue = _fileQueue

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


    member this.Retry(file: obj) = 
        printfn "Retrying" // TODO: Implement

    static member DesignVM = 
        new MainWindowViewModel(Design.stub)