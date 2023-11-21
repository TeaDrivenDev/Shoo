namespace Shoo

open Microsoft.Extensions.DependencyInjection

open Elmish.Avalonia

open Shoo.ViewModels
open Shoo.Views

type AppCompositionRoot() =
    inherit CompositionRoot()

    let mainView = MainWindowView()

    override this.RegisterServices(services) =
        services.AddSingleton<Services.FolderPickerService>(Services.FolderPickerService(mainView))

    override this.RegisterViews() =
        Map [
            VM.Key<MainWindowViewModel>(), View.Singleton(mainView)
        ]
