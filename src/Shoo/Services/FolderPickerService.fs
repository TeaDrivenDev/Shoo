namespace Shoo.Services

open System
open Avalonia.Controls
open Avalonia.Platform.Storage

type FolderPickerService(mainWindow: Window) =
    member this.OpenFolderPicker() =
        mainWindow.StorageProvider.OpenFolderPickerAsync(FolderPickerOpenOptions())

    member this.TryPickFolder() =
        task {
            let! files = this.OpenFolderPicker()

            return
                files
                |> Seq.tryHead
                |> Option.map
                    (fun file ->
                        let uri =
                            file.Path.AbsolutePath
                            |> System.Net.WebUtility.UrlDecode
                            |> Uri

                        uri.AbsolutePath)
        }
