namespace SentTestHough

open Extensions

open Android.App
open Android.OS
open Android.Runtime
open Android.Hardware
open FSM

open Android.Graphics
open Org.Opencv.Core
open Org.Opencv.Android
open Org.Opencv.Imgproc
open Org.Opencv

module ServiceContants =
    let defaultSensors = 
            [|
                //SensorType.Accelerometer; 
                //SensorType.Gyroscope; 
                SensorType.LinearAcceleration
                //SensorType.RotationVector; 
                //SensorType.Gravity
            |]

type WearMsg = {Path:string; Data:byte[]}
type SenEv = {Ts:int64; X:float32; Y:float32; Z:float32}

module EventDetect =
    let ``2 seconds`` =  2000_000_000L
    let ``1 second`` =  1000_000_000L

    let compact acc = 
        let l2 = acc |> List.take (acc.Length / 2)
        let t = l2 |> List.last
        t.Ts,l2

    let rec s_start tg ev = F(s_cont tg ev.Ts [ev],None)
    and s_cont tg startTs acc = function
        | ev when tg ev                            -> F(s_track tg ev.Ts (ev::acc), None)                  //detection triggered
        | ev when ev.Ts - startTs > ``2 seconds``  -> let s,acc = compact acc in F(s_cont tg s acc, None)  //compact collection
        | ev                                       -> F(s_cont tg startTs (ev::acc), None)                 //continue collection
    and s_track tg startTs acc = function
        | ev when ev.Ts - startTs > ``1 second``   -> F(s_start tg, Some (ev::acc))                        //accumulated 1 seconds after trigger
        | ev                                       -> F(s_track tg startTs (ev::acc), None)                //keep accumulating

[<Service>] 
type SensorService() as this = 
    inherit Service()
    let mutable wakeLock:PowerManager.WakeLock = null
    let mutable cts = Unchecked.defaultof<_>
    let mutable sensorAgent : MailboxProcessor<SenEv> = Unchecked.defaultof<_>
    let mutable openCvLoaded = false

    let unregisterAll() =
        let smgr = this.GetSystemService(Service.SensorService) :?> SensorManager
        if smgr <> Unchecked.defaultof<_> then
            smgr.UnregisterListener(this)

    let unregister sensors = 
        let smgr = this.GetSystemService(Service.SensorService) :?> SensorManager
        for snsr in sensors do
            let s = smgr.GetDefaultSensor(snsr)
            smgr.UnregisterListener(this, s) |> ignore

    let register sensors = 
        let smgr = this.GetSystemService(Service.SensorService) :?> SensorManager
        for snsr in sensors do
            let s = smgr.GetDefaultSensor(snsr)
            printfn "registering %A" s
            if not <| smgr.RegisterListener(this, s, SensorDelay.Game) then                 //sometimes it takes multiple tries 
                if not <| smgr.RegisterListener(this, s, SensorDelay.Game) then             //to register sensor
                    if not <| smgr.RegisterListener(this, s, SensorDelay.Game) then
                        printfn "sensor not registered %A" s
                        logE (sprintf "Unable to register sensor %A" s)
 
    let acquireWakeLock() =
        let pm = this.GetSystemService(Service.PowerService) :?> PowerManager
        wakeLock <- pm.NewWakeLock(WakeLockFlags.Partial, "SensorTest")
        wakeLock.Acquire()

    let releaseWakeLock() =
        if wakeLock <> null then
            wakeLock.Release()
            wakeLock.Dispose()
        wakeLock <- null

    let trigger ev = let th = GlobalState.senstivity in abs ev.X > th || abs ev.Y > th || abs ev.Z > th
    
    let ``20 meter/sec/sec`` = 20.0f
    let ``10 meter/sec/sec`` = 10.0f
    let ``5 meter/sec/sec``  = 5.0f

    let scale (sMin,sMax) (vMin,vMax) (v:float) =
        if v < vMin then failwith "out of min range for scaling"
        if v > vMax then failwith "out of max range for scaling"
        (v - vMin) / (vMax - vMin) * (sMax - sMin) + sMin

    let toBitmap (m:Mat) = 
        let b = Bitmap.CreateBitmap(m.Rows(),m.Cols(),Bitmap.Config.Argb8888)
        Utils.MatToBitmap(m,b)
        b

    let black =  new Scalar(0.0)
    let white = new Scalar(255.0)

    let delaySpaceImage evs = 
        let w = 500.0
        let edges = new Mat(int w, int w, CvType.Cv8uc1,black)
        let amp = evs |> Seq.map (fun s ->  s.X*s.X + s.Y*s.Y + s.Z*s.Z |> sqrt)
        let mx = Seq.max amp |> float
        let mn = Seq.min amp |> float

        amp |> Seq.pairwise |> Seq.windowed 2 |> Seq.iter (fun xs ->
            let p1x = scale (0.,w) (mn,mx) (fst xs.[0] |> float)
            let p1y = scale (0.,w) (mn,mx) (snd xs.[0] |> float)
            let p2x = scale (0.,w) (mn,mx) (fst xs.[1] |> float)
            let p2y = scale (0.,w) (mn,mx) (snd xs.[1] |> float)
            use pt1 = new Point(p1x,p1y)
            use pt2 = new Point(p2x,p2y)
            Imgproc.Line(edges, pt1,pt2,white)
        )
        edges, toBitmap edges,mn,mx

    let houghPTx (edges:Mat) =
        let theta       = GlobalState.theta
        let rho         = GlobalState.rho
        let threshold   = GlobalState.threshold
        let l           = GlobalState.minLength
        let g           = GlobalState.maxGap
        use hough = new Mat() //hough = {Mat [ 0*1*CV_32FC2,...
        Imgproc.HoughLinesP(edges,hough,rho,theta,threshold, l,g )
        let ln = [|0; 0; 0; 0 |]
        let e2 = new Mat(edges.Rows(),edges.Cols(),CvType.Cv8uc1,black)
        for r in 0..(hough.Rows()-1) do 
            let _ = hough.Get(r,0,ln)
            use p1 = new Point(float ln.[0], float ln.[1])
            use p2 = new Point(float ln.[2], float ln.[3])
            Imgproc.Line(e2,p1,p2,white,3)
        edges.Release()
        e2

    let houghTx (edges:Mat) =
        let theta       = GlobalState.theta
        let rho         = GlobalState.rho
        let threshold   = GlobalState.threshold
        use hough = new Mat() //hough = {Mat [ 0*1*CV_32FC2,...
        Imgproc.HoughLines(edges,hough,rho,theta,threshold)
        let e2 = new Mat(edges.Rows(),edges.Cols(),CvType.Cv8uc1,black)
        let ln = [|0.0f; 0.0f |]
        for r in 0..(hough.Rows()-1) do 
            let _ = hough.Get(r,0,ln)
            let r = ln.[0]
            let t = ln.[1]
            let a = cos t
            let b = sin t
            let x0 = a * r
            let y0 = b * r
            use p1 = new Point(x0 + 1000.f * (-b) |> float, y0 + 1000.f*a |> float)
            use p2 = new Point(x0 - 1000.f * (-b) |> float, y0 - 1000.f*a |> float)
            Imgproc.Line(e2,p1,p2,white,3)
        edges.Release()
        e2

    let imageAgent = MailboxProcessor.Start(fun inbox ->
        async {
            while true do
                try
                    let! evs = inbox.Receive()
                    let edges,img,mn,mx = delaySpaceImage evs
                    use hough = houghTx edges
                    let houghBmp = toBitmap hough
                    do! Async.SwitchToContext Android.App.Application.SynchronizationContext
                    GlobalState.sensorImageEvent.Trigger(img)
                    GlobalState.houghImageEvent.Trigger(houghBmp)
                    GlobalState.minAmpEvent.Trigger(mn)
                    GlobalState.maxAmpEvent.Trigger(mx)
                with ex -> logE ex.Message
        }
    )
     
    let createSensorAgent () (inbox:MailboxProcessor<SenEv>) =
        let mutable fsm = F(EventDetect.s_start trigger,None)
        let mutable i = 0
        async {
            while true do
                try 
                    let! ev = inbox.Receive()
                    fsm <- evalState fsm ev
                    match fsm with
                    | F(_,Some evs) -> if openCvLoaded then imageAgent.Post evs else logI "opencv not loaded"
                    | _ -> ()
                with ex ->
                    logE ex.Message
         }

   
    override x.OnBind(intent) = null
 
    interface ISensorEventListener with
        member x.OnAccuracyChanged(s,se) =
            let a = 1;
            ()
        member x.OnSensorChanged(ev) = 
            if sensorAgent <> Unchecked.defaultof<_> then
                sensorAgent.Post {Ts=ev.Timestamp; X=ev.Values.[0]; Y=ev.Values.[1]; Z=ev.Values.[2]}
            //let tes = {Snsr=int ev.Sensor.Type; Ticks=ev.Timestamp; X=ev.Values.[0]; Y=ev.Values.[1];Z=ev.Values.[2]}
            //let (F(nextState,maybeEvent)) =  evalState currentState tes
            //currentState <- F(nextState,None)
            //match maybeEvent with 
            //| Some e -> processEvent e 
            //| None -> ()

    override x.OnCreate() = 
        base.OnCreate()

    override x.OnStartCommand(intnt,flags,id) =
        try
            acquireWakeLock()
            cts <- new System.Threading.CancellationTokenSource()
            sensorAgent <- MailboxProcessor.Start(createSensorAgent(),cts.Token)
            register ServiceContants.defaultSensors
            GlobalState.srvcRunningEvent.Trigger(true)
            AndroidExtensions.playVib (100L)
            logI "service started"
            if OpenCVLoader.InitDebug() then
               (x :> ILoaderCallbackInterface).OnManagerConnected(LoaderCallbackInterface.Success);
            else
                OpenCVLoader.InitAsync(OpenCVLoader.OpencvVersion300, x , x) |> ignore

        with ex ->
            logE ex.Message
        StartCommandResult.NotSticky 

                           
    override x.OnDestroy() =
        try 
            unregisterAll() 
            releaseWakeLock()
            if cts <> Unchecked.defaultof<_> then cts.Cancel()
            GlobalState.srvcRunningEvent.Trigger(false)
            sensorAgent <- Unchecked.defaultof<_>
            AndroidExtensions.playVib (100L)
            logI "service closed"
        with ex ->
            logE (sprintf "error stopping service %s" ex.Message)
        base.OnDestroy()

            
    interface ILoaderCallbackInterface with
        member x.OnManagerConnected(i) =
           match i with
           | LoaderCallbackInterface.Success -> openCvLoaded <- true
           | _                               -> ()
        member x.OnPackageInstall(a,b) = ()

    interface IJavaObject with
        member x.Handle = x.Handle