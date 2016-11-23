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
open HeartbeatListener
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
/// StreamCredentials
///
type StreamCredentials = {
    username : string;
    cstToken : string;
    xSecurityToken : string;
    apiKey : string;
    lsHost : string
}

///
/// IgStreamClient
///
type IgStreamClient(streamCredentials : StreamCredentials, subscriptions : list<Subscription>, lsClient : LSClient) as this = 
    
    let streamCredentials = streamCredentials 
    let lsClient = lsClient        

    do
        this.TryConnectAndSubscribe(streamCredentials, subscriptions) |> ignore

    interface IConnectionListener with
       
        override this.OnDataError(e : PushServerException) = ()
        override this.OnFailure(e : PushConnException) = ()
        override this.OnFailure(e : PushServerException) =
            Console.WriteLine(e.Message)
            this.TryConnectAndSubscribe(streamCredentials, subscriptions) |> ignore

        override this.OnEnd(e : int) = ()
        override this.OnConnectionEstablished() = ()
        override this.OnSessionStarted(isPolling : bool) = ()
        override this.OnNewBytes(bytes : int64) = ()
        override this.OnActivityWarning(warning : bool) = ()
        override this.OnClose() =
             lsClient.CloseConnection()
    
    ///
    /// TryConnectAndSubscribe
    ///
    member this.TryConnectAndSubscribe(streamCredentials : StreamCredentials, subscriptions : list<Subscription>) = 
        if this.Connect(streamCredentials) then
            this.SubscribeToMarketDetails(subscriptions)
            this.SubscribeToHeartbeat()
            Console.WriteLine("Connected!")
        else
            Console.WriteLine("failed to connect.")
      
    ///
    /// Connect
    ///    
    member this.Connect(streamCredentials : StreamCredentials) : bool =
        let connectionInfo = ConnectionInfo()
        connectionInfo.Adapter <- "DEFAULT"
        connectionInfo.User <- streamCredentials.username
        connectionInfo.Password <- String.Format("CST-{0}|XST-{1}", streamCredentials.cstToken, streamCredentials.xSecurityToken)
        connectionInfo.PushServerUrl <- streamCredentials.lsHost
        connectionInfo.StreamingTimeoutMillis <- 15000L
        connectionInfo.ProbeTimeoutMillis <- 15000L
        connectionInfo.ReconnectionTimeoutMillis <- 15000L

        try
            lsClient.CloseConnection()
            lsClient.OpenConnection(connectionInfo, this) 
            true
        with
        | _ as e -> Console.WriteLine(e.Message)
                    false
    
    ///
    /// SubscribeToMarketDetails
    ///
    member this.SubscribeToMarketDetails(subscriptions : list<Subscription>) = 
        for subscription in subscriptions do
            let epic = String.Format("L1:{0}", subscription.marketDetails.epic)
            let fields = ["MID_OPEN"; "HIGH"; "LOW"; "CHANGE"; "CHANGE_PCT"; "UPDATE_TIME"; "MARKET_DELAY"; "MARKET_STATE"; "BID"; "OFFER"] 
            let extTableInfo = ExtendedTableInfo([|epic|], "MERGE", fields |> List.toArray, true)
            lsClient.SubscribeTable(extTableInfo, subscription.listener, false) |> ignore

    ///
    /// SubscribeToHeartbeat
    ///
    member this.SubscribeToHeartbeat() = 
        let extTableInfo = ExtendedTableInfo([|"TRADE:HB.U.HEARTBEAT.IP"|], "MERGE", [|"HEARTBEAT"|], true)
        lsClient.SubscribeTable(extTableInfo, HeartbeatListener(), false) |> ignore

    ///
    /// Disconnect
    /// 
    member this.Disconnect() = 
        lsClient.CloseConnection() |> ignore
        

        


    

        

    

