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

open ViewModule
open ViewModule.FSharp
open System
open System.Threading
open System.Windows.Input

type internal AsyncCommand<'a>(asyncWorkflow : SynchronizationContext -> 'a -> Async<unit>, canExecute : 'a -> bool, ?token : CancellationToken, ?onCancel : OperationCanceledException -> unit) as self =
    inherit ViewModelBase()

    let canExecuteChanged = new Event<EventHandler, EventArgs>()

    let fc = 
        match token, onCancel with
        | Some token, Some onCancel -> 
            self.Factory.CommandAsyncParamChecked(asyncWorkflow, canExecute, token = token, onCancel = onCancel)
        | _ -> 
            self.Factory.CommandAsyncParamChecked(asyncWorkflow, canExecute)     

    do 
        fc.CanExecuteChanged.Add (fun a -> canExecuteChanged.Trigger(self, a) )

    interface ICommand with
        [<CLIEvent>]
        member __.CanExecuteChanged = canExecuteChanged.Publish
        member __.CanExecute(param : obj) = fc.CanExecute(param)            
        member __.Execute(param : obj) = fc.Execute(param)
    interface INotifyCommand with
        member __.RaiseCanExecuteChanged() = fc.RaiseCanExecuteChanged()            
    interface IAsyncNotifyCommand with
        member __.CancellationToken with get() = fc.CancellationToken and set(v) = fc.CancellationToken <- v
    interface IAsyncCommand with
        member this.OperationExecuting with get() = this.OperationExecuting

namespace ViewModule.CSharp
    open System
    open System.Threading.Tasks

    open ViewModule

    [<AbstractClass;Sealed>]
    type Command () =
        static member CreateSync (execute : Action) = 
            ViewModule.Commands.createSyncInternal execute.Invoke (fun () -> true)
        static member CreateSync (execute : Action, canExecute : Func<bool>) = 
            ViewModule.Commands.createSyncInternal execute.Invoke canExecute.Invoke
        static member CreateAsync (createTask : Func<Task>) = 
            ViewModule.Internal.AsyncCommand((fun _ _ -> Async.fromTaskFunc createTask), (fun _ -> true)) :> IAsyncCommand

namespace ViewModule.FSharp
   
    module Command =
        let sync execute = 
            ViewModule.Commands.createSyncInternal execute (fun () -> true)
        let syncChecked execute canExecute = 
            ViewModule.Commands.createSyncInternal execute canExecute 
