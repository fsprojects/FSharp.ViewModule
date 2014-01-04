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

module FSharp.ViewModule.TypeProvider

open System
open System.IO
open System.Reflection
open System.Windows.Input

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns
open Microsoft.FSharp.Quotations.ExprShape

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

open FSharp.ViewModule.Helpers
open FSharp.ViewModule.TypeProviderInternal

[<TypeProvider>]
type ViewModelTypeProvider (cfg: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces ()

    let asm = Assembly.GetExecutingAssembly ()
    let ns = this.GetType().Namespace
    let pn = "ViewModelProvider"

    let tempAsm = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
    // TODO: See if there's a way to find the appropriate assembly/specification at runtime via reflection.
    // It appears this doesn't work with PCL assemblies, however, which is why they're currently being specified here.
    let parameters = 
        [
        ProvidedStaticParameter ("modelsAssembly", typeof<string>) 
        ; ProvidedStaticParameter("modelsSpecificationAssembly", typeof<string>) 
        ; ProvidedStaticParameter("modelsSpecification", typeof<string>) 
        ]
    
    do
        // THIS IS NECESSARY
        AppDomain.CurrentDomain.add_AssemblyResolve (fun _ args ->
            let localAssemblies = System.AppDomain.CurrentDomain.GetAssemblies()
            let referencedAssemblies = cfg.ReferencedAssemblies
            let name = System.Reflection.AssemblyName(args.Name)
            let existingAssembly = 
                localAssemblies 
                |> Seq.tryFind(fun a -> System.Reflection.AssemblyName.ReferenceMatchesDefinition(name, a.GetName()))
            match existingAssembly with
            | Some a -> a
            | None -> 
                let ref = 
                    referencedAssemblies
                    |> Seq.tryFind (fun a -> AssemblyName.ReferenceMatchesDefinition(AssemblyName.GetAssemblyName(a), name))
                match ref with
                | Some a -> Assembly.LoadFrom a
                | None -> null)
    
        try
            let def = ProvidedTypeDefinition (asm, ns, pn, Some typeof<obj>, IsErased = false) 
            tempAsm.AddTypes [def]
            def.DefineStaticParameters (parameters, this.GenerateTypes)
            this.AddNamespace(ns, [def])
        with 
        | exn ->
            printfn "%s" exn.Message

    /// FindModelsAssembly
    member internal this.LoadAssemblyByFilename fileName =
        match cfg |> TypeProviderConfig.tryFindAssembly (fun fullPath -> Path.GetFileNameWithoutExtension fullPath = fileName) with
        | None -> failwithf "Invalid models assembly name %s. Pick from the list of referenced assemblies." fileName
        | Some masmFileName -> AssemblyHelpers.loadFile masmFileName

    /// GenerateTypes
    member internal this.GenerateTypes (typeName: string) (args: obj[]) =
        let modelsAssembly = args.[0] :?> string
        let modelsSpecificationAssembly = args.[1] :?> string
        let modelsSpecification = args.[2] :?> string

        let masm = this.LoadAssemblyByFilename modelsAssembly
        let vmcAssembly = this.LoadAssemblyByFilename modelsSpecificationAssembly
        let vmcTemplate =  AssemblyHelpers.loadViewModuleTypeSpecification vmcAssembly modelsSpecification

        let types =
            Assembly.types masm
            |> List.filter (fun x -> FSharpType.IsRecord x)
            |> List.map (fun x ->
                let state = ProvidedField("state", x)
                state.SetFieldAttributes(FieldAttributes.Private)
                ({ ModelType = x; ModuleType = moduleType x masm; State = state }, vmcTemplate))

        let def = ProvidedTypeDefinition (asm, ns, typeName, Some typeof<obj>, IsErased = false) 
        tempAsm.AddTypes [def]

        def.AddMembersDelayed <| fun () -> 
            let defs = 
                types 
                |> List.map  (fun t -> 
                    generateViewModel (fst t) (snd t) )
            tempAsm.AddTypes defs
            defs

        def

[<assembly:TypeProviderAssembly>]
do ()
