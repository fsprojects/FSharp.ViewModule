(*
Copyright (c) 2013 William F. Smith

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

open Cirrious.MvvmCross.ViewModels

(********************************************************************
IMPORTANT!

Type-provider is still directly tied to MvvmCross; this needs to be
broken out soon.
********************************************************************)

/// Public
[<RequireQualifiedAccess>]
module MvxCommand =
    let create (f: unit -> unit) = MvxCommand (Action (f))

// Helpers

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

// End Helpers

/// Contains the association of model and module types.
type internal MvxViewModelInfo = { ModelType: Type; ModuleType: Type; State: FieldInfo }

/// Discriminated union for methods on the view-model.
type internal MvxMethodInfo =
    | Command of string * string

/// Discriminated union for types of mvx properties
type internal MvxPropertyInfo =
    /// Property that has a raise property changed call in the setter.
    | Observable of string * Type * string list

    /// Property that returns a value based on observable(s).
    | Computed of string * Type

    /// Property that returns a MvxCommand. Usually causes a side effect and/or new state for the model.
    | Command of string * MethodInfo

let internal (|VM|) (vm: MvxViewModelInfo) = vm.ModelType, vm.ModuleType, vm.State

let internal mvxNotifyPropertyChanged this = Expr.Coerce (this, typeof<IMvxNotifyPropertyChanged>)
let internal raisePropertyChanged = typeof<IMvxNotifyPropertyChanged>.GetMethod ("RaisePropertyChanged", [|typeof<string>|])
    
/// Gets the module that is associated with the given model type.
let internal moduleType (modelType: Type) asm =
    let moduleName = modelType.FullName + "Module"

    match Assembly.tryType moduleName asm with
    | None -> failwithf "%s not found." moduleName
    | Some x -> x

let internal computedFieldNames modelType expr =
    let rec f modelType names = function
        | Application (expr1, expr2) ->
            names @ (f modelType [] expr1) @ (f modelType [] expr2)

        | Call (expr, meth, exprList) ->
            names @ (List.collect (f modelType []) exprList) @
            match expr with
            | None -> []
            | Some x -> f modelType [] x
            @
            match Expr.TryGetReflectedDefinition (meth) with
            | None -> []
            | Some x -> f modelType [] x 

        | Lambda (_, body) -> f modelType names body

        | Let (_, expr1, expr2) ->
            names @ (f modelType [] expr1) @ (f modelType [] expr2)

        | PropertyGet (_, propOrValInfo, _) ->
            match propOrValInfo.DeclaringType = modelType with
            | false -> names
            | _ -> propOrValInfo.Name :: names

        | _ -> names

    f modelType [] expr
    |> Seq.distinct
    |> Seq.toList

let internal changedFieldNames modelType expr =
    let rec f modelType names = function
        | Application (expr1, expr2) ->
            names @ (f modelType [] expr1) @ (f modelType [] expr2)

        | Call (expr, meth, exprList) ->
            names @ (List.collect (f modelType []) exprList) @
            match expr with
            | None -> []
            | Some x -> f modelType [] x
            @
            match Expr.TryGetReflectedDefinition (meth) with
            | None -> []
            | Some x -> f modelType [] x 

        | Lambda (_, body) -> f modelType names body

        | Let (_, expr1, expr2) ->
            names @ (f modelType [] expr1) @ (f modelType [] expr2)

        | NewRecord (recType, exprList) ->
            match recType = modelType with
            | false -> names
            | _ ->

            // Note: May need to revisit this at some point.
            Type.recordFields recType
            |> List.fold2 (fun names field -> function
                | PropertyGet (_, propInfo, _) when propInfo.DeclaringType = recType -> names
                | _ -> field.Name :: names) [] <| exprList

        | _ -> names

    f modelType [] expr
    |> Seq.distinct
    |> Seq.toList

let internal namesToSequentialPropertyChanged names this =
    names |> List.map Expr.Value
    |> List.map (fun x -> Expr.Call (mvxNotifyPropertyChanged this, raisePropertyChanged, [x]))
    |> List.fold (fun expr x -> Expr.Sequential (expr, x)) (Expr.Value (()))

let internal propertyGetterCode (VM (modelType, moduleType, state)) = function
    | Observable (name, _, _) -> function
        | [this] -> Expr.PropertyGet (Expr.FieldGet (this, state), state.FieldType.GetProperty name)
        | _ -> raise <| ArgumentException ()

    | Computed (name, _) -> function
        | [this] -> Expr.Call (moduleType.GetMethod name, [Expr.FieldGet (this, state)])
        | _ -> raise <| ArgumentException ()

    | Command (name, meth) -> function
        | [this] ->
            let var = Var ("vm", typeof<obj>)
            let lambda =
                Expr.Lambda (var,
                    <@@
                    fun () ->
                    %%Expr.Call (Expr.Coerce (Expr.Var var, meth.DeclaringType), meth, []) 
                    () @@>)

            <@@ MvxCommand.create (%%Expr.Application (lambda, Expr.Coerce (this, typeof<obj>))) @@>
        | _ -> raise <| ArgumentException ()

let internal propertySetterCode (VM (modelType, moduleType, state)) = function
    | Observable (name, _, computedNames) -> function
        | [this; value] ->
            let fields =
                Type.recordFields modelType
                |> List.map (fun x -> Expr.PropertyGet (Expr.FieldGet (this, state), x))
                |> List.map (function
                    | PropertyGet (_, p, _) when p.Name = name -> value
                    | x -> x)

            let sequentialPropertyChanged = namesToSequentialPropertyChanged computedNames this

            <@@
            %%Expr.FieldSet (this, state, Expr.NewRecord (state.FieldType, fields))
            %%Expr.Call (mvxNotifyPropertyChanged this, raisePropertyChanged, [Expr.Value name])
            %%sequentialPropertyChanged
            () @@>
        | _ -> raise <| ArgumentException ()

    | Computed _ -> raise <| ArgumentException "Computed properties don't have setters."
    | Command _ -> raise <| ArgumentException "Command properties don't have setters."

let internal generateProperty vm prop =
    match prop with
    | Observable (name, t, _) ->
        ProvidedProperty (name, t, GetterCode = propertyGetterCode vm prop, SetterCode = propertySetterCode vm prop)

    | Computed (name, t) ->
        ProvidedProperty (name, t, GetterCode = propertyGetterCode vm prop)

    | Command (name, _) ->
        ProvidedProperty (name, typeof<ICommand>, GetterCode = propertyGetterCode vm prop)

let internal methodInvokeCode (VM (modelType, moduleType, state)) = function
    | MvxMethodInfo.Command (_, name) -> function
        | [this] ->
            let meth = moduleType.GetMethod name
            let changedNames =
                match Expr.TryGetReflectedDefinition (meth) with
                | None -> []
                | Some x -> changedFieldNames modelType x

            let sequentialPropertyChanged = namesToSequentialPropertyChanged changedNames this

            <@@
            %%Expr.FieldSet (this, state, Expr.Call (meth, [Expr.FieldGet (this, state)]))
            %%sequentialPropertyChanged 
            () @@>
        | _ -> raise <| ArgumentException ()

let internal generateMethod vm meth =
    match meth with
    | MvxMethodInfo.Command (name, _) ->
        ProvidedMethod (name, [], typeof<Void>, InvokeCode = methodInvokeCode vm meth) 

/// Generates a view-model
let internal generateViewModel vm =
    match vm with
    | VM (modelType, moduleType, state) ->

    // Get record fields on the model.
    let fields = Type.recordFields modelType

    // Get functions that are on the module.
    let init, funs =
        Type.moduleFunctions moduleType
        |> List.fold (fun (init, funs) x -> if x.Name = "init" then Some x, funs else init, x :: funs) (None, [])

    // See if we have a valid init function.
    let init = 
        match init with
        | None -> failwithf "Unable to resolve init function in module %s." moduleType.Name
        | Some x -> x

    // Get command methods based on if the functions in the module have a return type of the model.
    let cmdMeths =
        funs |> List.filter (fun x -> x.ReturnType = modelType)
        |> List.map (fun x -> MvxMethodInfo.Command (x.Name + "Fun", x.Name))

    // Get computeds based on if the functions in the module do not have a return type of the model.
    let comps =
        funs |> List.filter (fun x -> x.ReturnType <> modelType)
        |> List.map (fun x -> Computed (x.Name, x.ReturnType))

    // Structure that contains which functions use the model's fields that can be computed.
    // <method, fields>
    let compsMap = 
        comps
        |> List.fold (fun map -> function
            | Computed (name, _) ->
                match Expr.TryGetReflectedDefinition (moduleType.GetMethod (name)) with
                | None -> failwithf "Reflected defintion for function, %s, could not be found." name
                | Some x -> Map.add name (computedFieldNames vm.ModelType x) map
            | _ -> map) Map.empty<string, string list>

    // Get observables based on model fields and computed names.
    let observs =
        fields
        |> List.fold (fun observs x ->
            let computedNames =
                compsMap
                |> Map.fold (fun fields key -> function
                    | y when y |> List.exists (fun z -> z = x.Name) -> key :: fields
                    | _ -> fields) []
            Observable (x.Name, x.PropertyType, computedNames) :: observs) []

    // Generate methods based on command methods.
    let meths = cmdMeths |> List.map (generateMethod vm)

    // Get command properties based on the generated command methods.
    let cmds =
        List.map2 (fun x -> function
            | MvxMethodInfo.Command (name, moduleName) ->
                Command (moduleName, x)) meths cmdMeths

    // Generate constructor which sets the state field by calling the init function from the module.
    let ctor = ProvidedConstructor ([], InvokeCode = function
        | [this] -> Expr.FieldSet (this, state, Expr.Call (init, []))
        | _ -> raise <| ArgumentException ())
    let baseCtor = typeof<MvxViewModel>.GetConstructor (BindingFlags.NonPublic ||| BindingFlags.Instance, null, [||], null)
    ctor.BaseConstructorCall <- fun _ -> baseCtor, []

    // Generate properties.
    let props = observs @ comps @ cmds |> List.map (generateProperty vm)

    // Create view-model type definition.
    let vmp = ProvidedTypeDefinition (modelType.Name, Some typeof<MvxViewModel>, IsErased = false)
    vmp.SetAttributes (TypeAttributes.Public)
    vmp.AddMember state
    vmp.AddMember ctor
    vmp.AddMembers meths
    vmp.AddMembers props
    vmp

[<TypeProvider>]
type ViewModelTypeProvider (cfg: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces ()

    let asm = Assembly.GetExecutingAssembly ()
    let ns = this.GetType().Namespace
    let pn = "ViewModelProvider"

    let tempAsm = ProvidedAssembly (Path.ChangeExtension (Path.GetTempFileName (), ".dll"))

    let parameters = [
        ProvidedStaticParameter ("modelsAssembly", typeof<string>) ]

    do
        // THIS IS NECESSARY
        AppDomain.CurrentDomain.add_AssemblyResolve (fun _ args ->
            let name = System.Reflection.AssemblyName(args.Name)
            let existingAssembly = 
                System.AppDomain.CurrentDomain.GetAssemblies()
                |> Seq.tryFind(fun a -> System.Reflection.AssemblyName.ReferenceMatchesDefinition(name, a.GetName()))
            match existingAssembly with
            | Some a -> a
            | None -> null)
    
        let def = ProvidedTypeDefinition (asm, ns, pn, Some typeof<obj>, IsErased = false) 
        tempAsm.AddTypes [def]
        def.DefineStaticParameters (parameters, this.GenerateTypes)
        this.AddNamespace(ns, [def])

    /// FindModelsAssembly
    member internal this.FindModelsAssembly fileName =
        match cfg |> TypeProviderConfig.tryFindAssembly (fun fullPath -> Path.GetFileNameWithoutExtension fullPath = fileName) with
        | None -> failwithf "Invalid models assembly name %s. Pick from the list of referenced assemblies." fileName
        | Some masmFileName -> TypeProvider.loadAssemblyFile masmFileName

    /// GenerateTypes
    member internal this.GenerateTypes (typeName: string) (args: obj[]) =
        let modelsAssembly = args.[0] :?> string

        let masm = this.FindModelsAssembly modelsAssembly

        let def = ProvidedTypeDefinition (asm, ns, typeName, Some typeof<obj>, IsErased = false) 
        tempAsm.AddTypes [def]

        let types =
            Assembly.types masm
            |> List.filter (fun x -> FSharpType.IsRecord x)
            |> List.map (fun x ->
                let state = ProvidedField ("state", x)
                state.SetFieldAttributes (FieldAttributes.Private ||| FieldAttributes.InitOnly)
                { ModelType = x; ModuleType = moduleType x masm; State = state })

        def.AddMembersDelayed <| fun () -> 
            let defs = List.map generateViewModel types
            tempAsm.AddTypes defs
            defs

        def

[<assembly:TypeProviderAssembly>]
do ()
