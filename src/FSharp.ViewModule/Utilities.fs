namespace ViewModule

open System
open System.IO

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open System.Linq.Expressions
open System.Reflection
open System.Threading
open System.Threading.Tasks

[<assembly:System.Runtime.CompilerServices.Extension>]
[<assembly:System.Runtime.CompilerServices.InternalsVisibleTo("FSharp.ViewModule.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100cf203ce06af4e2593a080a08fd93abc9ab1acc5726f6ccf3fa03c73924a7545c50e263899072edeb6cfbce1c8528bbc5b7bc681d5dcf72cfe1ad1644a61adeb5f1a47699271008ca463835c6d1986efd918f9cc45d17d5b3a50a80c43e17cb9407005401d43f0745f64495374be491807c6b4b8ae2505d517824970a250e90b8")>]
[<assembly:AssemblyKeyFileAttribute("fsvm.snk")>]
do ()

[<AutoOpen>]
module internal Utilities =
    let internal castAs<'T when 'T : null> (o:obj) = 
        match o with
        | :? 'T as res -> res
        | _ -> null

    let internal downcastAndCreateOption<'T> (o: obj) =
        match o with
        | :? 'T as res -> 
            // Check direct cast - works for reference types
            Some res
        | _ ->             
            // Avoid exceptions in Convert call when possible
            let rType = typeof<'T>
            match o, rType.GetTypeInfo().IsValueType with
            | null, true -> None
            | _ ->
                try
                    // Handle nullable types, mismatched value types, or convertible reference types
                    match Convert.ChangeType(o, typeof<'T>) with
                    | :? 'T as res -> Some res
                    | _ -> None
                with
                | _ -> None
    
    let getPropertyNameFromExpression(expr : Expr) = 
        match expr with
        | PropertyGet(a, pi, list) -> pi.Name
        | _ -> ""

    let rec getPropertyNameFromLinqExpression (linqExpr : Expression) =
        match linqExpr with
        | :? LambdaExpression as l -> getPropertyNameFromLinqExpression l.Body
        | :? UnaryExpression as u -> getPropertyNameFromLinqExpression u.Operand
        | :? MemberExpression as m ->
            match m.Member with
            | :? PropertyInfo as p -> p.Name
            | _ -> ""
        | _ -> ""

module internal Async =
    let fromTaskFunc (createTask : Func<Task>) =
        createTask.Invoke () |> Async.AwaitIAsyncResult |> Async.Ignore

    let fromTaskFuncCancellable (createTask : Func<CancellationToken, Task>) = async {
        let! ct = Async.CancellationToken
        do! createTask.Invoke ct |> Async.AwaitIAsyncResult |> Async.Ignore }
    
    let fromTaskParamFunc (createTask : Func<'a, Task>) param =
        createTask.Invoke param |> Async.AwaitIAsyncResult |> Async.Ignore

    let FromTaskParamFuncCancellable (createTask : Func<'a, CancellationToken, Task>) param = async {
        let! ct = Async.CancellationToken
        do! createTask.Invoke(param, ct) |> Async.AwaitIAsyncResult |> Async.Ignore }

module public Helpers =
    let getPropertyNameFromExpression(expr : Expr) =
        Utilities.getPropertyNameFromExpression(expr)

    let runOnContextIfExists (syncContext : System.Threading.SynchronizationContext) action =
        match syncContext with
        | null -> action()
        | ctx -> ctx.Post((fun s -> action()), null)

    let runOnCurrentContextIfExists action =
        runOnContextIfExists System.Threading.SynchronizationContext.Current action

module public CollectionHelpers =
    let bindObservableToCollectionOnContext<'a> syncContext (collection: System.Collections.ObjectModel.ObservableCollection<'a>)  (observable : IObservable<'a>) =        
        observable.Subscribe(fun o -> 
            let action () = collection.Add(o)
            Helpers.runOnContextIfExists syncContext action) 

    let bindObservableToCollection<'a> collection observable =
        let ctx = System.Threading.SynchronizationContext.Current
        bindObservableToCollectionOnContext ctx collection observable
