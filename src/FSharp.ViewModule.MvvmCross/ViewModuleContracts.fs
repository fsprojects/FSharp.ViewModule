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
type FvxViewModel() as vm =
    inherit MvxViewModel()
    
    // Used for error tracking (TODO)
    let errorsChanged = new Event<EventHandler<DataErrorsChangedEventArgs>, DataErrorsChangedEventArgs>()

    let depTracker = DependencyTracker(vm.RaisePropertyChanged, vm.PropertyChanged)

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

        member this.DependencyTracker = depTracker :> IDependencyTracker

type ViewModuleTypeSpecification() =    
    interface IViewModuleTypeSpecification with
        member this.ViewModelType = typeof<FvxViewModel>
        member this.CommandType = typeof<FvxCommand>
        // TODO: Make platform "work" given our requirements
        member this.Platform = { Framework = ".NET" }