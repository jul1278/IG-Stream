module HeartbeatListener

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
/// Heartbeat listener
///
type HeartbeatListener() = 
    inherit TableListenerAdapterBase()
    override this.OnUpdate(itemPos : int, itemName : string, update : IUpdateInfo) = 
        let epochSec = update.GetNewValue("HEARTBEAT") |> Convert.ToDouble 
        let dateTime = DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(epochSec)
        Console.WriteLine(String.Format("IG HEARTBEAT: {0}", dateTime.ToString("yyyy-MM-dd HH:mm:ss")))
        
        

