# Client for reading Server-Sent Events (SSE)
The library (`FSharp.SSEClient`) implements a client for reading events typically over Http (although you can use any stream you like). The library aims to conform (as close as possible) to the [SSE Specification](https://www.w3.org/TR/eventsource/).
## Example Usage
The SSE Client is implemented as an Observable. This makes it handy to compose your events using RX.
```csharp
open FSharp.SSEClient
open FSharp.Data

let s = Http.RequestStream("http://demo.howopensource.com/sse/stocks.php")
SSEConnection.receive s.ResponseStream 
  |> Observable.subscribeWithError (printfn "SSE Event=%A") (fun e -> printfn "Error=%s" (e.Message))
```
## Building
* Windows: Run *build.cmd* 
* Mono: Run *build.sh*
 
