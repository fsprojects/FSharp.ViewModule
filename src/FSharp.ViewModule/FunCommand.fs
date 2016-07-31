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
open System.ComponentModel
open System.Collections.Generic
open System.Threading
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open FSharp.ViewModule

// Default command implementation for our MVVM base classes
type FunCommand (execute : obj -> unit, canExecute, token) =
    let canExecuteChanged = new Event<EventHandler, EventArgs>()

    member val private executeMethod = execute with get, set

    member val private cancellationToken = token with get, set

    new(execute, canExecute) =
        FunCommand(execute, canExecute, CancellationToken.None)
    // Constructor which works from async workflows, and auto disables the command while executing
    new(asyncExecute, getExecuting, setExecuting, canExecute, token : CancellationToken, onCancel) as self =        
        let ui = SynchronizationContext.Current        
        let ce = (fun a -> (not(getExecuting())) && canExecute(a))
        // Build with default execute method, then replace
        FunCommand((fun a -> a |> ignore), ce, token)
        then
            let idg = self :> INotifyCommand
            let exec param = 
                setExecuting(true)
                idg.RaiseCanExecuteChanged()
                let wf = async {
                    do! asyncExecute ui param
                    do! Async.SwitchToContext(ui)
                    setExecuting(false)
                    idg.RaiseCanExecuteChanged()
                }
                Async.StartWithContinuations(
                    wf, 
                    (fun _ -> ()), 
                    (fun _ -> ()), 
                    (fun e -> 
                        ui.Post((fun _ -> 
                            onCancel(e)
                            setExecuting(false)
                            idg.RaiseCanExecuteChanged()
                            ), null)
                        ), 
                    self.cancellationToken)
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

    interface CSharp.ViewModule.INotifyCommand

    interface IAsyncNotifyCommand with
        member this.CancellationToken with get() = this.cancellationToken and set(v) = this.cancellationToken <- v

    interface CSharp.ViewModule.IAsyncNotifyCommand

/// Module containing Command factory methods to create ICommand implementations
module internal Commands =
    let createSyncInternal execute canExecute =
        let ceWrapped : obj -> bool = fun _ -> canExecute()
        let func : obj -> unit = (fun _ -> execute())
        FunCommand(func, ceWrapped) :> CSharp.ViewModule.INotifyCommand    

    let createAsyncInternal (asyncWorkflow : (SynchronizationContext -> Async<unit>)) getExecuting setExecuting canExecute (token : CancellationToken) onCancel =
        let execute = (fun (ui : SynchronizationContext) (p : obj) -> asyncWorkflow(ui))
        FunCommand(execute, getExecuting, setExecuting, (fun o -> canExecute()), token, onCancel) :> CSharp.ViewModule.IAsyncNotifyCommand

    let createSyncParamInternal<'a> (execute : ('a -> unit)) (canExecute : ('a -> bool)) =
        let ceWrapped o = 
            let a = downcastAndCreateOption(o)            
            match a with
            | None -> false
            | Some v -> canExecute(v)

        let func o = 
            let a = downcastAndCreateOption(o)
            match a with
            | None -> a |> ignore
            | Some v -> execute(v)

        let result = FunCommand(func, ceWrapped) :> CSharp.ViewModule.INotifyCommand

        // Note that we need to handle the fact that the arg is passed as null the first time, due to stupid data binding issues.  Let's fix that here.
        // This will cause the command to requery the CanExecute method after everything's loaded, which will then pass onto the user's canExecute function.
        // The first time things are loaded, since null will be passed, None will go through, and the method won't execute
        // Note that we only do this if loaded in a sync context that's current, so we can post back safely
        match SynchronizationContext.Current with
        | null -> ()
        | sc   -> sc.Post((fun _ -> result.RaiseCanExecuteChanged()), null)

        result

    let createAsyncParamInternal<'a> (asyncWorkflow : (SynchronizationContext -> 'a -> Async<unit>)) getExecuting setExecuting (canExecute : ('a -> bool)) token onCancel =
        let ceWrapped o = 
            let a = downcastAndCreateOption(o)            
            match a with
            | None -> false
            | Some v -> canExecute(v)

        // Handler for when param type doesn't match - does nothing
        let emptyFunc (ui : SynchronizationContext) (a : obj) : Async<unit> = async { () }

        // Build a handler that converts the untyped param to our typed param
        let func (ui : SynchronizationContext) (o : obj) = 
            let a = downcastAndCreateOption(o)
            match a with
            | None -> emptyFunc ui o 
            | Some v -> asyncWorkflow ui v

        let result = FunCommand(func, getExecuting, setExecuting, ceWrapped, token, onCancel) :> CSharp.ViewModule.IAsyncNotifyCommand

        // Note that we need to handle the fact that the arg is passed as null the first time, due to stupid data binding issues.  Let's fix that here.
        // This will cause the command to requery the CanExecute method after everything's loaded, which will then pass onto the user's canExecute function.
        // The first time things are loaded, since null will be passed, None will go through, and the method won't execute
        // Note that we only do this if loaded in a sync context that's current, so we can post back safely
        if SynchronizationContext.Current <> null then
            SynchronizationContext.Current.Post((fun _ -> result.RaiseCanExecuteChanged()), null)

        result
