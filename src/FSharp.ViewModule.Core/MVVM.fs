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

namespace FSharp.ViewModule

open System
open System.ComponentModel
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open FSharp.ViewModule
open FSharp.ViewModule.Validation

/// Encapsulation of a value which handles raising property changed automatically in a clean manner
[<AbstractClass>]
type public NotifyingValue<'a>() =
    /// Extracts the current value from the backing storage
    abstract member Value : 'a with get, set

type IViewModelPropertyFactory =
    abstract Backing<'a> : prop:Expr * defaultValue:'a * validate:(ValidationResult<'a> -> ValidationResult<'a>) -> NotifyingValue<'a>
    abstract Backing<'a> : prop:Expr * defaultValue:'a * ?validate:('a -> string list) -> NotifyingValue<'a>
    abstract FromFuncs<'a> : prop:Expr * getter:(unit->'a) * setter: ('a->unit) -> NotifyingValue<'a>

    abstract CommandAsync : asyncWorkflow:(SynchronizationContext -> Async<unit>) * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand
    abstract CommandAsyncChecked : asyncWorkflow:(SynchronizationContext -> Async<unit>) * canExecute:(unit -> bool) * ?dependentProperties: Expr list * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand
    abstract CommandAsyncParam<'a> : asyncWorkflow:(SynchronizationContext -> 'a -> Async<unit>) * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand
    abstract CommandAsyncParamChecked<'a> : asyncWorkflow:(SynchronizationContext -> 'a -> Async<unit>) * canExecute:('a -> bool) * ?dependentProperties: Expr list * ?token:CancellationToken * ?onCancel:(OperationCanceledException -> unit) -> IAsyncNotifyCommand    

    abstract CommandSync : execute:(unit -> unit) -> INotifyCommand
    abstract CommandSyncParam<'a> : execute:('a -> unit) -> INotifyCommand
    abstract CommandSyncChecked : execute:(unit -> unit) * canExecute:(unit -> bool) * ?dependentProperties: Expr list -> INotifyCommand
    abstract CommandSyncParamChecked<'a> : execute:('a -> unit) * canExecute:('a -> bool) * ?dependentProperties: Expr list -> INotifyCommand    

type IEventViewModelPropertyFactory<'a> =
    inherit IViewModelPropertyFactory

    abstract EventValueCommand<'a> : value:'a -> ICommand
    abstract EventValueCommand<'a> : unit -> ICommand
    abstract EventValueCommand<'a,'b> : valueFactory:('b -> 'a) -> ICommand

    abstract EventValueCommandChecked<'a> : value:'a * canExecute:(unit -> bool) * ?dependentProperties: Expr list -> INotifyCommand
    abstract EventValueCommandChecked<'a> : canExecute:('a -> bool) * ?dependentProperties: Expr list -> INotifyCommand
    abstract EventValueCommandChecked<'a,'b> : valueFactory:('b -> 'a) * canExecute:('b -> bool) * ?dependentProperties: Expr list -> INotifyCommand

type internal NotifyingValueBackingField<'a> (propertyName, raisePropertyChanged : string -> unit, defaultValue : 'a, validationResultPublisher : IValidationTracker, validate : 'a -> string list) =
    inherit NotifyingValue<'a>()
    
    let mutable value = defaultValue
    
    let updateValidation () =
        validate value
    
    do
        validationResultPublisher.AddPropertyValidationWatcher propertyName updateValidation
        if (SynchronizationContext.Current <> null) then
            SynchronizationContext.Current.Post((fun _ -> validationResultPublisher.Revalidate propertyName), null)

    override this.Value 
        with get() = value 
        and set(v) = 
            if (not (EqualityComparer<'a>.Default.Equals(value, v))) then
                value <- v                                
                raisePropertyChanged propertyName                

type internal NotifyingValueFuncs<'a> (propertyName, raisePropertyChanged : string -> unit, getter, setter) =
    inherit NotifyingValue<'a>()
    let propertyName = propertyName
    override this.Value 
        with get() = getter()
        and set(v) = 
            if (not (EqualityComparer<'a>.Default.Equals(getter(), v))) then
                setter v
                raisePropertyChanged propertyName
    
namespace FSharp.ViewModule.Internal
open System
open System.ComponentModel
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open FSharp.ViewModule
open FSharp.ViewModule.Validation


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
        let deps : Expr list = defaultArg dependentProperties []
        deps |> List.iter (fun prop -> dependencyTracker.AddCommandDependency(cmd, prop)) 

    // TODO: This should be set by commands to allow disabling of other commands by default
    let operationExecuting = NotifyingValueBackingField(getPropertyNameFromExpression(<@ self.OperationExecuting @>), self.RaisePropertyChanged, false, validationTracker, (fun _ -> List.empty)) :> NotifyingValue<bool>

    // Overridable entity level validation
    abstract member Validate : string -> ValidationResult seq
    default this.Validate(propertyName: string) =
        Seq.empty
            
    member private this.RaiseErrorChanged(propertyName : string) =
        errorsChanged.Trigger(this, new DataErrorsChangedEventArgs(propertyName))
        if (Option.isNone(errorRelatedPropertyNames |> List.tryFind ((=) propertyName))) then
            errorRelatedPropertyNames
            |> List.iter (fun p -> this.RaisePropertyChanged(p))

    member this.RaisePropertyChanged([<Optional;DefaultParameterValue(null);CallerMemberName>]propertyName : string) =
        propertyChanged.Trigger(this, new PropertyChangedEventArgs(propertyName))
        
    member this.RaisePropertyChanged(expr : Expr) =
        let propName = getPropertyNameFromExpression(expr)
        this.RaisePropertyChanged(propName)

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

        member this.DependencyTracker = this.DependencyTracker

    interface IViewModelPropertyFactory with
        member this.Backing (prop : Expr, defaultValue : 'a, validate : ValidationResult<'a> -> ValidationResult<'a>) =
            let validateFun = Validators.validate(getPropertyNameFromExpression(prop)) >> validate >> result
            NotifyingValueBackingField<'a>(getPropertyNameFromExpression(prop), this.RaisePropertyChanged, defaultValue, validationTracker, validateFun) :> NotifyingValue<'a>

        member this.Backing (prop : Expr, defaultValue : 'a, ?validate : 'a -> string list) =
            let validateFun = defaultArg validate (fun _ -> List.empty)
            NotifyingValueBackingField<'a>(getPropertyNameFromExpression(prop), this.RaisePropertyChanged, defaultValue, validationTracker, validateFun) :> NotifyingValue<'a>

        member this.FromFuncs (prop : Expr, getter, setter) =
            NotifyingValueFuncs<'a>(getPropertyNameFromExpression(prop), this.RaisePropertyChanged, getter, setter) :> NotifyingValue<'a>

        member this.CommandAsync(asyncWorkflow, ?token, ?onCancel) =
            let ct = defaultArg token CancellationToken.None
            let oc = defaultArg onCancel (fun e -> ())
            let cmd = Commands.createAsyncInternal asyncWorkflow getExecuting setExecuting (fun () -> true) ct oc
            cmd

        member this.CommandAsyncChecked(asyncWorkflow, canExecute, ?dependentProperties: Expr list, ?token, ?onCancel) =
            let ct = defaultArg token CancellationToken.None
            let oc = defaultArg onCancel (fun e -> ())
            let cmd = Commands.createAsyncInternal asyncWorkflow getExecuting setExecuting canExecute ct oc
            addCommandDependencies cmd dependentProperties
            cmd

        member this.CommandAsyncParam(asyncWorkflow, ?token, ?onCancel) =
            let ct = defaultArg token CancellationToken.None
            let oc = defaultArg onCancel (fun e -> ())
            let cmd = Commands.createAsyncParamInternal asyncWorkflow getExecuting setExecuting (fun _ -> true) ct oc
            cmd

        member this.CommandAsyncParamChecked(asyncWorkflow, canExecute, ?dependentProperties: Expr list, ?token, ?onCancel) =
            let ct = defaultArg token CancellationToken.None
            let oc = defaultArg onCancel (fun e -> ())
            let cmd = Commands.createAsyncParamInternal asyncWorkflow getExecuting setExecuting canExecute ct oc
            addCommandDependencies cmd dependentProperties
            cmd

        member this.CommandSync(execute) =
            let cmd = Commands.createSyncInternal execute (fun () -> true)
            cmd

        member this.CommandSyncChecked(execute, canExecute, ?dependentProperties: Expr list) =
            let cmd = Commands.createSyncInternal execute canExecute
            addCommandDependencies cmd dependentProperties
            cmd

        member this.CommandSyncParam(execute) =
            let cmd = Commands.createSyncParamInternal execute (fun _ -> true)
            cmd

        member this.CommandSyncParamChecked(execute, canExecute, ?dependentProperties: Expr list) =
            let cmd = Commands.createSyncParamInternal execute canExecute
            addCommandDependencies cmd dependentProperties
            cmd

namespace FSharp.ViewModule

open System
open System.Windows.Input
open Microsoft.FSharp.Quotations

[<AbstractClass>]
type ViewModelBase() =
    inherit FSharp.ViewModule.Internal.ViewModelUntyped()

    member this.Factory with get() = this :> IViewModelPropertyFactory

[<AbstractClass>]
type EventViewModelBase<'a>() =
    inherit FSharp.ViewModule.Internal.ViewModelUntyped()
    
    let eventStream = Event<'a>()

    let addCommandDependencies cmd dependentProperties (tracker : IDependencyTracker) =
        let deps : Expr list = defaultArg dependentProperties []
        deps |> List.iter (fun prop -> tracker.AddCommandDependency(cmd, prop)) 

    member this.EventStream = eventStream.Publish :> IObservable<'a>

    member this.Factory with get() = this :> IEventViewModelPropertyFactory<'a>

    interface IEventViewModelPropertyFactory<'a> with
        member this.EventValueCommand<'a> value =
            let execute = fun _ -> eventStream.Trigger(value)
            Commands.createSyncInternal execute (fun _ -> true) :> ICommand

        member this.EventValueCommand<'a,'b> valueFactory =
            let execute = fun (args:'b) -> eventStream.Trigger(valueFactory(args))
            Commands.createSyncParamInternal execute (fun _ -> true) :> ICommand

        member this.EventValueCommand<'a>() =
            let execute = fun (args:'a) -> eventStream.Trigger(args)
            Commands.createSyncParamInternal execute (fun _ -> true) :> ICommand

        member this.EventValueCommandChecked<'a>(value, canExecute, ?dependentProperties) =
            let execute = fun _ -> eventStream.Trigger(value)
            let cmd = Commands.createSyncInternal execute canExecute
            addCommandDependencies cmd dependentProperties this.DependencyTracker
            cmd

        member this.EventValueCommandChecked<'a>(canExecute, ?dependentProperties) =
            let execute = fun (args:'a) -> eventStream.Trigger(args)
            let cmd = Commands.createSyncParamInternal execute canExecute
            addCommandDependencies cmd dependentProperties this.DependencyTracker
            cmd

        member this.EventValueCommandChecked<'a,'b>(valueFactory, canExecute, ?dependentProperties) =
            let execute = fun (args:'b) -> eventStream.Trigger(valueFactory(args))
            let cmd = Commands.createSyncParamInternal execute canExecute
            addCommandDependencies cmd dependentProperties this.DependencyTracker
            cmd
        