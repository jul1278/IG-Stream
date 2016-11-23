module HistoricalPrices

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
open dto.endpoint.prices.v2
open dto.endpoint.browse
open dto.endpoint.search
open dto.endpoint.accountbalance

let priceString (price : PriceSnapshot) = 
                //time, ask open, ask close, ask high, ask low, bid open, bid close, bid high, bid low, ltv
    String.Format("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}", 
            price.snapshotTime, 
            price.openPrice.ask.Value, 
            price.closePrice.ask.Value, 
            price.highPrice.ask.Value, 
            price.lowPrice.ask.Value,
            price.openPrice.bid.Value,
            price.closePrice.bid.Value,
            price.highPrice.bid.Value,
            price.lowPrice.bid.Value, 
            price.lastTradedVolume.Value)

///
/// historicalPrices
///
let historicalPricesToCsv (market : Market, startDate : DateTime, endDate : DateTime, period : string, igClient : IgRestApiClient) =
    let startDateString = startDate.ToString("yyyy-MM-ddTHH:mm:ss")
    let endDateString = endDate.ToString("yyyy-MM-ddTHH:mm:ss")
    
    let priceTask = igClient.priceSearchByDateV2(market.epic, period, startDateString, endDateString)
    priceTask.Wait()

    let filename = String.Format("{0} {1}.csv", market.epic.Replace("/", " "), startDate.ToString("yyyy MM dd HH mm ss"))
    let headerString = "time, ask_open, ask_close, ask_high, ask_low, bid_open, bid_close, bid_high, bid_low, ltv" + Environment.NewLine

    let pricesStrings = priceTask.Result.Response.prices |> Seq.map(fun p -> (priceString p) + Environment.NewLine)

    File.WriteAllLines(filename, [headerString])
    File.AppendAllLines(filename, pricesStrings |> Seq.toArray)
