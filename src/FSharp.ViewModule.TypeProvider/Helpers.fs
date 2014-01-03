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
module internal TypeProvider =
    /// Load an assembly file properly for a type provider.
    let loadAssemblyFile fileName = File.ReadAllBytes fileName |> Assembly.Load

[<RequireQualifiedAccess>]
module internal AssemblyHelpers =
    /// Used to load assemblies into a remote appdomain and search them
    type RemoteAssemblyLoader() =
        inherit MarshalByRefObject()

        member this.CheckAssembly (predicate : Assembly -> bool) path =
            let assembly = Assembly.LoadFrom(path)
            predicate(assembly)

    let private matchingType (t : Type) = t.GetInterfaces() |> Array.exists (fun t -> t = typeof<IViewModuleTypeSpecification>)

    let private predicate (assembly : Assembly) =
        try
            assembly.GetTypes()
            |> Array.exists matchingType
        with
        | exn -> false
         
    /// Finds the appropriate type specifier from the referenced assemblies of the project, without
    /// loading them into the current AppDomain
    let findViewModuleTypeSpecification (referencedAssemblies : string []) =
        // let appDomain = AppDomain.CreateDomain("ViewModuleTypeCheckDomain")
        let loadType = typeof<RemoteAssemblyLoader>
        let loader = RemoteAssemblyLoader() // appDomain.CreateInstanceAndUnwrap(loadType.Assembly.FullName, loadType.FullName) :?> RemoteAssemblyLoader

        let matches =
            referencedAssemblies
            |> Array.filter (loader.CheckAssembly predicate)

        // AppDomain.Unload(appDomain)

        match matches.Length with
        | 0 -> None
        | _ -> Some matches.[0]

    let loadViewModuleTypeSpecification (assembly : Assembly) =
        let t = 
            assembly.GetTypes()
            |> Array.filter matchingType

        let vmType = t.[0]
        Activator.CreateInstance(vmType) :?> IViewModuleTypeSpecification