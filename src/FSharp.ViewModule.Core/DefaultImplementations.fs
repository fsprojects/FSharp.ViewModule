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

namespace FSharp.ViewModule.Core

open System
open System.ComponentModel
open System.Collections.Generic
open System.Threading
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

[<AutoOpen>]
module internal ChangeNotifierUtils =    
    let getPropertyNameFromExpression(expr : Expr) = 
        match expr with
        | PropertyGet(a, pi, list) -> pi.Name
        | _ -> ""

type ValidationEntry = { propertyName : string ; keyName : string }

type ValidationTracker(raiseErrorsChanged : string -> unit, propertyChanged : IObservable<PropertyChangedEventArgs>, entityValidator : string -> ValidationResult seq) =
    let errorDictionary = Dictionary<ValidationEntry, string list>()

    let setErrorState key error =
        let changed = 
            match error with
            | None -> errorDictionary.Remove(key)
            | Some err ->
                errorDictionary.[key] <- err
                true
        if changed then raiseErrorsChanged(key.propertyName)

    let setResult vr =
        let key, error = 
            match vr with
            | PropertyValidation(pn, ek, err) -> { propertyName = pn; keyName = ek}, err
            | EntityValidation(ek, err) -> { propertyName = String.Empty; keyName = ek}, err
        setErrorState key error

    let validateProperties (pcea : PropertyChangedEventArgs) =        
        entityValidator(pcea.PropertyName)      
        |> Seq.iter (fun vr -> setResult vr)

    do
        propertyChanged.Subscribe(validateProperties) |> ignore

    member this.HasErrors with get() = errorDictionary.Count > 0
    member this.GetErrors propertyName =
        errorDictionary
        |> Seq.filter (fun kvp -> kvp.Key.propertyName = propertyName)
        |> Seq.collect (fun kvp -> kvp.Value)

    interface IValidationTracker with
        member this.SetResult (vr : ValidationResult) = 
            setResult vr
        
        member this.ClearErrors() =
            errorDictionary.Clear()

/// Default implementation of IDependencyTracker which can be used for any relevent ViewModel
/// if an implementation does not already exist for the given framework
type DependencyTracker(raisePropertyChanged : string -> unit, propertyChanged : IObservable<PropertyChangedEventArgs>) as self =    
    let propertyTracking = Dictionary<string, Set<string>>()
    let commandTracking = Dictionary<string, INotifyCommand list>()
    
    let propertyTrackingActions propertyName =
        match propertyTracking.ContainsKey(propertyName) with
        | false -> []
        | true -> 
            propertyTracking.[propertyName]
            |> List.ofSeq
            |> List.map (fun dep -> (fun () -> raisePropertyChanged(dep)))

    let commandTrackingActions propertyName =
        match commandTracking.ContainsKey(propertyName) with
        | false -> []
        | true -> 
            commandTracking.[propertyName]
            |> List.map (fun cmd -> (fun () -> cmd.RaiseCanExecuteChanged()))

    let trackingActions propertyName =
        propertyTrackingActions propertyName 
        |> List.append (commandTrackingActions propertyName)

    let handleTrackingActions (args : PropertyChangedEventArgs) : unit =
        let actions = trackingActions args.PropertyName
        if not(List.isEmpty actions) then
            let sendNotifications() = List.iter (fun i -> i()) actions
            match self.SynchronizationContext with
            | null -> sendNotifications()
            | _ -> self.SynchronizationContext.Post((fun _ -> sendNotifications()), null)

    let addDependentCommand propertyName command =
        if (commandTracking.ContainsKey(propertyName)) then
            let existing = commandTracking.[propertyName]
            commandTracking.[propertyName] <- command :: existing
        else
            commandTracking.Add(propertyName, [command])

    let addDependentProperty propertyName dependency =
        if (propertyTracking.ContainsKey(dependency)) then
            let existing = propertyTracking.[dependency]
            propertyTracking.[dependency] <- Set.add propertyName existing
        else
            propertyTracking.Add(dependency, Set(seq {yield propertyName}))

    let addDependentProperties propertyName dependencies =    
        dependencies
        |> List.iter (addDependentProperty propertyName)

    do
        propertyChanged.Subscribe(handleTrackingActions) |> ignore

    /// Optional context used to raise all property changed notifications.  If null, they'll get raised directly.
    member val SynchronizationContext : SynchronizationContext = null with get, set

    interface IDependencyTracker with
        member this.AddPropertyDependencies (property : string, dependentProperties: string list) = addDependentProperties property dependentProperties

        member this.AddPropertyDependencies(property : Expr, dependentProperties: Expr list) =             
            let dependents = dependentProperties |> List.map getPropertyNameFromExpression
            addDependentProperties (getPropertyNameFromExpression property) dependents
            
        member this.AddPropertyDependency (property : Expr, dependentProperty: Expr) = 
            addDependentProperty (getPropertyNameFromExpression property) (getPropertyNameFromExpression dependentProperty)

        member this.AddPropertyDependency (property : string, dependentProperty: string) = addDependentProperty property dependentProperty

        member this.AddCommandDependency (command : INotifyCommand, expr) = addDependentCommand (getPropertyNameFromExpression expr) command

        member this.AddCommandDependency (command : INotifyCommand, name) = addDependentCommand name command
