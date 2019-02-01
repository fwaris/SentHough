namespace SentTestHough

open System

open Android.App
open Android.Content.PM
open Android.OS
open Android.Runtime
open Android.Views
open Android.Widget
open Android.Provider
open Android.Support.V4.App
open Android

open System.IO
open Android.Graphics
open Org.Opencv.Core
open Org.Opencv.Android
open Org.Opencv.Imgproc
open Org.Opencv


type Resources = SentTestHough.Resource


[<Activity (Label = "SentTestHough", MainLauncher = false, Icon = "@mipmap/icon",
                 ScreenOrientation=ScreenOrientation.Landscape,
                 ConfigurationChanges=(ConfigChanges.KeyboardHidden ||| ConfigChanges.Orientation))>]
type MainActivity () =
    inherit Activity ()

    let mutable ev:Event<_> = Unchecked.defaultof<_>
    let imageDir = (Environment.GetExternalStoragePublicDirectory (Environment.DirectoryPictures)).Path
    let mutable _openCvCameraView = Unchecked.defaultof<_>

    override this.OnCreate (bundle) =

        base.OnCreate (bundle)
        this.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
        // Set our view from the "main" layout resource
        this.SetContentView (Resources.Layout.Main)

        _openCvCameraView <- this.FindViewById<CameraBridgeViewBase>(Resources.Id.surfaceView);
        _openCvCameraView.Visibility <- ViewStates.Visible;
        _openCvCameraView.SetCvCameraViewListener(this);

    override x.OnDestroy() = 
        base.OnDestroy()
        if _openCvCameraView <> null then
            _openCvCameraView.DisableView()

    override x.OnResume() =
        base.OnResume()
        if OpenCVLoader.InitDebug() then
            (x :> ILoaderCallbackInterface).OnManagerConnected(LoaderCallbackInterface.Success);
        else
            OpenCVLoader.InitAsync(OpenCVLoader.OpencvVersion300, x , x) |> ignore

    member x.EnableCamera() =
        PermissionsSupport.getPermissionsAndDo 
                                (x :> _)
                                &ev
                                [|Manifest.Permission.Camera; Manifest.Permission.WriteExternalStorage|]
                                (fun () -> _openCvCameraView.EnableView())


    interface ILoaderCallbackInterface with
        member x.OnManagerConnected(i) =
           match i with
           | LoaderCallbackInterface.Success -> x.EnableCamera()
           | _                               -> ()
        
        member x.OnPackageInstall(a,b) = ()

    interface ActivityCompat.IOnRequestPermissionsResultCallback  with
        member x.OnRequestPermissionsResult (code,permissions,grantPermissions) = 
            if ev <> Unchecked.defaultof<_> then ev.Trigger(grantPermissions)

    interface CameraBridgeViewBase.ICvCameraViewListener with   
        member x.OnCameraViewStopped() = ()
        member x.OnCameraViewStarted(a,b) = ()
        member x.OnCameraFrame(m) =
            use gray = new Mat()
            use canny = new Mat()
            use hough = new Mat() //hough = {Mat [ 0*1*CV_32FC2,...
            Imgproc.CvtColor(m,gray,Imgproc.ColorBgr2gray)
            Imgproc.Canny(gray,canny,50.0, 200.0, 3,true)
            let theta =  Math.PI/180.0 //angle resolution
            let rho = 2.0 //distance Resolution
            let threshold = 200 //peaks
            Imgproc.HoughLinesP(canny,hough,rho,theta,threshold, 50., 5. )
            //let ln = [|0.0f; 0.0f |]
            let ln = [|0; 0; 0; 0 |]
            use clr = new Scalar(255.,255.,0.)
            for r in 0..(hough.Rows()-1) do 
                let _ = hough.Get(r,0,ln)

                //let r = ln.[0]
                //let t = ln.[1]
                //let a = cos t
                //let b = sin t
                //let x0 = a * r
                //let y0 = b * r
                //use p1 = new Point(x0 + 1000.f * (-b) |> float, y0 + 1000.f*a |> float)
                //use p2 = new Point(x0 - 1000.f * (-b) |> float, y0 - 1000.f*a |> float)

                use p1 = new Point(float ln.[0], float ln.[1])
                use p2 = new Point(float ln.[2], float ln.[3])
                Imgproc.Line(m,p1,p2,clr,3)
            m


    interface IJavaObject with
        member x.Handle = x.Handle