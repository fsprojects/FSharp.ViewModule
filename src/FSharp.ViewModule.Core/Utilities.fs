namespace FSharp.ViewModule

open System
open System.IO

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open System.Reflection

[<assembly:AssemblyVersion("0.9.6.0")>]
[<assembly:AssemblyFileVersion("0.9.6.0")>]
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