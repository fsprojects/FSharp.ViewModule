namespace FSharp.ViewModule.MvvmCross

open System
open System.ComponentModel

open Cirrious.MvvmCross.ViewModels
open FSharp.ViewModule

/// MvvmCross based command implementation
type FvxCommand(execute : obj->unit, canExecute : obj -> bool) =
    inherit MvxCommand<obj>(System.Action<obj>(execute), Func<obj,bool>(canExecute))
    interface INotifyCommand with
        member this.RaiseCanExecuteChanged() = this.RaiseCanExecuteChanged()

/// MvvmCross based view model implementation
type FvxViewModel<'a>(state : 'a) =
    inherit MvxViewModel()
    
    // Used for error tracking (TODO)
    let errorsChanged = new Event<EventHandler<DataErrorsChangedEventArgs>, DataErrorsChangedEventArgs>()

    let mutable operationExecuting = false
    let mutable state = state

    member this.State 
        with get() = state
        and set v = 
            if (not(obj.ReferenceEquals(state, v))) then
                state <- v
                this.RaisePropertyChanged(PropertyChangedEventArgs("State"))

    member this.OperationExecuting
        with get() = 
            operationExecuting 
        and set v = 
            if (operationExecuting <> v) then
                operationExecuting <- v
                this.RaisePropertyChanged(PropertyChangedEventArgs("OperationExecuting"))
    
    // Used for validation and error tracking (TODO)
    interface INotifyDataErrorInfo with
        member this.GetErrors propertyName =
            Seq.empty<string> :> System.Collections.IEnumerable

        member this.HasErrors with get() = false

        [<CLIEvent>]
        member this.ErrorsChanged = errorsChanged.Publish
        
    interface IViewModel<'a> with
        member this.State with get() = this.State
        member this.RaisePropertyChanged (propertyName : string) = this.RaisePropertyChanged(propertyName)
        member this.OperationExecuting 
            with get() = this.OperationExecuting 
            and set v = this.OperationExecuting <- v
        member this.SetErrors (validationResults : ValidationResult seq) =
            ()

type MvvmCrossViewModuleTypeSpecification() =    
    interface IViewModuleTypeSpecification with
        member this.ViewModelType = typedefof<FvxViewModel<_>>
        member this.CommandType = typeof<FvxCommand>
        // TODO: Make platform "work" given our requirements
        member this.Platform = { Framework = ".NET" }