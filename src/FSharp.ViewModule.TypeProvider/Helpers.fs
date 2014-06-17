(*
Copyright (c) 2013-2014 FSharp.ViewModule Team

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*)

module internal FSharp.ViewModule.Helpers

open System
open System.IO
open System.Reflection

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Reflection

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

open FSharp.ViewModule

/// Helps use a Type safely.
[<RequireQualifiedAccess>]
module Type =
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
module Assembly =
    let tryType name (asm: Assembly) =
        match asm.GetType name with
        | null  -> None
        | x     -> Some x

    let types (asm: Assembly) = asm.GetTypes () |> List.ofArray

[<RequireQualifiedAccess>]
module TypeProviderConfig =
    let tryFindAssembly predicate (cfg: TypeProviderConfig) =
        cfg.ReferencedAssemblies |> Array.tryFind predicate

[<RequireQualifiedAccess>]
module AssemblyHelpers =
    /// Load an assembly file properly for a type provider.
    let loadFile fileName = File.ReadAllBytes fileName |> Assembly.Load

    let loadViewModuleTypeSpecification (assembly : Assembly) typeName =
        let vmType = assembly.GetType(typeName)
        Activator.CreateInstance(vmType) :?> IViewModuleTypeSpecification