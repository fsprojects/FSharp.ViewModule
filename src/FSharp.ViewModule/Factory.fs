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

type internal ViewModelPropertyFactory(propChanged : string -> unit, addCommandDependencies : INotifyCommand -> string list option -> unit, 
                                       getExecuting : unit -> bool, setExecuting : bool -> unit, operationExecutingProp : string,
                                       validationTracker : IValidationTracker) =

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
        let opEx = Some [ operationExecutingProp ]
        addCommandDependencies cmd opEx
        cmd

    member internal this.CommandAsyncCheckedI(asyncWorkflow, canExecute, ?dependentProperties: string list, ?token, ?onCancel) =
        let ct = defaultArg token CancellationToken.None
        let oc = defaultArg onCancel ignore
        let cmd = Commands.createAsyncInternal asyncWorkflow getExecuting setExecuting canExecute ct oc
        let opEx = Some [ operationExecutingProp ]
        addCommandDependencies cmd opEx
        addCommandDependencies cmd dependentProperties
        cmd

    member internal this.CommandAsyncParamI(asyncWorkflow, ?token, ?onCancel) =
        let ct = defaultArg token CancellationToken.None
        let oc = defaultArg onCancel ignore
        let cmd = Commands.createAsyncParamInternal asyncWorkflow getExecuting setExecuting (fun _ -> true) ct oc
        let opEx = Some [ operationExecutingProp ]
        addCommandDependencies cmd opEx
        cmd

    member internal this.CommandAsyncParamCheckedI(asyncWorkflow, canExecute, ?dependentProperties: string list, ?token, ?onCancel) =
        let ct = defaultArg token CancellationToken.None
        let oc = defaultArg onCancel ignore
        let cmd = Commands.createAsyncParamInternal asyncWorkflow getExecuting setExecuting canExecute ct oc
        let opEx = Some [ operationExecutingProp ]
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

