namespace FSharp.SSEClient

open System
open System.IO
open System.Reactive.Disposables  
open System.Reactive.Linq
open FSharp.Control.Reactive

module SSEConnection =
  
  type SSEEvent = 
    {Data:string option; EventName:string option; Id:string option; Retry:int option} 
     static member None = {Data = None;EventName = None;Id = None; Retry = None}
  
  let dataAppend s1 s2 = 
    let s = [s1;s2] |> List.choose id
    if s = List.Empty then None else Some <| String.Join("\n", s) 
                          
  type SSELine = 
    | Event of string option 
    | Data of string option  
    | Id of string  
    | Retry of int 
    static member Fold(s) =
      s |> Seq.fold 
        (fun e l -> 
            match l with
            |Event s -> {e with EventName = s}  
            |Data s -> {e with Data = dataAppend e.Data s}   
            |Id s -> {e with Id = Some s} 
            |Retry s -> {e with Retry = Some s}         
        ) SSEEvent.None
  
  type SSEProcessingState =
    |Processing of SSELine list
    |DispatchReady of SSELine list
    member x.Unwrap() = 
      match x with
      |Processing p -> p
      |DispatchReady p -> p
    member x.IsReady() =
      match x with
      |DispatchReady _ -> true |_ -> false 
  
  let toLine (str:string) =
    let nv = match str.Split([|':'|],2) with
               |[|n;v|] -> [|n;v.TrimStart()|]
               |s -> s
    match nv with
    |[|"event";v|] -> Some (Event (Some v))
    |[|"data";v|] -> Some (Data (Some v))
    |[|"id";v|] -> Some (Id v)
    |[|"retry";v|] when fst (Int32.TryParse v) -> Some <| Retry (Int32.Parse v)
    |[|"event"|] -> Some (Event None)     
    |[|"data"|] -> Some (Data None)      
    |_ -> None
  
  let processLines (obs:IObservable<_>) =
    obs |> Observable.scanInit (Processing [])
      (fun e l -> let ps = 
                    match e with
                    |DispatchReady _ -> Processing []
                    |Processing _ -> e
                  match l |> toLine with 
                  |None -> DispatchReady <| (ps.Unwrap() |> List.rev)
                  |Some li -> Processing <| li::ps.Unwrap()) 
  
  let startDisposable (op:Async<unit>) =
    let ct = new System.Threading.CancellationTokenSource()
    Async.Start(op, ct.Token)
    { new IDisposable with 
        member __.Dispose() = ct.Cancel() }  
  
  let receive (network:Stream) =
    let rec read (observer:IObserver<_>) (sr:StreamReader) = async {
      match (sr.ReadLine()) with
      | null -> observer.OnCompleted() 
      | line -> observer.OnNext line
      return! read observer sr}
    let start obs = async {
      try
        let sr = new StreamReader(network)
        return! read obs sr
      with |e -> obs.OnError e}
    Observable.Create (start >> startDisposable)
      |> Observable.filter (fun s -> not <| s.StartsWith(":")) 
      |> processLines
      |> Observable.filter (fun t -> t.IsReady())
      |> Observable.map (fun t -> t.Unwrap() |> SSELine.Fold)

