// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

module Program

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
open ErrorIgStreamClient
open HeartbeatListener
open Lightstreamer.DotNet.Client

/// <summary>
/// MarketPosition
/// </summary>
type MarketPosition = { 
    updates : Dictionary<string, seq<L1LsPriceData>>; 
    positions : PositionsResponse; 
    igClient : IgRestApiClient;
    authentication : AuthenticationResponse 
    }

/// <summary>
/// some ig plumbing
/// </summary>
type EventDispatcher() =
 interface PropertyEventDispatcher with
    override this.BeginInvoke(a : Action) = Console.WriteLine(a.ToString()) |> ignore
    override this.addEventMessage(message : string) = Console.WriteLine(message) |> ignore

/// <summary>
/// pretty print out update data with arrows and stuff
/// </summary>
let prettyPrint (update : L1LsPriceData, updateInfo : IUpdateInfo) = 
            let ask = Convert.ToDecimal(updateInfo.GetOldValue("OFFER")), Convert.ToDecimal(updateInfo.GetNewValue("OFFER"))
            let prevBid = Convert.ToDecimal(updateInfo.GetOldValue("BID"))

            Console.OutputEncoding <- System.Text.Encoding.Unicode
            Console.Write("TIME: {0} - ITEM: {1} - BID: {2} - ASK: {3} ", update.UpdateTime, updateInfo.ItemName, update.Bid, snd ask)

            if update.Bid.HasValue && (update.Bid.Value > prevBid) then
                Console.ForegroundColor <- ConsoleColor.Green
                Console.Write("▲")
            elif update.Bid.HasValue && (update.Bid.Value < prevBid) then
                Console.ForegroundColor <- ConsoleColor.Red
                Console.Write("▼")
            else
                Console.Write("-")

            Console.ForegroundColor <- ConsoleColor.White
            Console.Write(Environment.NewLine)

/// <summary>
/// Print out a seq of positions 
/// </summary>
let printPositions (positions : seq<OpenPosition>) = 
    for position in positions do
        let print = String.Format("name: {0} size: {1} price: {2} direction: {3} bid: {4} offer: {5}", 
                        position.market.instrumentName, 
                        position.position.size, 
                        position.position.level,
                        position.position.direction,
                        position.market.bid,
                        position.market.offer) 
        Console.WriteLine(print)
        
/// <summary>
/// calculate the margin required from a position
/// </summary>  
let marginRequired (position : OpenPositionData, market : MarketDetailsResponse) : decimal = 
    match market.instrument.``type`` with
    | "COMMODITIES" -> position.level.Value * position.size.Value * position.contractSize.Value * market.instrument.marginFactor.Value
    | _ -> 0M

/// <summary>
/// Calculate profit (or loss)
/// </summary>
let simpleProfit (position : OpenPositionData) (startPrice : decimal) (endPrice : decimal) : decimal = (endPrice - startPrice) * position.contractSize.Value 

/// <summary>
/// log on to ig market rest api
/// </summary>
let igLogOn : MarketPosition = 
    let igClient = new IgRestApiClient("demo", EventDispatcher())
    let authRequest = AuthenticationRequest(encryptedPassword = true, identifier = credentials.Username, password = credentials.Password)
    let response = igClient.SecureAuthenticate(ar = authRequest, apiKey = credentials.ApiKey) 
    
    response.Wait()
    let result = response.Result
    let currentPositions = igClient.getOTCOpenPositionsV2().Result.Response
    {updates = Dictionary<string, seq<L1LsPriceData>>(); positions = currentPositions; igClient = igClient; authentication = result.Response}

/// <summary>
/// search for markets
/// </summary>
let searchMarketEpics (markets : string[], igClient : IgRestApiClient) : seq<Market> = 
    seq {for market in markets do 
            let task = igClient.searchMarket(market) 
            task.Wait()
            yield task.Result.Response.markets |> Seq.head}

///
/// Write out the update to the mailbox
///
let onMarketUpdate (csvWriterAgent : MailboxProcessor<UpdateArgs<L1LsPriceData>>) (update : UpdateArgs<L1LsPriceData>) = csvWriterAgent.Post(update)

///
/// Write out to csv
///
let writeToCsv (filePath : string) (update : UpdateArgs<L1LsPriceData>) = 
    if update.UpdateData.Bid.HasValue && update.UpdateData.Offer.HasValue then 
        let contents = String.Format("{0} {1}, {2}, {3}", 
                                DateTime.UtcNow.ToString("yyyy-MM-dd"), 
                                update.UpdateData.UpdateTime, 
                                update.UpdateData.Offer.Value, 
                                update.UpdateData.Bid.Value)

        Console.WriteLine(String.Format("{0} - {1}", contents, update.ItemName))
        File.AppendAllText(filePath, contents + Environment.NewLine)
    
/// <summary>
/// subscribe to markets
/// </summary>
let csvWriterAgent (filePath : string) = MailboxProcessor<UpdateArgs<L1LsPriceData>>.Start(fun inbox -> 
            async {while true do 
                    let! msg = inbox.Receive()
                    writeToCsv filePath msg |> ignore})
///
/// filepath for given epic
///
let filePath (epic : string) = DateTime.Now.ToString("yyyy MM dd HH mm ss ") + epic.Replace("/", "") + ".csv"

/// <summary>
/// subscribe to heartbeat
/// </summary>
let subscribeHeartbeat (igStreamClient : IGStreamingApiClient) = 
    let marketDetailsListener = HeartbeatListener()
    igStreamClient.SubscribeToMarketDetails([|"TRADE:HB.U.HEARTBEAT.IP"|], marketDetailsListener, ["HEARTBEAT"])

/// <summary>
/// subs
/// </summary>
let subscriptions (markets : seq<Market>) : seq<Subscription> = 
    seq {for market in markets do 
            let path = filePath(market.epic)
            let marketDetailsListener = MarketDetailsTableListerner()
            marketDetailsListener.Update.Add(onMarketUpdate (csvWriterAgent path))
            yield {marketDetails = market; filePath = path; listener = marketDetailsListener}}


/// <summary>
/// 
/// </summary>
[<EntryPoint>]
let main argv = 
    let marketPosition = igLogOn
    
    let context = marketPosition.igClient.GetConversationContext()

    printPositions marketPosition.positions.positions

    let markets = searchMarketEpics(argv, marketPosition.igClient)

    Console.WriteLine("Subscribing to the following markets...")
    for market in markets do    
        Console.WriteLine(market.instrumentName + " - " + market.marketStatus)

    let streamClient = ErrorIgStreamClient(subscriptions markets |> Seq.toList)
    let streamResult = streamClient.Connect(marketPosition.authentication.currentAccountId, context.cst, context.xSecurityToken, context.apiKey, marketPosition.authentication.lightstreamerEndpoint)

    if streamResult then

        let hbKey = streamClient.SubscribeToHeartbeat(HeartbeatListener())
        Console.ReadKey() |> ignore
       
    else
        Console.WriteLine("Couldn't connect stream"); 

    streamClient.Disconnect() |> ignore
    marketPosition.igClient.logout() |> ignore
    0 // return an integer exit code
