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

namespace FSharp.ViewModule.Core.ViewModel

open System
open System.ComponentModel
open System.Collections.Generic
open System.Threading
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open FSharp.ViewModule.Core
open FSharp.ViewModule.Core.Validation

/// Encapsulation of a value which handles raising property changed automatically in a clean manner
[<AbstractClass>]
type public NotifyingValue<'a>() =
    /// Extracts the current value from the backing storage
    abstract member Value : 'a with get, set

type internal NotifyingValueBackingField<'a> (propertyName, raisePropertyChanged : string -> unit, defaultValue : 'a, validationResultPublisher : IValidationTracker, validate : 'a -> string option) =
    inherit NotifyingValue<'a>()
    [<Literal>]
    let backingFieldValidationResultKey = "BackingFieldValidationKey"
    let propertyName = propertyName
    let mutable value = defaultValue
    do
        if (SynchronizationContext.Current <> null) then
            SynchronizationContext.Current.Post((fun _ -> validationResultPublisher.SetResult(PropertyValidation(propertyName, backingFieldValidationResultKey, validate(value)))), null)

    override this.Value 
        with get() = value 
        and set(v) = 
            if (not (EqualityComparer<'a>.Default.Equals(value, v))) then
                value <- v                                
                validationResultPublisher.SetResult(PropertyValidation(propertyName, backingFieldValidationResultKey, validate(value)))
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

[<AutoOpen>]
module ChangeNotifierUtils =    
    let internal getPropertyNameFromExpression(expr : Expr) = 
        match expr with
        | PropertyGet(a, pi, list) -> pi.Name
        | _ -> ""

type ViewModelPropertyFactory(dependencyTracker : IDependencyTracker, validationTracker: IValidationTracker, raisePropertyChanged : string -> unit) =     
    let addCommandDependencies cmd dependentProperties =
        let deps : Expr list = defaultArg dependentProperties []
        deps |> List.iter (fun prop -> dependencyTracker.AddCommandDependency(cmd, prop)) 
        dependencyTracker.AddCommandDependency(cmd, "HasErrors")

    member this.Backing (prop : Expr, defaultValue : 'a, validate : ValidationStep<'a> -> ValidationStep<'a>) =
        let validateFun = Validators.validate(getPropertyNameFromExpression(prop)) >> validate >> result
        NotifyingValueBackingField<'a>(getPropertyNameFromExpression(prop), raisePropertyChanged, defaultValue, validationTracker, validateFun) :> NotifyingValue<'a>

    member this.Backing (prop : Expr, defaultValue : 'a, ?validate : 'a -> string option) =
        let validateFun = defaultArg validate (fun _ -> None)
        NotifyingValueBackingField<'a>(getPropertyNameFromExpression(prop), raisePropertyChanged, defaultValue, validationTracker, validateFun) :> NotifyingValue<'a>

    member this.FromFuncs (prop : Expr, getter, setter) =
        NotifyingValueFuncs<'a>(getPropertyNameFromExpression(prop), raisePropertyChanged, getter, setter) :> NotifyingValue<'a>

    member this.CommandAsync(asyncWorkflow) =
        let cmd = Commands.createAsyncInternal asyncWorkflow (fun () -> true)
        cmd

    member this.CommandAsyncChecked(asyncWorkflow, canExecute, ?dependentProperties: Expr list) =
        let cmd = Commands.createAsyncInternal asyncWorkflow canExecute
        addCommandDependencies cmd dependentProperties
        cmd

    member this.CommandAsyncParam(asyncWorkflow) =
        let cmd = Commands.createAsyncParamInternal asyncWorkflow (fun _ -> true)
        cmd

    member this.CommandAsyncParamChecked(asyncWorkflow, canExecute, ?dependentProperties: Expr list) =
        let cmd = Commands.createAsyncParamInternal asyncWorkflow canExecute
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

    member this.CommandSyncCheckedParam(execute, canExecute, ?dependentProperties: Expr list) =
        let cmd = Commands.createSyncParamInternal execute canExecute
        addCommandDependencies cmd dependentProperties
        cmd
    
[<AbstractClass>]
type ViewModelBase() as self =
    let propertyChanged = new Event<_, _>()
    let depTracker = DependencyTracker(self.RaisePropertyChanged, propertyChanged.Publish)
    
    // Used for error tracking
    let errorsChanged = new Event<EventHandler<DataErrorsChangedEventArgs>, DataErrorsChangedEventArgs>()
    let errorTracker = ValidationTracker(self.RaiseErrorChanged, propertyChanged.Publish, self.Validate)
    
    let vmf = ViewModelPropertyFactory(depTracker :> IDependencyTracker, errorTracker :> IValidationTracker, self.RaisePropertyChanged)        

    // TODO: This should be set by commands to allow disabling of other commands by default
    let operationExecuting = vmf.Backing(<@ self.OperationExecuting @>, false)

    member this.Factory with get() = vmf                    

    // Overridable entity level validation
    abstract member Validate : string -> ValidationResult seq
    default this.Validate(propertyName: string) =
        Seq.empty
            
    member private this.RaiseErrorChanged(propertyName : string) =
        errorsChanged.Trigger(this, new DataErrorsChangedEventArgs(propertyName))
        this.RaisePropertyChanged(<@ self.HasErrors @>)

    member this.RaisePropertyChanged(propertyName : string) =
        propertyChanged.Trigger(this, new PropertyChangedEventArgs(propertyName))
        
    member this.RaisePropertyChanged(expr : Expr) =
        let propName = getPropertyNameFromExpression(expr)
        this.RaisePropertyChanged(propName)

    /// Value used to notify view that an asynchronous operation is executing
    member this.OperationExecuting with get() = operationExecuting.Value and set(value) = operationExecuting.Value <- value

    /// Setup all errors for validation
    member this.SetErrors (result : ValidationResult seq) =
        // TODO: Do something here
        result |> ignore

    /// Handles management of dependencies for all computed properties 
    /// as well as ICommand dependencies
    member this.DependencyTracker = depTracker :> IDependencyTracker

    member this.HasErrors with get() = errorTracker.HasErrors

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
        
