# Client for reading Server-Sent Events (SSE)
The library (`FSharp.SSEClient`) implements a client for reading events typically over Http (although you can use any stream you like). The library aims to conform (as close as possible) to the [SSE Specification](https://www.w3.org/TR/eventsource/).
## Example Usage
The SSE Client is implemented as an Observable. This makes it handy to compose your events using RX.
```csharp
open FSharp.SSEClient
open FSharp.Data

let s = Http.RequestStream("http://demo.howopensource.com/sse/stocks.php")
Connection.Receive s.ResponseStream
  |> Observable.subscribe (printfn "SSE Event=%A")
```
## Building
* Windows: Run *build.cmd* 
* Mono: Run *build.sh*

## Using with PAKET
`FSharp.SSEClient` can easily be linked as a single file using the PAKET dependency manager. Simply add the following to your `paket.dependencies` file:
```csharp
group SSE
   github deapsquatter/FSharp.SSEClient /src/SSEClient.fs
```
and to your projects `paket.references` file:
```csharp
group SSE
    File:SSEClient.fs
```
 
