(*
Copyright (c) 2013-2015 FSharp.ViewModule Team

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

namespace ViewModule

open System
open System.ComponentModel
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Threading
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open ViewModule.Validation.FSharp

/// Encapsulation of a value which handles raising property changed automatically in a clean manner
type public INotifyingValue<'a> =
    inherit IObservable<'a>
    /// Extracts the current value from the backing storage
    abstract member Value : 'a with get, set

type public NotifyingValue<'a>(defaultValue) =
    let mutable value = defaultValue
    let ev = Event<'a>()
        
    member __.Value 
        with get() = value 
        and set(v) = 
            if (not (EqualityComparer<'a>.Default.Equals(value, v))) then
                value <- v
                ev.Trigger v

    interface IObservable<'a> with
        member this.Subscribe observer =
            let obs = ev.Publish :> IObservable<'a>
            obs.Subscribe observer

    interface INotifyingValue<'a> with
        member this.Value with get() = this.Value and set(v) = this.Value <- v    

/// This interface is a tag for implementation of extension methods. Do not implement externally. 
type IViewModelPropertyFactory =
    interface end

    // The only type which should implement this interface is ViewModelUntyped. Therefore, the implementation can be cast to this type
    // and used to access the following internal members:
    //
    // BackingI<'a> : prop: string * defaultValue:'a * ?validate:('a -> string list) -> INotifyingValue<'a>
    // BackingI<'a> : prop: string * defaultValue:'a * validate:(ValidationResult<'a> -> ValidationResult<'a>) -> INotifyingValue<'a>
    // FromFuncsI<'a> : prop: string * getter:(unit -> 'a) * setter: ('a -> unit) -> INotifyingValue<'a>
    // 
    // CommandAsyncI : asyncWorkflow:(SynchronizationContext -> Async<unit>) * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand
    // CommandAsyncCheckedI : asyncWorkflow:(SynchronizationContext -> Async<unit>) * canExecute:(unit -> bool) * ?dependentProperties: string list * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand
    // CommandAsyncParamI<'a> : asyncWorkflow:(SynchronizationContext -> 'a -> Async<unit>) * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand
    // CommandAsyncParamCheckedI<'a> : asyncWorkflow:(SynchronizationContext -> 'a -> Async<unit>) * canExecute:('a -> bool) * ?dependentProperties: string list * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand    
    // 
    // CommandSyncI : execute:(unit -> unit) -> INotifyCommand
    // CommandSyncParamI<'a> : execute:('a -> unit) -> INotifyCommand
    // CommandSyncCheckedI : execute:(unit -> unit) * canExecute:(unit -> bool) * ?dependentProperties: string list -> INotifyCommand
    // CommandSyncParamCheckedI<'a> : execute:('a -> unit) * canExecute:('a -> bool) * ?dependentProperties: string list -> INotifyCommand    
    

    // The F# API is implemented with members having the following signatures via extension methods in the ViewModule.FSharp namespace:
    // (note that string property names are replaced by quotation expressions of type Expr)
    //
    // abstract Backing<'a> : prop:Expr * defaultValue:'a * validate:(ValidationResult<'a> -> ValidationResult<'a>) -> INotifyingValue<'a>
    // abstract Backing<'a> : prop:Expr * defaultValue:'a * ?validate:('a -> Expr list) -> INotifyingValue<'a>
    // abstract FromFuncs<'a> : prop:Expr * getter:(unit->'a) * setter: ('a->unit) -> INotifyingValue<'a>
    //
    // CommandAsync : asyncWorkflow:(SynchronizationContext -> Async<unit>) * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand
    // CommandAsyncChecked : asyncWorkflow:(SynchronizationContext -> Async<unit>) * canExecute:(unit -> bool) * ?dependentProperties: Expr list * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand
    // CommandAsyncParam<'a> : asyncWorkflow:(SynchronizationContext -> 'a -> Async<unit>) * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand
    // CommandAsyncParamChecked<'a> : asyncWorkflow:(SynchronizationContext -> 'a -> Async<unit>) * canExecute:('a -> bool) * ?dependentProperties: Expr list * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand    
    // 
    // CommandSync : execute:(unit -> unit) -> INotifyCommand
    // CommandSyncParam<'a> : execute:('a -> unit) -> INotifyCommand
    // CommandSyncChecked : execute:(unit -> unit) * canExecute:(unit -> bool) * ?dependentProperties: Expr list -> INotifyCommand
    // CommandSyncParamChecked<'a> : execute:('a -> unit) * canExecute:('a -> bool) * ?dependentProperties: Expr list -> INotifyCommand    


    // The C# API is implemented with members having the following signatures via extension methods in the ViewModule.CSharp namespace:
    //
    // Backing<'TProp> : property : string * defaultValue:'TProp -> INotifyingValue<'TProp>
    // Backing<'TProp> : property : string * defaultValue:'TProp * validate : Validator<'TProp> -> INotifyingValue<'TProp>
    // Backing<'TProp> : property : string * defaultValue:'TProp * validate: Func<'TProp, string seq> -> INotifyingValue<'TProp>
    // FromFuncs<'TProp> : property : string * getter : Func<'TProp> * setter: Action<'TProp> -> INotifyingValue<'TProp>
    // 
    // CommandSync : execute : Action -> INotifyCommand
    // CommandSyncParam<'TParam> : execute : Action<'TParam> -> INotifyCommand
    // CommandSyncChecked : execute : Action * canExecute : Func<bool> * [<ParamArray>] dependentProperties: string array -> INotifyCommand
    // CommandSyncParamChecked<'TParam> : execute : Action<'TParam> * canExecute : Func<'TParam, bool> * [<ParamArray>] dependentProperties : string array -> INotifyCommand  
    // 
    // CommandAsync : createTask : Func<Task> -> IAsyncNotifyCommand
    // CommandAsync : createTask : Func<CancellationToken, Task> * token : CancellationToken -> IAsyncNotifyCommand
    // CommandAsync : createTask : Func<CancellationToken, Task> * token : CancellationToken * onCancel : Action<OperationCanceledException> -> IAsyncNotifyCommand
    // CommandAsyncChecked : createTask : Func<Task> * canExecute: Func<bool> * [<ParamArray>] dependentProperties : string array -> IAsyncNotifyCommand
    // CommandAsyncChecked : createTask : Func<CancellationToken, Task> * canExecute: Func<bool> * token : CancellationToken * [<ParamArray>] dependentProperties : string array -> IAsyncNotifyCommand
    // CommandAsyncChecked : createTask : Func<CancellationToken, Task> * canExecute: Func<bool> * token : CancellationToken * onCancel : Action<OperationCanceledException> * [<ParamArray>] dependentProperties : string array -> IAsyncNotifyCommand
    // CommandAsyncParam<'TParam> : createTask : Func<'TParam, Task> -> IAsyncNotifyCommand
    // CommandAsyncParam<'TParam> : createTask : Func<'TParam, CancellationToken, Task> * token : CancellationToken -> IAsyncNotifyCommand
    // CommandAsyncParam<'TParam> : createTask : Func<'TParam, CancellationToken, Task> * token : CancellationToken * onCancel : Action<OperationCanceledException> -> IAsyncNotifyCommand
    // CommandAsyncParamChecked<'TParam> : createTask : Func<'TParam, Task> * canExecute : Func<'TParam, bool> * [<ParamArray>] dependentProperties : string array -> IAsyncNotifyCommand    
    // CommandAsyncParamChecked<'TParam> : createTask : Func<'TParam, CancellationToken, Task> * canExecute : Func<'TParam, bool> * token : CancellationToken * [<ParamArray>] dependentProperties : string array -> IAsyncNotifyCommand    
    // CommandAsyncParamChecked<'TParam> : createTask : Func<'TParam, CancellationToken, Task> * canExecute : Func<'TParam, bool> * token : CancellationToken * onCancel : Action<OperationCanceledException> * [<ParamArray>] dependentProperties : string array -> IAsyncNotifyCommand    
    //
    //
    // In addition, the following extension methods are implemented in the namespace ViewModule.CSharp.Expressions
    // to support LINQ Expression trees for C# versions which do not have nameof:
    //
    // Backing<'TProp> : property : Expression<Func<'TProp>> * defaultValue:'TProp -> INotifyingValue<'TProp>
    // Backing<'TProp> : property : Expression<Func<'TProp>> * defaultValue:'TProp * validate : Validator<'TProp> -> INotifyingValue<'TProp>
    // Backing<'TProp> : property : Expression<Func<'TProp>> * defaultValue:'TProp * validate: Func<'TProp, string seq> -> INotifyingValue<'TProp>
    // FromFuncs<'TProp> : property : Expression<Func<'TProp>> * getter : Func<'TProp> * setter: Action<'TProp> -> INotifyingValue<'TProp>
    //
    // CommandSyncChecked : execute : Action * canExecute : Func<bool> * [<ParamArray>] dependentProperties: Expression<Func<obj>> array -> INotifyCommand
    // CommandSyncParamChecked<'TParam> : execute : Action<'TParam> * canExecute : Func<'TParam, bool> * [<ParamArray>] dependentProperties : Expression<Func<obj>> array -> INotifyCommand    
    //
    // CommandAsyncChecked : createTask : Func<Task> * canExecute: Func<bool> * [<ParamArray>] dependentProperties : Expression<Func<obj>> array -> IAsyncNotifyCommand
    // CommandAsyncChecked : createTask : Func<CancellationToken, Task> * canExecute: Func<bool> * token : CancellationToken * [<ParamArray>] dependentProperties : Expression<Func<obj>> array -> IAsyncNotifyCommand
    // CommandAsyncChecked : createTask : Func<CancellationToken, Task> * canExecute: Func<bool> * token : CancellationToken * onCancel : Action<OperationCanceledException> * [<ParamArray>] dependentProperties : Expression<Func<obj>> array -> IAsyncNotifyCommand
    // CommandAsyncParamChecked<'TParam> : createTask : Func<'TParam, Task> * canExecute : Func<'TParam, bool> * [<ParamArray>] dependentProperties : Expression<Func<obj>> array -> IAsyncNotifyCommand    
    // CommandAsyncParamChecked<'TParam> : createTask : Func<'TParam, CancellationToken, Task> * canExecute : Func<'TParam, bool> * token : CancellationToken * [<ParamArray>] dependentProperties : Expression<Func<obj>> array -> IAsyncNotifyCommand    
    // CommandAsyncParamChecked<'TParam> : createTask : Func<'TParam, CancellationToken, Task> * canExecute : Func<'TParam, bool> * token : CancellationToken * onCancel : Action<OperationCanceledException> * [<ParamArray>] dependentProperties : Expression<Func<obj>> array -> IAsyncNotifyCommand 

type internal NotifyingValueBackingField<'a> (propertyName, raisePropertyChanged : string -> unit, storage : NotifyingValue<'a>, validationResultPublisher : IValidationTracker, validate : 'a -> string list) =    
    let value = storage
    
    let updateValidation () =
        validate value.Value
    
    do
        value.Add (fun _ -> raisePropertyChanged propertyName)

        validationResultPublisher.AddPropertyValidationWatcher propertyName updateValidation
        if (SynchronizationContext.Current <> null) then
            SynchronizationContext.Current.Post((fun _ -> validationResultPublisher.Revalidate propertyName), null)

    new(propertyName, raisePropertyChanged : string -> unit, defaultValue : 'a, validationResultPublisher, validate) =
        NotifyingValueBackingField<'a>(propertyName, raisePropertyChanged, NotifyingValue<_>(defaultValue), validationResultPublisher, validate)

    member __.Value 
        with get() = value.Value
        and set(v) = value.Value <- v

    interface IObservable<'a> with
        member this.Subscribe observer = (value :> IObservable<'a>).Subscribe observer

    interface INotifyingValue<'a> with
        member this.Value with get() = this.Value and set(v) = this.Value <- v

type internal NotifyingValueFuncs<'a> (propertyName, raisePropertyChanged : string -> unit, getter, setter) =
    let propertyName = propertyName
    let ev = Event<'a>()

    do
        ev.Publish.Add (fun _ -> raisePropertyChanged propertyName)
    
    member this.Value 
        with get() = getter()
        and set(v) = 
            if (not (EqualityComparer<'a>.Default.Equals(getter(), v))) then
                setter v
                ev.Trigger v
        
    interface IObservable<'a> with
        member this.Subscribe observer =
            let obs = ev.Publish :> IObservable<'a>
            obs.Subscribe observer

    interface INotifyingValue<'a> with
        member this.Value with get() = this.Value and set(v) = this.Value <- v

namespace ViewModule.Internal

open System
open System.ComponentModel
open System.Threading

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

    interface IViewModelPropertyFactory
    /// members associated with IViewModelPropertyFactory which is just an interface tag

    member internal this.BackingI (prop : string, defaultValue : 'a, validate : ValidationResult<'a> -> ValidationResult<'a>) =
        let validateFun = Validators.validate(prop) >> validate >> result
        NotifyingValueBackingField<'a>(prop, propChanged, defaultValue, validationTracker, validateFun) :> INotifyingValue<'a>

    member internal this.BackingI (prop : string, defaultValue : 'a, ?validate : 'a -> string list) =
        let validateFun = defaultArg validate (fun _ -> [])
        NotifyingValueBackingField<'a>(prop, propChanged, defaultValue, validationTracker, validateFun) :> INotifyingValue<'a>

    member internal this.FromFuncsI (prop : string, getter, setter) =
        NotifyingValueFuncs<'a>(prop, propChanged, getter, setter) :> INotifyingValue<'a>

    member internal this.CommandAsyncI(asyncWorkflow, ?token, ?onCancel) =
        let ct = defaultArg token CancellationToken.None
        let oc = defaultArg onCancel ignore
        let cmd = Commands.createAsyncInternal asyncWorkflow getExecuting setExecuting (fun () -> true) ct oc
        let opEx = Some [ getPropertyNameFromExpression <@@ this.OperationExecuting @@> ]
        addCommandDependencies cmd opEx
        cmd

    member internal this.CommandAsyncCheckedI(asyncWorkflow, canExecute, ?dependentProperties: string list, ?token, ?onCancel) =
        let ct = defaultArg token CancellationToken.None
        let oc = defaultArg onCancel ignore
        let cmd = Commands.createAsyncInternal asyncWorkflow getExecuting setExecuting canExecute ct oc
        let opEx = Some [ getPropertyNameFromExpression <@@ this.OperationExecuting @@> ]
        addCommandDependencies cmd opEx
        addCommandDependencies cmd dependentProperties
        cmd

    member internal this.CommandAsyncParamI(asyncWorkflow, ?token, ?onCancel) =
        let ct = defaultArg token CancellationToken.None
        let oc = defaultArg onCancel ignore
        let cmd = Commands.createAsyncParamInternal asyncWorkflow getExecuting setExecuting (fun _ -> true) ct oc
        let opEx = Some [ getPropertyNameFromExpression <@@ this.OperationExecuting @@> ]
        addCommandDependencies cmd opEx
        cmd

    member internal this.CommandAsyncParamCheckedI(asyncWorkflow, canExecute, ?dependentProperties: string list, ?token, ?onCancel) =
        let ct = defaultArg token CancellationToken.None
        let oc = defaultArg onCancel ignore
        let cmd = Commands.createAsyncParamInternal asyncWorkflow getExecuting setExecuting canExecute ct oc
        let opEx = Some [ getPropertyNameFromExpression <@@ this.OperationExecuting @@> ]
        addCommandDependencies cmd opEx
        addCommandDependencies cmd dependentProperties
        cmd

    member internal this.CommandSyncI(execute) =
        let cmd = Commands.createSyncInternal execute (fun () -> true)
        cmd

    member internal this.CommandSyncCheckedI(execute, canExecute, ?dependentProperties: string list) =
        let cmd = Commands.createSyncInternal execute canExecute
        addCommandDependencies cmd dependentProperties
        cmd

    member internal this.CommandSyncParamI(execute) =
        let cmd = Commands.createSyncParamInternal execute (fun _ -> true)
        cmd

    member internal this.CommandSyncParamCheckedI(execute, canExecute, ?dependentProperties: string list) =
        let cmd = Commands.createSyncParamInternal execute canExecute
        addCommandDependencies cmd dependentProperties
        cmd

namespace ViewModule.FSharp

open ViewModule
open ViewModule.Internal
open ViewModule.Validation.FSharp

open System
open System.Threading

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

[<AutoOpen>]
module EventExtensions =

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
            (x :?> ViewModelUntyped).BackingI(getPropertyNameFromExpression prop, defaultValue, validate)

        member x.Backing(prop : Expr, defaultValue : 'a, ?validate : 'a -> string list) =
            (x :?> ViewModelUntyped).BackingI(getPropertyNameFromExpression prop, defaultValue, ?validate = validate)

        member x.FromFuncs(prop : Expr, getter : unit -> 'a, setter : 'a -> unit) =
            (x :?> ViewModelUntyped).FromFuncsI(getPropertyNameFromExpression prop, getter, setter)

        member x.CommandAsync (asyncWorfklow : SynchronizationContext -> Async<unit>, ?token : CancellationToken, ?onCancel : OperationCanceledException -> unit) =
            (x :?> ViewModelUntyped).CommandAsyncI (asyncWorfklow, ?token = token, ?onCancel = onCancel)

        member x.CommandAsyncChecked (asyncWorkflow : SynchronizationContext -> Async<unit>, canExecute : unit -> bool, ?dependentProperties : Expr list,
                                      ?token : CancellationToken, ?onCancel : OperationCanceledException -> unit) =
            let deps = dependentProperties |> Option.map (List.map getPropertyNameFromExpression)
            (x :?> ViewModelUntyped).CommandAsyncCheckedI(asyncWorkflow, canExecute, ?dependentProperties = deps, ?token = token, ?onCancel = onCancel) 

        member x.CommandAsyncParam (asyncWorkflow : SynchronizationContext -> 'a -> Async<unit>, ?token : CancellationToken,
                                    ?onCancel : OperationCanceledException -> unit) =
            (x :?> ViewModelUntyped).CommandAsyncParamI(asyncWorkflow, ?token = token, ?onCancel = onCancel)

        member x.CommandAsyncParamChecked (asyncWorkflow : SynchronizationContext -> 'a -> Async<unit>, canExecute : 'a -> bool, ?dependentProperties : Expr list,
                                           ?token : CancellationToken, ?onCancel : OperationCanceledException -> unit) =
            let deps = dependentProperties |> Option.map (List.map getPropertyNameFromExpression)
            (x :?> ViewModelUntyped).CommandAsyncParamCheckedI(asyncWorkflow, canExecute, ?dependentProperties = deps, ?token = token, ?onCancel = onCancel)

        member x.CommandSync (execute : unit -> unit) =
            (x :?> ViewModelUntyped).CommandSyncI(execute)

        member x.CommandSyncParam (execute : 'a -> unit) =
            (x :?> ViewModelUntyped).CommandSyncParamI(execute)

        member x.CommandSyncChecked (execute : unit -> unit, canExecute : unit -> bool, ?dependentProperties : Expr list) =
            let deps = dependentProperties |> Option.map (List.map getPropertyNameFromExpression)
            (x :?> ViewModelUntyped).CommandSyncCheckedI(execute, canExecute, ?dependentProperties = deps)

        member x.CommandSyncParamChecked (execute : 'a -> unit, canExecute : 'a -> bool, ?dependentProperties : Expr list) =
            let deps = dependentProperties |> Option.map (List.map getPropertyNameFromExpression)
            (x :?> ViewModelUntyped).CommandSyncParamCheckedI(execute, canExecute, ?dependentProperties = deps)


namespace ViewModule.CSharp

open ViewModule
open ViewModule.Internal
open ViewModule.Validation.CSharp

open System
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

[<Extension>]
type Extensions =
    
    [<Extension>]
    static member AddPropertyDependency(dependencyTracker : IDependencyTracker, property : string, dependency : string) =
        (dependencyTracker :?> DependencyTracker).AddPropertyDependencyI(property, dependency)

    [<Extension>]
    static member AddPropertyDependencies(dependencyTracker : IDependencyTracker, property : string, dependency : string, [<ParamArray>] rest : string array) =
        (dependencyTracker :?> DependencyTracker).AddPropertyDependenciesI(property, dependency :: (List.ofArray rest))
    
    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : string, defaultValue : 'TProp) =
        (factory :?> ViewModelUntyped).BackingI(property, defaultValue)

    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : string, defaultValue : 'TProp, validator : Validator<'TProp>) =
        let validate = match validator with Validator v -> v
        (factory :?> ViewModelUntyped).BackingI(property, defaultValue, validate)

    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : string, defaultValue : 'TProp, validate : Func<'TProp, string seq>) =
        (factory :?> ViewModelUntyped).BackingI(property, defaultValue, validate.Invoke >> List.ofSeq)

    [<Extension>]
    static member FromFuncs<'TProp> (factory : IViewModelPropertyFactory, property : string, getter : Func<'TProp>, setter : Action<'TProp>) =
        (factory :?> ViewModelUntyped).FromFuncsI(property, getter.Invoke, setter.Invoke)
    
    [<Extension>]
    static member CommandSync (factory : IViewModelPropertyFactory, execute : Action) =
        (factory :?> ViewModelUntyped).CommandSyncI(execute.Invoke)

    [<Extension>]
    static member CommandSyncParam<'TParam> (factory : IViewModelPropertyFactory, execute : Action<'TParam>) =
        (factory :?> ViewModelUntyped).CommandSyncParamI(execute.Invoke)

    [<Extension>]
    static member CommandSyncChecked (factory : IViewModelPropertyFactory, execute : Action, canExecute : Func<bool>,
                                      [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelUntyped).CommandSyncCheckedI(execute.Invoke, canExecute.Invoke, dependentProperties |> List.ofArray)

    [<Extension>]
    static member CommandSyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, execute : Action<'TParam>, canExecute : Func<'TParam, bool>,
                                                    [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelUntyped).CommandSyncParamCheckedI(execute.Invoke, canExecute.Invoke, dependentProperties |> List.ofArray)
    
    [<Extension>]
    static member CommandAsync (factory : IViewModelPropertyFactory, createTask : Func<Task>) =
        (factory :?> ViewModelUntyped).CommandAsyncI(fun _ -> Async.fromTaskFunc createTask)

    [<Extension>]
    static member CommandAsync (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, token : CancellationToken) =
        (factory :?> ViewModelUntyped).CommandAsyncI((fun _ -> Async.fromTaskFuncCancellable createTask), token)
    
    [<Extension>]
    static member CommandAsync (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, token : CancellationToken,
                                onCancel : Action<OperationCanceledException>) =
        (factory :?> ViewModelUntyped).CommandAsyncI((fun _ -> Async.fromTaskFuncCancellable createTask), token, onCancel.Invoke)

    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<Task>, canExecute : Func<bool>,
                                       [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelUntyped).CommandAsyncCheckedI((fun _ -> Async.fromTaskFunc createTask), canExecute.Invoke, dependentProperties |> List.ofArray)

    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, canExecute : Func<bool>,
                                       token : CancellationToken, [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelUntyped).CommandAsyncCheckedI((fun _ -> Async.fromTaskFuncCancellable createTask), canExecute.Invoke,
                                                            dependentProperties |> List.ofArray, token)

    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, canExecute : Func<bool>,
                                       token : CancellationToken, onCancel : Action<OperationCanceledException>, [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelUntyped).CommandAsyncCheckedI((fun _ -> Async.fromTaskFuncCancellable createTask), canExecute.Invoke,
                                                            dependentProperties |> List.ofArray, token, onCancel.Invoke)

    [<Extension>]
    static member CommandAsyncParam<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, Task>) =
        (factory :?> ViewModelUntyped).CommandAsyncParamI(fun _ -> Async.fromTaskParamFunc createTask)
        
    [<Extension>]
    static member CommandAsyncParam<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>, token : CancellationToken) =
        (factory :?> ViewModelUntyped).CommandAsyncParamI((fun _ -> Async.FromTaskParamFuncCancellable createTask), token)
                
    [<Extension>]
    static member CommandAsyncParam<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>, token : CancellationToken,
                                              onCancel : Action<OperationCanceledException>) =
        (factory :?> ViewModelUntyped).CommandAsyncParamI((fun _ -> Async.FromTaskParamFuncCancellable createTask), token, onCancel.Invoke)

    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, Task>, canExecute : Func<'TParam, bool>,
                                                     [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelUntyped).CommandAsyncParamCheckedI((fun _ -> Async.fromTaskParamFunc createTask), canExecute.Invoke, dependentProperties |> List.ofArray)

    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>,
                                                     canExecute : Func<'TParam, bool>, token : CancellationToken, [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelUntyped).CommandAsyncParamCheckedI((fun _ -> Async.FromTaskParamFuncCancellable createTask), canExecute.Invoke,
                                                                 dependentProperties |> List.ofArray, token)

    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>,
                                                     canExecute : Func<'TParam, bool>, token : CancellationToken, onCancel : Action<OperationCanceledException>,
                                                     [<ParamArray>] dependentProperties : string array) =
        (factory :?> ViewModelUntyped).CommandAsyncParamCheckedI((fun _ -> Async.FromTaskParamFuncCancellable createTask), canExecute.Invoke,
                                                                 dependentProperties |> List.ofArray, token, onCancel.Invoke)
    

namespace ViewModule.CSharp.Expressions

open ViewModule
open ViewModule.Internal
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
        (factory :?> ViewModelUntyped).BackingI(getPropertyNameFromLinqExpression property, defaultValue)

    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : PropertyExpr, defaultValue : 'TProp, validator : Validator<'TProp>) =
        let validate = match validator with Validator v -> v
        (factory :?> ViewModelUntyped).BackingI(getPropertyNameFromLinqExpression property, defaultValue, validate)

    [<Extension>]
    static member Backing<'TProp> (factory : IViewModelPropertyFactory, property : PropertyExpr, defaultValue : 'TProp, validate : Func<'TProp, string seq>) =
        (factory :?> ViewModelUntyped).BackingI(getPropertyNameFromLinqExpression property, defaultValue, validate.Invoke >> List.ofSeq)

    [<Extension>]
    static member FromFuncs<'TProp> (factory : IViewModelPropertyFactory, property : PropertyExpr, getter : Func<'TProp>, setter : Action<'TProp>) =
        (factory :?> ViewModelUntyped).FromFuncsI(getPropertyNameFromLinqExpression property, getter.Invoke, setter.Invoke)
    
    [<Extension>]
    static member CommandSyncChecked (factory : IViewModelPropertyFactory, execute : Action, canExecute : Func<bool>,
                                      [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelUntyped).CommandSyncCheckedI(execute.Invoke, canExecute.Invoke,
                                                           dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression)

    [<Extension>]
    static member CommandSyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, execute : Action<'TParam>, canExecute : Func<'TParam, bool>,
                                                    [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelUntyped).CommandSyncParamCheckedI(execute.Invoke, canExecute.Invoke,
                                                                dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression)
    
    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<Task>, canExecute : Func<bool>,
                                       [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelUntyped).CommandAsyncCheckedI((fun _ -> Async.fromTaskFunc createTask), canExecute.Invoke,
                                                            dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression)

    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, canExecute : Func<bool>,
                                       token : CancellationToken, [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelUntyped).CommandAsyncCheckedI((fun _ -> Async.fromTaskFuncCancellable createTask), canExecute.Invoke,
                                                            dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression , token)

    [<Extension>]
    static member CommandAsyncChecked (factory : IViewModelPropertyFactory, createTask : Func<CancellationToken, Task>, canExecute : Func<bool>,
                                       token : CancellationToken, onCancel : Action<OperationCanceledException>,
                                       [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelUntyped).CommandAsyncCheckedI((fun _ -> Async.fromTaskFuncCancellable createTask), canExecute.Invoke,
                                                            dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression, token,
                                                            onCancel.Invoke)
    
    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, Task>, canExecute : Func<'TParam, bool>,
                                                     [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelUntyped).CommandAsyncParamCheckedI((fun _ -> Async.fromTaskParamFunc createTask), canExecute.Invoke,
                                                                 dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression)

    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>,
                                                     canExecute : Func<'TParam, bool>, token : CancellationToken, [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelUntyped).CommandAsyncParamCheckedI((fun _ -> Async.FromTaskParamFuncCancellable createTask), canExecute.Invoke,
                                                                 dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression, token)

    [<Extension>]
    static member CommandAsyncParamChecked<'TParam> (factory : IViewModelPropertyFactory, createTask : Func<'TParam, CancellationToken, Task>,
                                                     canExecute : Func<'TParam, bool>, token : CancellationToken, onCancel : Action<OperationCanceledException>,
                                                     [<ParamArray>] dependentProperties : PropertyExpr array) =
        (factory :?> ViewModelUntyped).CommandAsyncParamCheckedI((fun _ -> Async.FromTaskParamFuncCancellable createTask), canExecute.Invoke,
                                                                 dependentProperties |> List.ofArray |> List.map getPropertyNameFromLinqExpression,
                                                                 token, onCancel.Invoke)
    