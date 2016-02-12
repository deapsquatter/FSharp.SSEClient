# FSharp.SSE
The Server-Sent Events(SSE) Library ('FSharp.SSClient') implements a client for reading events typically over Http. The library aims to conform (as close as possible) to the [SSE Specification](https://www.w3.org/TR/eventsource/).
## Example Usage
```csharp
open FSharp.SSEClient
open FSharp.Data

let s = Http.RequestStream("http://demo.howopensource.com/sse/stocks.php")
SSEConnection.receive s.ResponseStream 
  |> Observable.subscribeWithError (printfn "SSE Event=%A") (fun e -> printfn "Error=%s" (e.Message))
```
 
