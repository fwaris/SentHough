namespace SentTestHough

open Android.Content
open Android.Support.V4.App
open Android.App

module PermissionsSupport =
    open Android.Content.PM

    //utility operator for F# implicit conversions 

    let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)

    let havePermissions (permissions:string[],grants:PM.Permission[]) =
        if permissions.Length = grants.Length then
            Array.zip permissions grants
            |> Array.forall(fun (p,x) -> x = Permission.Granted)
        else
            false

    let getPermissionsAndDo (ac:Activity) (ev:byref<Event<_>>) permissions fDo =
        let ev1 = new Event<_>()
        ev <- ev1
        ActivityCompat.RequestPermissions(ac,permissions,0)
        async {
            let! grants = Async.AwaitEvent ev1.Publish
            if havePermissions(permissions,grants) then
                do! Async.SwitchToContext Application.SynchronizationContext
                do fDo()
        }
        |> Async.Start

