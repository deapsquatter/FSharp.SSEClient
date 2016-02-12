namespace FSharp.SSEClient.Tests

open System.Text
open System.IO
open NUnit.Framework
open NUnit.Framework.Constraints
open FsUnit
open System.Reactive.Linq
open System.Reactive.Disposables
open FSharp.SSEClient
open FSharp.SSEClient.SSEConnection
open FSharp.Control.Reactive.Observable

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

  let stream () =
    let stream = new MemoryStream()
    let writer = new StreamWriter(stream)
    writer.Write ``sample sse stream``
    writer.Flush()
    stream.Position <- 0L  
    stream
    
  let obs () = SSEConnection.receive (stream ())

  [<Test>]
  let ``Last received event is as expected``() =
    let m = obs () |> wait
    m |> should equal {Data = Some "GOOG:533.95";EventName = None;Id = Some "5";Retry = None}   
    
  [<Test>]
  let ``First received event is as expected``() =
    let m = obs () |> first |> wait
    m |> should equal {Data = Some "GOOG:533.37";EventName = None;Id = Some "0";Retry = None}               
    
    