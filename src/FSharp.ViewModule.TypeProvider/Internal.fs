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

module internal FSharp.ViewModule.TypeProviderInternal

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

open FSharp.ViewModule.Core
open FSharp.ViewModule.Helpers

/// Contains the association of model and module types.
type ViewModelInfo = { ModelType: Type; ModuleType: Type; State: FieldInfo }

/// Discriminated union for methods on the view-model.
type ViewModelMethodInfo =
    | Command of string * string

/// Discriminated union for types of vm properties
type ViewModelPropertyInfo =
    /// Property that has a raise property changed call in the setter.
    | Observable of string * Type * string list

    /// Property that returns a value based on observable(s).
    | Computed of string * Type

    /// Property that returns a vmCommand. Usually causes a side effect and/or new state for the model.
    | Command of string * MethodInfo

let (|VMC|) (vmc: IViewModuleTypeSpecification) = vmc.ViewModelType, vmc.CommandType

let (|VM|) (vm: ViewModelInfo) = vm.ModelType, vm.ModuleType, vm.State

let notifyPropertyChanged this = Expr.Coerce (this, typeof<IRaisePropertyChanged>)
let raisePropertyChanged = typeof<IRaisePropertyChanged>.GetMethod ("RaisePropertyChanged", [|typeof<string>|])
    
/// Gets the module that is associated with the given model type.
let moduleType (modelType: Type) asm =
    let moduleName = modelType.FullName + "Module"

    match Assembly.tryType moduleName asm with
    | None -> failwithf "%s not found." moduleName
    | Some x -> x

let computedFieldNames modelType expr =
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

let changedFieldNames modelType expr =
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

let namesToSequentialPropertyChanged names this =
    names |> List.map Expr.Value
    |> List.map (fun x -> Expr.Call (notifyPropertyChanged this, raisePropertyChanged, [x]))
    |> List.fold (fun expr x -> Expr.Sequential (expr, x)) (Expr.Value (()))

let propertyGetterCode (VM (modelType, moduleType, state)) (VMC (viewModelType, commandType)) = function
    | Observable (name, _, _) -> function
        | [this] -> Expr.PropertyGet (Expr.FieldGet(this, state), state.FieldType.GetProperty(name))
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
                    fun (arg : obj) ->
                    %%Expr.Call (Expr.Coerce (Expr.Var var, meth.DeclaringType), meth, []) 
                    () @@>)
            // TODO: This would be better stored as a field, and returned from the property, instead of generated each time
            Expr.NewObject(commandType.GetConstructor([| typeof<(obj -> unit)>; typeof<(obj -> bool)> |]), [ Expr.Application (lambda, Expr.Coerce (this, typeof<obj>)) ; <@ (fun (o:obj) -> true) @> ])
        | _ -> raise <| ArgumentException ()

let propertySetterCode (VM (modelType, moduleType, state)) = function
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
            %%Expr.Call (notifyPropertyChanged this, raisePropertyChanged, [Expr.Value name])
            %%sequentialPropertyChanged
            () @@>
        | _ -> raise <| ArgumentException ()

    | Computed _ -> raise <| ArgumentException "Computed properties don't have setters."
    | Command _ -> raise <| ArgumentException "Command properties don't have setters."

let generateProperty vm vmc prop =
    match prop with
    | Observable (name, t, _) ->
        ProvidedProperty (name, t, GetterCode = propertyGetterCode vm vmc prop, SetterCode = propertySetterCode vm prop)

    | Computed (name, t) ->
        ProvidedProperty (name, t, GetterCode = propertyGetterCode vm vmc prop)

    | Command (name, _) ->
        ProvidedProperty (name, typeof<ICommand>, GetterCode = propertyGetterCode vm vmc prop)

let methodInvokeCode (VM (modelType, moduleType, state)) = function
    | ViewModelMethodInfo.Command (_, name) -> function
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

// TODO: Technically, we don't need a method at all.  We can just build the command (as a field)
// straight from the Module (or a wrapper method around it) and expose that field via a property
let generateMethod vm meth =
    match meth with
    | ViewModelMethodInfo.Command (name, _) ->
        let meth = ProvidedMethod (name, [], typeof<Void>, InvokeCode = methodInvokeCode vm meth)         
        meth

/// Generates a view-model
let generateViewModel vm (vmc : IViewModuleTypeSpecification) =
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
        |> List.map (fun x -> ViewModelMethodInfo.Command (x.Name + "Fun", x.Name))

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
            | ViewModelMethodInfo.Command (name, moduleName) ->
                Command (moduleName, x)) meths cmdMeths
                
    // Generate constructor which sets the state field by calling the init function from the module.
    let ctor = ProvidedConstructor ([], InvokeCode = function
        | [this] -> Expr.FieldSet (this, state, Expr.Call (init, []))
        | _ -> raise <| ArgumentException ())
    let baseCtor = vmc.ViewModelType.GetConstructor (BindingFlags.Public ||| BindingFlags.Instance, null, [||], null)
    ctor.BaseConstructorCall <- fun _ -> baseCtor, []

    // Generate properties.
    let props = observs @ comps @ cmds |> List.map (generateProperty vm vmc)

    // Create view-model type definition.
    let vmp = ProvidedTypeDefinition (modelType.Name, Some vmc.ViewModelType, IsErased = false)
    vmp.SetAttributes (TypeAttributes.Public)
    vmp.AddMember state
    vmp.AddMember ctor
    vmp.AddMembers meths
    vmp.AddMembers props
    vmp