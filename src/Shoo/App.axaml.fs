namespace Shoo

open Avalonia
open Avalonia.Markup.Xaml
open Avalonia.Controls.ApplicationLifetimes

open Shoo.Views

type App() =
    inherit Application()

    override this.Initialize() =
        // Initialize Avalonia controls from NuGet packages:
        let _ = typeof<Avalonia.Controls.DataGrid>

        AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            let view = MainWindowView()
            desktop.MainWindow <- view
            Services.Init view
            ViewModels.MainWindowViewModel.vm().StartElmishLoop(view)
        | _ ->
            // leave this here for design view re-renders
            ()

        base.OnFrameworkInitializationCompleted()
