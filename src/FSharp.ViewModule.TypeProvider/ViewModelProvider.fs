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

namespace FSharp.ViewModule.TypeProvider

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
    
    // TODO: See if there's a way to find the appropriate assembly/specification at runtime via reflection.
    // It appears this doesn't work with PCL assemblies, however, which is why they're currently being specified here.
    let parameters = 
        [
        ProvidedStaticParameter ("modelsAssembly", typeof<string>) 
        ; ProvidedStaticParameter("modelsSpecificationAssembly", typeof<string>, String.Empty) 
        ; ProvidedStaticParameter("modelsSpecification", typeof<string>, String.Empty) 
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
        let tempAsm = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
        let modelsAssembly = args.[0] :?> string
        let modelsSpecificationAssembly = args.[1] :?> string
        let modelsSpecification = args.[2] :?> string

        let masm = this.LoadAssemblyByFilename modelsAssembly
        
        let vmcTemplate =  
            match modelsSpecificationAssembly, modelsSpecification with
            | "", "" ->
                FSharp.ViewModule.TypeProvider.DefaultViewModuleTypeSpecification() :> FSharp.ViewModule.IViewModuleTypeSpecification
            | _, _ ->
                let vmcAssembly = this.LoadAssemblyByFilename modelsSpecificationAssembly
                AssemblyHelpers.loadViewModuleTypeSpecification vmcAssembly modelsSpecification

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
