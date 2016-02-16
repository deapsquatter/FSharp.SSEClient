namespace FSharp.SSEClient

open System
open System.IO
open System.Reactive.Disposables  
open System.Reactive.Linq
open FSharp.Control.Reactive

module SSEConnection =
  
  type SSEEvent = 
    {Data:string option; EventName:string option; Id:string option; Retry:UInt32 option} 
     static member None = {Data = None;EventName = None;Id = None; Retry = None}
  
  let dataAppend s1 s2 = 
    let s = [s1;s2] |> List.choose id
    if s = List.Empty then None else Some <| String.Join("\n", s) 
                          
  type SSELine = 
    | Event of string option 
    | Data of string option  
    | Id of string  
    | Retry of UInt32 
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
      
  let unwrap (ps:SSEProcessingState) = ps.Unwrap()
  
  let isReady (ps:SSEProcessingState) = ps.IsReady()
  
  let toLine (str:string) =
    let nv = match str.Split([|':'|],2) with
             |[|n;v|] -> [|n;v.TrimStart()|]
             |s -> s
    match nv with
    |[|"event";v|] -> Some (Event (Some v))
    |[|"data";v|] -> Some (Data (Some v))
    |[|"id";v|] -> Some (Id v)
    |[|"retry";v|] when fst (UInt32.TryParse v) -> Some <| Retry (UInt32.Parse v)
    |[|"event"|] -> Some (Event None)     
    |[|"data"|] -> Some (Data None)      
    |_ -> None
    
  let getIdLine lines =
    lines |> List.tryFindBack 
      (fun t -> match t with |Retry _ -> true|_ -> false) 
      
  module List =
    let ofOption t = match t with Some s -> [s] |_ -> []
      
  let processLines (obs:IObservable<_>) =
    obs |> Observable.scanInit (Processing [])
      (fun e l -> let lines =
                    match e with
                    |DispatchReady x -> List.ofOption (getIdLine x)
                    |Processing x -> x
                  match l |> toLine with 
                  |None -> DispatchReady <| (lines |> List.rev)
                  |Some li -> Processing <| li::lines) 
  
  let startDisposable (op:Async<unit>) =
    let ct = new System.Threading.CancellationTokenSource()
    Async.Start(op, ct.Token)
    { new IDisposable with 
        member __.Dispose() = ct.Cancel() }  
        
  let repeatInfinite (delay:TimeSpan) (src:IObservable<_>) = seq{
    yield src
    while true do yield src.DelaySubscription(delay)}      
  
  let retryAfterDelay (delay:TimeSpan) (src:IObservable<_>)  =
    src |> repeatInfinite delay |> Observable.catchSeq
        
  let receive (network:unit -> Stream) (retryTime:TimeSpan option) =
    let rec read (observer:IObserver<_>) (sr:StreamReader) = async {
      match (sr.ReadLine()) with
      | null -> observer.OnCompleted() 
      | line -> observer.OnNext line
      return! read observer sr}
    let start obs = async {
      try
        use sr = new StreamReader(network ())
        return! read obs sr
      with |e -> obs.OnError e}
    Observable.Create (start >> startDisposable)
      |> Observable.filter (fun s -> not <| s.StartsWith(":")) 
      |> processLines
      |> Observable.filter isReady
      |> Observable.map (unwrap >> SSELine.Fold)
      |> retryAfterDelay (defaultArg retryTime (TimeSpan.FromSeconds 0.))