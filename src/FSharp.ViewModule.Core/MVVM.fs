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

// Default command implementation for our MVVM base classes
type FunCommand (execute : obj -> unit, canExecute) =
    let canExecuteChanged = new Event<EventHandler, EventArgs>()

    member val private executeMethod = execute with get, set

    // Constructor which works from async workflows, and auto disables the command while executing
    new(asyncExecute, canExecute) as self =        
        let ui = SynchronizationContext.Current
        let executing = ref false
        let ce = (fun a -> (not !executing) && canExecute(a))
        // Build with default execute method, then replace
        FunCommand((fun a -> a |> ignore), ce)
        then
            let idg = self :> INotifyCommand
            let exec param = 
                executing := true                
                idg.RaiseCanExecuteChanged()
                async {
                    do! asyncExecute ui param
                    do! Async.SwitchToContext(ui)
                    executing := false
                    idg.RaiseCanExecuteChanged()
                } |> Async.Start
            self.executeMethod <- exec

    interface ICommand with
        [<CLIEvent>]
        member this.CanExecuteChanged = canExecuteChanged.Publish

        member this.CanExecute(param : obj) =
            canExecute(param)

        member this.Execute(param : obj) =
            this.executeMethod(param)

    interface INotifyCommand with
        member this.RaiseCanExecuteChanged() =
            canExecuteChanged.Trigger(this, EventArgs.Empty)
