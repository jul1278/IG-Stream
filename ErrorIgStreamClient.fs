module ErrorIgStreamClient

open System
open System.Threading
open System.Collections.Concurrent
open System.Collections.Generic
open System.IO
open IGWebApiClient;
open IGWebApiClient.Common;
open dto.endpoint.auth.session.v2
open dto.endpoint.positions.get.otc.v2
open dto.endpoint.marketdetails.v2
open dto.endpoint.search
open ApiCredentials
open Lightstreamer.DotNet.Client

///
/// A subscription
///
type Subscription = {
    marketDetails : Market;
    filePath : string;
    listener : TableListenerAdapterBase
}

///
/// ErrorIgStreamClient
///
type ErrorIgStreamClient(subscriptions : list<Subscription>) as this = 
    inherit IGStreamingApiClient()

    let onFailureEvent = new Event<PushServerException>()
    let mutable tableKeys = [new SubscribedTableKey()]

    [<CLIEvent>]
    member this.OnFailureEvent = onFailureEvent.Publish

    override this.OnDataError(e : PushServerException) = Console.WriteLine(e.Message)
    override this.OnFailure(e : PushConnException) = ()
    override this.OnFailure(e : PushServerException) =  
        Console.WriteLine(e.Message)
        Console.WriteLine("Attempting to resubscribe...")
        this.ReSubscribe |> ignore

    override this.OnEnd(e : int) = Console.WriteLine("END : " + e.ToString())

    ///
    /// Actually connect to everything
    ///
    override this.OnConnectionEstablished() = 
        try
            tableKeys <- subscriptions |> List.map(fun s -> this.SubscribeToMarketDetails([s.marketDetails.epic], s.listener)) 
        with
        | _ as e -> Console.WriteLine(e.Message)

    ///
    /// Unsub and resub to everything
    /// 
    member this.ReSubscribe() = 
        tableKeys |> List.iter(this.UnsubscribeTableKey)
        this.OnConnectionEstablished()
        


    

        

    

