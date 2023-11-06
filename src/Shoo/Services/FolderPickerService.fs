namespace Shoo

open System

open Microsoft.Extensions.DependencyInjection

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
                |> Option.map (fun file -> System.Net.WebUtility.UrlDecode file.Path.AbsolutePath)
        }

type Services() =
    static let mutable container : IServiceProvider = null

    static member Container
        with get() = container

    static member Init mainWindow =
        let services = ServiceCollection()
        services.AddSingleton<FolderPickerService>(FolderPickerService(mainWindow)) |> ignore
        container <- services.BuildServiceProvider()

    static member Get<'Svc>() =
        container.GetRequiredService<'Svc>()
