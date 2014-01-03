module FSharp.ViewModule.Helpers

open System
open System.IO
open System.Reflection

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Reflection

open FSharp.ViewModule
open ProviderImplementation
open ProviderImplementation.ProvidedTypes


/// Helps use a Type safely.
[<RequireQualifiedAccess>]
module internal Type =
    let tryMethod name (t: Type) =
        match t.GetMethod name with
        | null  -> None
        | x     -> Some x

    let recordFields (t: Type) = FSharpType.GetRecordFields t |> List.ofArray

    let methods (t: Type) = t.GetMethods () |> List.ofArray

    let moduleFunctions (t: Type) =
        methods t
        |> List.filter (fun x -> 
        x.Name <> "GetType" && 
        x.Name <> "GetHashCode" && 
        x.Name <> "Equals" && 
        x.Name <> "ToString")

/// Helps use an Assembly safely.
[<RequireQualifiedAccess>]
module internal Assembly =
    let tryType name (asm: Assembly) =
        match asm.GetType name with
        | null  -> None
        | x     -> Some x

    let types (asm: Assembly) = asm.GetTypes () |> List.ofArray

[<RequireQualifiedAccess>]
module internal TypeProviderConfig =
    let tryFindAssembly predicate (cfg: TypeProviderConfig) =
        cfg.ReferencedAssemblies |> Array.tryFind predicate

[<RequireQualifiedAccess>]
module internal AssemblyHelpers =
    /// Load an assembly file properly for a type provider.
    let loadFile fileName = File.ReadAllBytes fileName |> Assembly.Load

    let loadViewModuleTypeSpecification (assembly : Assembly) typeName =
        let vmType = assembly.GetType(typeName)
        Activator.CreateInstance(vmType) :?> IViewModuleTypeSpecification