namespace FSharp.SSEClient

open System
open System.IO
open System.Reactive.Disposables  
open System.Reactive.Linq
open FSharp.Control.Reactive
     
  type SSEData = SSEData of string   
  [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module SSEData =  
  
    let append s1 s2 = 
      let m = Option.map (fun (SSEData t) -> t)
      let s = [m s1;m s2] |> List.choose id
      if s = List.Empty then None else Some (SSEData <| String.Join("\n", s) )        
  
  type SSELine = 
    | Event of string option 
    | Data of SSEData option  
    | Id of string  
    | Retry of UInt32 
  [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module SSELine =
      
    let fromLine (str:string) =
      let nv = match str.Split([|':'|],2) with
               |[|n;v|] -> [|n;v.TrimStart()|]
               |s -> s
      match nv with
      |[|"event";v|] -> Some (Event (Some v))
      |[|"data";v|] -> Some (Data (Some <| SSEData v))
      |[|"id";v|] -> Some (Id v)
      |[|"retry";v|] when fst (UInt32.TryParse v) -> Some <| Retry (UInt32.Parse v)
      |[|"event"|] -> Some (Event None)     
      |[|"data"|] -> Some (Data None)      
      |_ -> None  
 
    let getIdLine lines =
      lines |> List.tryFindBack 
        (fun t -> match t with |Retry _ -> true|_ -> false)  
        
  type SSEProcessingState =
    |Processing of SSELine list
    |DispatchReady of SSELine list  
 [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module SSEProcessingState =
  
    let isReady = function |DispatchReady _ -> true |_ -> false  
    let unwrap = function |Processing p -> p |DispatchReady p -> p                  
 
  type SSEEvent = 
    {Data:SSEData option; EventName:string option; Id:string option; Retry:UInt32 option} 
     static member None = {Data = None;EventName = None;Id = None; Retry = None}   
 [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module SSEEvent =
 
    let fromLines lines =
      lines |> Seq.fold 
        (fun e l -> 
            match l with
            |Event s -> {e with EventName = s}  
            |Data s -> {e with Data = SSEData.append e.Data s}   
            |Id s -> {e with Id = Some s} 
            |Retry s -> {e with Retry = Some s}         
        ) SSEEvent.None                      
  
  module List =
    
    let ofOption t = match t with Some s -> [s] |_ -> []
    
  module Observable =
    
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
        
    let processStrings (obs:IObservable<_>) =
      obs |> Observable.scanInit (Processing [])
        (fun e l -> let lines =
                      match e with
                      |DispatchReady x -> List.ofOption (SSELine.getIdLine x)
                      |Processing x -> x
                    match l |> SSELine.fromLine with 
                    |None -> DispatchReady <| (lines |> List.rev)
                    |Some li -> Processing <| li::lines)         
            
    let receive (network:unit -> Stream) (retryDelay:TimeSpan option) =
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
        |> processStrings
        |> Observable.filter SSEProcessingState.isReady
        |> Observable.map (SSEProcessingState.unwrap >> SSEEvent.fromLines)
        |> retryAfterDelay (defaultArg retryDelay (TimeSpan.FromSeconds 0.))
        
  type Connection =
    static member Receive(network:unit -> Stream, ?retryDelay: TimeSpan) =
      Observable.receive network retryDelay