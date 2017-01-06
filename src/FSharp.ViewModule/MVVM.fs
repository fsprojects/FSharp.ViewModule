(*
Copyright (c) 2013-2017 FSharp.ViewModule Team

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

namespace ViewModule.Internal

open System
open System.ComponentModel
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Threading
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open ViewModule
open ViewModule.Validation.FSharp

[<AbstractClass>]
type ViewModelUntyped() as self =
    let propertyChanged = new Event<_, _>()
    let depTracker = DependencyTracker(self.RaisePropertyChanged, propertyChanged.Publish)
    let dependencyTracker = depTracker :> IDependencyTracker

    // Used for error tracking
    let errorRelatedProperties = [ <@@ self.EntityErrors @@> ; <@@ self.PropertyErrors @@> ; <@@ self.IsValid @@> ]
    let errorRelatedPropertyNames = errorRelatedProperties |> List.map getPropertyNameFromExpression

    let errorsChanged = new Event<EventHandler<DataErrorsChangedEventArgs>, DataErrorsChangedEventArgs>()
    let errorTracker = ValidationTracker(self.RaiseErrorChanged, propertyChanged.Publish, self.Validate, errorRelatedProperties)
    let validationTracker = errorTracker :> IValidationTracker

    let getExecuting () = self.OperationExecuting
    let setExecuting b = self.OperationExecuting <- b
    
    let addCommandDependencies cmd dependentProperties =
        let deps : string list = defaultArg dependentProperties []
        deps |> List.iter (fun prop -> depTracker.AddCommandDependencyI(cmd, prop)) 

    let addCommandDependenciesString cmd = Array.iter (fun (prop : string) -> depTracker.AddCommandDependencyI(cmd, prop))
    let addCommandDependenciesLinq cmd = Array.iter (fun prop -> depTracker.AddCommandDependencyI(cmd, getPropertyNameFromLinqExpression prop))

    // TODO: This should be set by commands to allow disabling of other commands by default
    let propChanged : string -> unit = self.RaisePropertyChanged
    let operationExecuting = NotifyingValueBackingField(getPropertyNameFromExpression(<@ self.OperationExecuting @>), propChanged, false, validationTracker, (fun _ -> List.empty)) :> INotifyingValue<bool>
    let operationExecutingProp = getPropertyNameFromExpression <@ self.OperationExecuting @>

    let factory = ViewModelPropertyFactory(propChanged, addCommandDependencies, getExecuting, setExecuting, operationExecutingProp, validationTracker)

    // Overridable entity level validation
    abstract member Validate : string -> ValidationState seq
    default this.Validate(propertyName: string) =
        Seq.empty
            
    member private this.RaiseErrorChanged(propertyName : string) =
        errorsChanged.Trigger(this, new DataErrorsChangedEventArgs(propertyName))
        if (Option.isNone(errorRelatedPropertyNames |> List.tryFind ((=) propertyName))) then
            errorRelatedPropertyNames
            |> List.iter (fun p -> this.RaisePropertyChanged(p))

    member this.RaisePropertyChanged(propertyName : string) =
        propertyChanged.Trigger(this, new PropertyChangedEventArgs(propertyName))
    
    /// Value used to notify view that an asynchronous operation is executing
    member this.OperationExecuting with get() = operationExecuting.Value and set(value) = operationExecuting.Value <- value

    /// Handles management of dependencies for all computed properties 
    /// as well as ICommand dependencies
    member this.DependencyTracker = depTracker :> IDependencyTracker

    /// Manages tracking of validation information for the entity
    member this.ValidationTracker = errorTracker :> IValidationTracker

    member this.Factory = factory :> IViewModelPropertyFactory

    member this.IsValid with get() = not errorTracker.HasErrors

    member this.EntityErrors with get() = errorTracker.EntityErrors        
    member this.PropertyErrors with get() = errorTracker.PropertyErrors    
    member internal this.DependencyTrackerInternal with get() = depTracker    

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member this.PropertyChanged = propertyChanged.Publish

    interface IRaisePropertyChanged with
        member this.RaisePropertyChanged(propertyName: string) =
            this.RaisePropertyChanged(propertyName)

    interface INotifyDataErrorInfo with
        member this.GetErrors propertyName = 
            errorTracker.GetErrors(propertyName) :> System.Collections.IEnumerable

        member this.HasErrors with get() = errorTracker.HasErrors

        [<CLIEvent>]
        member this.ErrorsChanged = errorsChanged.Publish
    
    interface IViewModel with
        member this.OperationExecuting with get() = this.OperationExecuting and set(v) = this.OperationExecuting <- v
        member this.DependencyTracker = depTracker :> IDependencyTracker

namespace ViewModule.FSharp

open ViewModule
open ViewModule.Validation.FSharp

open System
open System.Threading

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

[<AutoOpen>]
module EventExtensions =
    type IRaisePropertyChanged with
        member x.RaisePropertyChanged(expr : Expr) = 
            x.RaisePropertyChanged(getPropertyNameFromExpression(expr))

    type IDependencyTracker with
        member x.AddPropertyDependency(prop : string, dependency : string) =
            (x :?> DependencyTracker).AddPropertyDependencyI(prop, dependency)

        member x.AddPropertyDependencies(prop : string, dependencies : string list) =
            (x :?> DependencyTracker).AddPropertyDependenciesI(prop, dependencies)

        member x.AddPropertyDependency(prop : Expr, dependency : Expr) =
            (x :?> DependencyTracker).AddPropertyDependencyI(getPropertyNameFromExpression prop, getPropertyNameFromExpression dependency)

        member x.AddPropertyDependencies(prop : Expr, dependencies : Expr list) =
            (x :?> DependencyTracker).AddPropertyDependenciesI(getPropertyNameFromExpression prop, dependencies |> List.map getPropertyNameFromExpression)

    type IViewModelPropertyFactory with
        member x.Backing(prop : Expr, defaultValue : 'a, validate : ValidationResult<'a> -> ValidationResult<'a>) =
            (x :?> ViewModelPropertyFactory).BackingI(getPropertyNameFromExpression prop, defaultValue, validate)

        member x.Backing(prop : Expr, defaultValue : 'a, ?validate : 'a -> string list) =
            (x :?> ViewModelPropertyFactory).BackingI(getPropertyNameFromExpression prop, defaultValue, ?validate = validate)

        member x.FromFuncs(prop : Expr, getter : unit -> 'a, setter : 'a -> unit) =
            (x :?> ViewModelPropertyFactory).FromFuncsI(getPropertyNameFromExpression prop, getter, setter)

        member x.CommandAsync (asyncWorfklow : SynchronizationContext -> Async<unit>, ?token : CancellationToken, ?onCancel : OperationCanceledException -> unit) =
            (x :?> ViewModelPropertyFactory).CommandAsyncI (asyncWorfklow, ?token = token, ?onCancel = onCancel)

        member x.CommandAsyncChecked (asyncWorkflow : SynchronizationContext -> Async<unit>, canExecute : unit -> bool, ?dependentProperties : Expr list,
                                      ?token : CancellationToken, ?onCancel : OperationCanceledException -> unit) =
            let deps = dependentProperties |> Option.map (List.map getPropertyNameFromExpression)
            (x :?> ViewModelPropertyFactory).CommandAsyncCheckedI(asyncWorkflow, canExecute, ?dependentProperties = deps, ?token = token, ?onCancel = onCancel) 

        member x.CommandAsyncParam (asyncWorkflow : SynchronizationContext -> 'a -> Async<unit>, ?token : CancellationToken,
                                    ?onCancel : OperationCanceledException -> unit) =
            (x :?> ViewModelPropertyFactory).CommandAsyncParamI(asyncWorkflow, ?token = token, ?onCancel = onCancel)

        member x.CommandAsyncParamChecked (asyncWorkflow : SynchronizationContext -> 'a -> Async<unit>, canExecute : 'a -> bool, ?dependentProperties : Expr list,
                                           ?token : CancellationToken, ?onCancel : OperationCanceledException -> unit) =
            let deps = dependentProperties |> Option.map (List.map getPropertyNameFromExpression)
            (x :?> ViewModelPropertyFactory).CommandAsyncParamCheckedI(asyncWorkflow, canExecute, ?dependentProperties = deps, ?token = token, ?onCancel = onCancel)

        member x.CommandSync (execute : unit -> unit) =
            (x :?> ViewModelPropertyFactory).CommandSyncI(execute)

        member x.CommandSyncParam (execute : 'a -> unit) =
            (x :?> ViewModelPropertyFactory).CommandSyncParamI(execute)

        member x.CommandSyncChecked (execute : unit -> unit, canExecute : unit -> bool, ?dependentProperties : Expr list) =
            let deps = dependentProperties |> Option.map (List.map getPropertyNameFromExpression)
            (x :?> ViewModelPropertyFactory).CommandSyncCheckedI(execute, canExecute, ?dependentProperties = deps)

        member x.CommandSyncParamChecked (execute : 'a -> unit, canExecute : 'a -> bool, ?dependentProperties : Expr list) =
            let deps = dependentProperties |> Option.map (List.map getPropertyNameFromExpression)
            (x :?> ViewModelPropertyFactory).CommandSyncParamCheckedI(execute, canExecute, ?dependentProperties = deps)


namespace ViewModule.CSharp

open ViewModule
open ViewModule.Validation.CSharp

open System
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

[<Extension>]
type Extensions =

    [<Extension>]
     static member RaisePropertyChanged(irpc : IRaisePropertyChanged, expr : System.Linq.Expressions.Expression) = 
            irpc.RaisePropertyChanged(getPropertyNameFromLinqExpression(expr))
    
    [<Extension>]
    static member AddPropertyDependency(dependencyTracker : IDependencyTracker, property : string, dependency : string) =
        (dependencyTracker :?> DependencyTracker).AddPropertyDependencyI(property, dependency)

    [<Extension>]
    static member AddPropertyDependencies(dependencyTracker : IDependencyTracker, property : string, dependency : string, [<ParamArray>] rest : string array) =
        (dependencyTracker :?> DependencyTracker).AddPropertyDependenciesI(property, dependency :: (List.ofArray rest))
    
    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : string, defaultValue : 'TProp) =
        (factory :?> ViewModelPropertyFactory).BackingI(property, defaultValue)

    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : string, defaultValue : 'TProp, validator : Validator<'TProp>) =
        let validate = match validator with Validator v -> v
        (factory :?> ViewModelPropertyFactory).BackingI(property, defaultValue, validate)

    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : string, defaultValue : 'TProp, validate : Func<'TProp, string seq>) =
        (factory :?> ViewModelPropertyFactory).BackingI(property, defaultValue, validate.Invoke >> List.ofSeq)

    [<Extension>]
    static member FromFuncs<'TProp> (factory : IViewModelPropertyFactory, property : string, getter : Func<'TProp>, setter : Action<'TProp>) =
        (factory :?> ViewModelPropertyFactory).FromFuncsI(property, getter.Invoke, setter.Invoke)
    
    [<Extension>]
    static member CommandSync (factory : IViewModelPropertyFactory, execute : Action) =
        (factory :?> ViewModelPropertyFactory).CommandSyncI(execute.Invoke)

    [<Extension>]
    static member CommandSyncParam<'TParam> (factory : IViewModelPropertyFactory, execute : Action<'TParam>) =
        (factory :?> ViewModelPropertyFactory).CommandSyncParamI(execute.Invoke)

    [<Extension>]
    static member CommandSyncChecked (factory : IViewModelPropertyFactory, execute : Action, canExecute : Func<bool>,
                                      [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelPropertyFactory).CommandSyncCheckedI(execute.Invoke, canExecute.Invoke, dependentProperties |> List.ofArray)

    [<Extension>]
    static member CommandSyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, execute : Action<'TParam>, canExecute : Func<'TParam, bool>,
                                                    [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelPropertyFactory).CommandSyncParamCheckedI(execute.Invoke, canExecute.Invoke, dependentProperties |> List.ofArray)
    
    [<Extension>]
    static member CommandAsync (factory : IViewModelPropertyFactory, createTask : Func<Task>) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncI(fun _ -> Async.fromTaskFunc createTask)

    [<Extension>]
    static member CommandAsync (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, token : CancellationToken) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncI((fun _ -> Async.fromTaskFuncCancellable createTask), token)
    
    [<Extension>]
    static member CommandAsync (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, token : CancellationToken,
                                onCancel : Action<OperationCanceledException>) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncI((fun _ -> Async.fromTaskFuncCancellable createTask), token, onCancel.Invoke)

    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<Task>, canExecute : Func<bool>,
                                       [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncCheckedI((fun _ -> Async.fromTaskFunc createTask), canExecute.Invoke, dependentProperties |> List.ofArray)

    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, canExecute : Func<bool>,
                                       token : CancellationToken, [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncCheckedI((fun _ -> Async.fromTaskFuncCancellable createTask), canExecute.Invoke,
                                                                    dependentProperties |> List.ofArray, token)

    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, canExecute : Func<bool>,
                                       token : CancellationToken, onCancel : Action<OperationCanceledException>, [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncCheckedI((fun _ -> Async.fromTaskFuncCancellable createTask), canExecute.Invoke,
                                                                    dependentProperties |> List.ofArray, token, onCancel.Invoke)

    [<Extension>]
    static member CommandAsyncParam<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, Task>) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncParamI(fun _ -> Async.fromTaskParamFunc createTask)
        
    [<Extension>]
    static member CommandAsyncParam<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>, token : CancellationToken) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncParamI((fun _ -> Async.FromTaskParamFuncCancellable createTask), token)
                
    [<Extension>]
    static member CommandAsyncParam<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>, token : CancellationToken,
                                              onCancel : Action<OperationCanceledException>) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncParamI((fun _ -> Async.FromTaskParamFuncCancellable createTask), token, onCancel.Invoke)

    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, Task>, canExecute : Func<'TParam, bool>,
                                                     [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncParamCheckedI((fun _ -> Async.fromTaskParamFunc createTask), canExecute.Invoke, dependentProperties |> List.ofArray)

    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>,
                                                     canExecute : Func<'TParam, bool>, token : CancellationToken, [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncParamCheckedI((fun _ -> Async.FromTaskParamFuncCancellable createTask), canExecute.Invoke,
                                                                         dependentProperties |> List.ofArray, token)

    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>,
                                                     canExecute : Func<'TParam, bool>, token : CancellationToken, onCancel : Action<OperationCanceledException>,
                                                     [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncParamCheckedI((fun _ -> Async.FromTaskParamFuncCancellable createTask), canExecute.Invoke,
                                                                         dependentProperties |> List.ofArray, token, onCancel.Invoke)
    

namespace ViewModule.CSharp.Expressions

open ViewModule
open ViewModule.Validation.CSharp

open System
open System.Linq.Expressions
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

type PropertyExpr = Expression<Func<obj>>

[<Extension>]
type Extensions =
    
    [<Extension>]
    static member AddPropertyDependency(dependencyTracker : IDependencyTracker, property : PropertyExpr, dependency : PropertyExpr) =
        let property'   = getPropertyNameFromLinqExpression property
        let dependency' = getPropertyNameFromLinqExpression dependency
        (dependencyTracker :?> DependencyTracker).AddPropertyDependencyI(property', dependency')

    [<Extension>]
    static member AddPropertyDependencies(dependencyTracker : IDependencyTracker, property : PropertyExpr, dependency : PropertyExpr,
                                          [<ParamArray>] rest : PropertyExpr array) =
        let property'     = getPropertyNameFromLinqExpression property
        let dependencies' = dependency :: (List.ofArray rest) |> List.map getPropertyNameFromLinqExpression
        (dependencyTracker :?> DependencyTracker).AddPropertyDependenciesI(property', dependencies')

    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : PropertyExpr, defaultValue : 'TProp) =
        (factory :?> ViewModelPropertyFactory).BackingI(getPropertyNameFromLinqExpression property, defaultValue)

    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : PropertyExpr, defaultValue : 'TProp, validator : Validator<'TProp>) =
        let validate = match validator with Validator v -> v
        (factory :?> ViewModelPropertyFactory).BackingI(getPropertyNameFromLinqExpression property, defaultValue, validate)

    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : PropertyExpr, defaultValue : 'TProp, validate : Func<'TProp, string seq>) =
        (factory :?> ViewModelPropertyFactory).BackingI(getPropertyNameFromLinqExpression property, defaultValue, validate.Invoke >> List.ofSeq)

    [<Extension>]
    static member FromFuncs<'TProp> (factory : IViewModelPropertyFactory, property : PropertyExpr, getter : Func<'TProp>, setter : Action<'TProp>) =
        (factory :?> ViewModelPropertyFactory).FromFuncsI(getPropertyNameFromLinqExpression property, getter.Invoke, setter.Invoke)
    
    [<Extension>]
    static member CommandSyncChecked (factory : IViewModelPropertyFactory, execute : Action, canExecute : Func<bool>,
                                      [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelPropertyFactory).CommandSyncCheckedI(execute.Invoke, canExecute.Invoke,
                                                                   dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression)

    [<Extension>]
    static member CommandSyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, execute : Action<'TParam>, canExecute : Func<'TParam, bool>,
                                                    [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelPropertyFactory).CommandSyncParamCheckedI(execute.Invoke, canExecute.Invoke,
                                                                        dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression)
    
    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<Task>, canExecute : Func<bool>,
                                       [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncCheckedI((fun _ -> Async.fromTaskFunc createTask), canExecute.Invoke,
                                                                    dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression)

    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, canExecute : Func<bool>,
                                       token : CancellationToken, [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncCheckedI((fun _ -> Async.fromTaskFuncCancellable createTask), canExecute.Invoke,
                                                                    dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression , token)

    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, canExecute : Func<bool>,
                                       token : CancellationToken, onCancel : Action<OperationCanceledException>,
                                       [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncCheckedI((fun _ -> Async.fromTaskFuncCancellable createTask), canExecute.Invoke,
                                                                    dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression, token,
                                                                    onCancel.Invoke)
    
    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, Task>, canExecute : Func<'TParam, bool>,
                                                     [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncParamCheckedI((fun _ -> Async.fromTaskParamFunc createTask), canExecute.Invoke,
                                                                         dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression)

    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>,
                                                     canExecute : Func<'TParam, bool>, token : CancellationToken, [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncParamCheckedI((fun _ -> Async.FromTaskParamFuncCancellable createTask), canExecute.Invoke,
                                                                         dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression, token)

    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>,
                                                     canExecute : Func<'TParam, bool>, token : CancellationToken, onCancel : Action<OperationCanceledException>,
                                                     [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelPropertyFactory).CommandAsyncParamCheckedI((fun _ -> Async.FromTaskParamFuncCancellable createTask), canExecute.Invoke,
                                                                         dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression,
                                                                         token, onCancel.Invoke)
    