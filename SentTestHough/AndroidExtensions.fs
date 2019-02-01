module AndroidExtensions
open System
open System.Threading
open Extensions

open Android.App
open Android.Content
open Android.OS

let notifyAsync 
    (context:Context) 
    (uiCtx:SynchronizationContext)
    (title:string)
    (msg:string)
    fOk =
    async {
        try
            let okHndlr = EventHandler<DialogClickEventArgs>(fOk)
            do! Async.SwitchToContext uiCtx
            use builder = new AlertDialog.Builder(context)
            builder.SetTitle(title)                      |> ignore
            builder.SetMessage(msg)                      |> ignore
            builder.SetCancelable(true)                  |> ignore
            builder.SetPositiveButton("OK", okHndlr)     |> ignore
            builder.Show() |> ignore
        with ex -> logE (sprintf  "notifyAsync %s" ex.Message)
    }
    |> Async.Start

let promptAsync 
    (context:Context) 
    (uiCtx:SynchronizationContext)
    (title:string)
    (msg:string)
    fOk
    fCancel =
    async {
        try
            let okHndlr = EventHandler<DialogClickEventArgs>(fOk)
            let cnHndlr = EventHandler<DialogClickEventArgs>(fCancel)
            do! Async.SwitchToContext uiCtx
            use builder = new AlertDialog.Builder(context)
            builder.SetTitle(title)                      |> ignore
            builder.SetMessage(msg)                      |> ignore
            builder.SetCancelable(true)                  |> ignore
            builder.SetPositiveButton("OK", okHndlr)     |> ignore
            builder.SetNegativeButton("Cancel", cnHndlr) |> ignore
            builder.Show() |> ignore 
        with ex -> logE (sprintf "promptAsync %s" ex.Message)
    }
    |> Async.Start


let playVib (sec:int64) =
    try 
        let s = Android.App.Application.Context.GetSystemService(Service.VibratorService) :?> Vibrator
        let eff = VibrationEffect.CreateOneShot(sec, -1)
        s.Vibrate(eff)           
    with ex -> logE (sprintf  "playVib %A"  ex)
