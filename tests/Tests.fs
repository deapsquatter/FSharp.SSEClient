namespace FSharp.SSEClient.Tests

open System.Text
open System.IO
open NUnit.Framework
open NUnit.Framework.Constraints
open FsUnit
open System.Reactive.Linq
open System.Reactive.Disposables
open FSharp.Control.Reactive
open FSharp.SSEClient
open FSharp.SSEClient.SSEConnection

module Main =

  let ``sample sse stream`` = "id: 0
data: GOOG:533.37

id: 1
data: MSFT:47.59

id: 2
data: IBM:162.99

id: 3
data: AAPL:114.12

id: 4
data: MSFT:47.29

id: 5
data: GOOG:533.95

"  

  [<Test>]
  let ``Last received event is as expected``() =
    let stream = new MemoryStream()
    use writer = new StreamWriter(stream)
    writer.Write ``sample sse stream``
    writer.Flush()
    stream.Position <- 0L
    
    let obs = SSEConnection.receive stream
    let m = obs |> Observable.wait
    m |> should equal {Data = Some "GOOG:533.95";EventName = None;Id = Some "5";Retry = None}         
    
    