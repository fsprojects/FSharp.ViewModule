namespace FSharp.ViewModule

open System
open System.IO

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open System.Reflection

[<assembly:AssemblyVersion("0.9.7.0")>]
[<assembly:AssemblyFileVersion("0.9.7.0")>]
do()

[<AutoOpen>]
module internal Utilities =
    let internal castAs<'T when 'T : null> (o:obj) = 
        match o with
        | :? 'T as res -> res
        | _ -> null

    let internal downcastAndCreateOption<'T> (o: obj) =
        match o with
        | :? 'T as res -> Some res
        | _ -> None
    
    let getPropertyNameFromExpression(expr : Expr) = 
        match expr with
        | PropertyGet(a, pi, list) -> pi.Name
        | _ -> ""


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
