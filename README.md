# IG-Stream
Streaming market prices from ig.com and saving to a csv

Example command line ``IG_Stream.exe "usd jpy" "gold" "oil" "aud gbp"``

# Setup

- Clone the ig .net sample ``https://github.com/IG-Group/ig-webapi-dotnet-sample``
- Clone this repo and open up in visual studio.
- Add references to all the dll's in ``ig-webapi-dotnet-sample\IGWebApiClient\bin\Debug``
- Add reference to ``ig-webapi-dotnet-sample\packages\Validation.2.3.7\lib\dotnet\Validation.dll``
- Reference to ``FSharp.Data``
- Get an api credentials from ``labs.ig.com`` and save into apiKey.json (check the apiKey_example.json)
- Make sure to add apiKey.json to visual studio project and tell visual studio to copy to the build directory.
- Go ahead and compile

# Issue

The ig lightstream implementation tends to just cut out after a while, sometimes minutes sometimes a few hours
to get around this modify the code in IGStreamingApi.cs at around line 552 and add the line: 
`` connectionInfo.StreamingTimeoutMillis = 15000;`` 

This doesn't fix the problem but does seem to lessen it.

I've also added an additional member function to IgStreamingApiClient to subscribe to the server heartbeat:

```
public SubscribedTableKey SubscribeToHeartbeat(IHandyTableListener tableListener)
{
    var extTableInfo = new ExtendedTableInfo(new string[] {"TRADE:HB.U.HEARTBEAT.IP"}, "MERGE", new string[] { "HEARTBEAT"}, true);
    return lsClient.SubscribeTable(extTableInfo, tableListener, false);
}
```



