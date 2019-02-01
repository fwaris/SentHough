namespace SentTestHough

open System

open Android.App
open Android.Content
open Android.Widget
open Android.Support.V4.App
open Android

[<Activity ( MainLauncher = true, Label = "SensorViewActivity")>]
type SensorViewActivity () =
    inherit Activity()

    let mutable ev:Event<_> = Unchecked.defaultof<_>

    let startStopService (ctx:Context) shouldStart =
        async {
            let intnt  = new Intent(ctx, typeof<SensorService>)
            if shouldStart then intnt |> ctx.StartService |> ignore
            else intnt |> ctx.StopService |> ignore
        }
        |> Async.Start

    override this.OnCreate(bundle) =
        base.OnCreate (bundle)
        this.SetContentView(Resources.Layout.SensorView)

        let serviceSwitch = this.FindViewById<Switch>(Resources.Id.serviceSwitch)
        let imgSensor = this.FindViewById<ImageView>(Resources.Id.imgSensor)
        let imgHough = this.FindViewById<ImageView>(Resources.Id.imgHough)
        let txMinAmp = this.FindViewById<TextView>(Resources.Id.txMinAmp)
        let txMaxAmp = this.FindViewById<TextView>(Resources.Id.txMaxAmp)
        let sensitivityBar = this.FindViewById<SeekBar>(Resources.Id.sensitivityBar)
        let txSensitivity = this.FindViewById<TextView>(Resources.Id.txSensitivity)
        let peaksBar = this.FindViewById<SeekBar>(Resources.Id.peeksBar)
        let txPeaks = this.FindViewById<TextView>(Resources.Id.txPeaks)

 
        let _ = GlobalState.srvcRunningSub.Subscribe(fun running -> serviceSwitch.Checked <- running )

        serviceSwitch.CheckedChange.Add(fun _ -> 
            PermissionsSupport.getPermissionsAndDo 
                                    (this :> _)
                                    &ev
                                    [|Manifest.Permission.Camera; Manifest.Permission.WriteExternalStorage|]
                                    (fun () -> startStopService this serviceSwitch.Checked))

        let _ = GlobalState.sensorImageSub.Subscribe(fun img ->
            imgSensor.SetImageBitmap(img)
            imgSensor.Invalidate()
            )

        let _ = GlobalState.houghImageSub.Subscribe(fun img ->
            imgHough.SetImageBitmap(img)
            imgHough.Invalidate()
            )

        let _ = GlobalState.minAmpSub.Subscribe(fun v -> txMinAmp.Text <- sprintf "%0.2f" v)
        let _ = GlobalState.maxAmpSub.Subscribe(fun v -> txMaxAmp.Text <- sprintf "%0.2f" v)

        txSensitivity.Text <- string GlobalState.senstivity
        sensitivityBar.Progress <- int GlobalState.senstivity
        sensitivityBar.ProgressChanged.Add(fun _ -> 
            txSensitivity.Text <- string sensitivityBar.Progress
            GlobalState.senstivity <- float32 sensitivityBar.Progress)

        txPeaks.Text <- string GlobalState.threshold
        peaksBar.Progress <- GlobalState.threshold
        peaksBar.ProgressChanged.Add(fun _ -> 
            txPeaks.Text <- string peaksBar.Progress
            GlobalState.threshold <-  peaksBar.Progress)

        ()

    interface ActivityCompat.IOnRequestPermissionsResultCallback  with
        member x.OnRequestPermissionsResult (code,permissions,grantPermissions) = 
            if ev <> Unchecked.defaultof<_> then ev.Trigger(grantPermissions)

