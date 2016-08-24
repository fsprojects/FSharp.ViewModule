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

namespace ViewModule.Progress

open System
open ViewModule
open ViewModule.FSharp

type OperationState =
| Idle
| Reporting of status : string
| Executing of status : string * current : int * total : int

type ProgressManager() as self =
    inherit ViewModelBase()

    let progressHandler = Progress<OperationState>()

    let isOperating = self.Factory.Backing(<@@ self.IsOperating @@>, false)
    let indeterminate = self.Factory.Backing(<@@ self.Indeterminate @@>, false)
    let current = self.Factory.Backing(<@@ self.Current @@>, 0)
    let max = self.Factory.Backing(<@@ self.Max @@>, 100)
    let status = self.Factory.Backing(<@@ self.Status @@>, String.Empty)

    do
        progressHandler.ProgressChanged.Add(fun pc ->
            match pc with
            | Idle ->
                self.IsOperating <- false                               
                self.Indeterminate <- true
                self.Status <- String.Empty
            | Reporting(status) ->
                self.IsOperating <- true
                self.Indeterminate <- true
                self.Status <- status
            | Executing(status, current, total) ->
                self.IsOperating <- true
                self.Indeterminate <- false
                self.Current <- current
                self.Max <- total
                self.Status <- status
            )

    member this.IsOperating with get() = isOperating.Value and private set(v) = isOperating.Value <- v
    member this.Indeterminate with get() = indeterminate.Value and private set(v) = indeterminate.Value <- v
    member this.Current with get() = current.Value and private set(v) = current.Value <- v
    member this.Max with get() = max.Value and private set(v) = max.Value <- v
    member this.Status with get() = status.Value and private set(v) = status.Value <- v

    member internal this.ProgressReporter = progressHandler :> IProgress<OperationState>

namespace ViewModule.Progress.FSharp

open System

open ViewModule
open ViewModule.Progress

[<AutoOpen>]
module ProgressReporting =
    let updateProgress (manager : ProgressManager) (state : OperationState) =
        manager.ProgressReporter.Report(state)        

    let reportProgress (reporter : (OperationState -> unit) option) (state : OperationState) =
        match reporter with
        | Some(report) -> report(state)
        | None -> ()

namespace ViewModule.Progress.CSharp

open System
open System.Runtime.CompilerServices

open ViewModule
open ViewModule.Progress
open ViewModule.Progress.FSharp

[<Extension>]
type ProgressExtensions =
    
    [<Extension>]
    static member SetIdle(manager : ProgressManager) =
        updateProgress manager Idle

    [<Extension>]
    static member Report(manager : ProgressManager, status : string) =
        updateProgress manager (Reporting status)

    [<Extension>]
    static member Executing(manager : ProgressManager, status : string, current : int, total : int) =
        updateProgress manager (Executing(status, current, total))