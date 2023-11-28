namespace Shoo

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml

type App() =
    inherit Application()

    override this.Initialize() =
        // Initialize Avalonia controls from NuGet packages:
        let _ = typeof<Avalonia.Controls.DataGrid>

        AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            let appRoot = AppCompositionRoot()
            desktop.MainWindow <- appRoot.GetView<ViewModels.MainWindowViewModel>() :?> Window
        | _ ->
            // leave this here for design view re-renders
            ()

        base.OnFrameworkInitializationCompleted()
