namespace FSharp.ViewModule.MvvmCross

open System
open System.ComponentModel

open Cirrious.MvvmCross.ViewModels

open FSharp.ViewModule.Core

/// MvvmCross based command implementation
type FvxCommand(execute : obj -> unit, canExecute : obj -> bool) =
    inherit MvxCommand<obj>(System.Action<obj>(execute), Func<obj,bool>(canExecute))
    interface INotifyCommand with
        member this.RaiseCanExecuteChanged() = this.RaiseCanExecuteChanged()

/// MvvmCross based view model implementation
type FvxViewModel() =
    inherit MvxViewModel()
    
    // Used for error tracking (TODO)
    let errorsChanged = new Event<EventHandler<DataErrorsChangedEventArgs>, DataErrorsChangedEventArgs>()

    let mutable operationExecuting = false
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
        
    interface IViewModel with
        member this.RaisePropertyChanged (propertyName : string) = this.RaisePropertyChanged(propertyName)
        member this.OperationExecuting 
            with get() = this.OperationExecuting 
            and set v = this.OperationExecuting <- v
        member this.SetErrors (validationResults : ValidationResult seq) =
            ()

type ViewModuleTypeSpecification() =    
    interface IViewModuleTypeSpecification with
        member this.ViewModelType = typeof<FvxViewModel>
        member this.CommandType = typeof<FvxCommand>
        // TODO: Make platform "work" given our requirements
        member this.Platform = { Framework = ".NET" }