module GlobalState 
open System
open Android.Graphics

let srvcRunningEvent = Event<bool>()
let srvcRunningSub  = srvcRunningEvent.Publish

let sensorImageEvent = Event<Bitmap>()
let sensorImageSub  = sensorImageEvent.Publish

let houghImageEvent = Event<Bitmap>()
let houghImageSub  = houghImageEvent.Publish

let minAmpEvent = Event<float>()
let minAmpSub = minAmpEvent.Publish

let maxAmpEvent = Event<float>()
let maxAmpSub = maxAmpEvent.Publish


//need to be able change these from the UI
let mutable theta = Math.PI/180.0
let mutable rho  = 2.0
let mutable threshold = 50 //peaks
let mutable minLength = 50.
let mutable maxGap = 5.
let mutable senstivity = 5.f