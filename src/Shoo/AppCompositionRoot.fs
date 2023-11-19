namespace Shoo

open System
open Elmish.Avalonia
open Microsoft.Extensions.DependencyInjection
open Shoo.ViewModels
open Shoo.Views

type AppCompositionRoot() = 
    inherit CompositionRoot()

    let mainView = MainWindowView()

    override this.RegisterServices(services) = 
        services.AddSingleton<Services.FolderPickerService>(Services.FolderPickerService(mainView))

    override this.RegisterViews() = 
        Map [
            VM.Create<MainWindowViewModel>(), View.Singleton(mainView)
        ]
