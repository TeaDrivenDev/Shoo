namespace Shoo.ViewModels

open Elmish.Avalonia

module MainWindowViewModel =
    type Model =
        {
            Message: string
        }

    type Message = Terminate

    let init () = { Message = "Shoo" }

    let update message model =
        match message with
        | Terminate -> model

    let bindings () =
        [
            "Message" |> Binding.oneWay (fun m -> m.Message)
        ]

    let designVM = ViewModel.designInstance (init()) (bindings())

    let vm =
        AvaloniaProgram.mkSimple init update bindings
        |> ElmishViewModel.create
        |> ElmishViewModel.terminateOnViewUnloaded Terminate
        :> IElmishViewModel
