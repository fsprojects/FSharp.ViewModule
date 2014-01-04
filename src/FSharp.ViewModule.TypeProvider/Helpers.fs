(*
Copyright (c) 2013-2014 FSharp.ViewModule Team

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*)

module internal FSharp.ViewModule.Helpers

open System
open System.IO
open System.Reflection

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Reflection

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

open FSharp.ViewModule.Core

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