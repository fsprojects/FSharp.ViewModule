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

namespace FSharp.ViewModule

open System
open System.Windows.Input
open Microsoft.FSharp.Quotations

[<AbstractClass>]
type EventViewModelBase<'a>() =
    inherit FSharp.ViewModule.Internal.ViewModelUntyped()
    
    let eventStream = Event<'a>()

    let addCommandDependencies cmd dependentProperties (tracker : IDependencyTracker) =
        let deps : Expr list = defaultArg dependentProperties []
        deps |> List.iter (fun prop -> tracker.AddCommandDependency(cmd, prop)) 

    member __.EventStream = eventStream.Publish :> IObservable<'a>

    member __.RaiseEvent = eventStream.Trigger

    member this.Factory with get() = this :> IEventViewModelPropertyFactory<'a>

    interface IEventViewModelPropertyFactory<'a> with
        member __.EventValueCommand<'a> value =
            let execute = fun _ -> eventStream.Trigger value
            Commands.createSyncInternal execute (fun _ -> true) :> ICommand

        member __.EventValueCommand<'a,'b> (valueFactory : 'b -> 'a) =
            let execute = valueFactory >> eventStream.Trigger 
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
        

