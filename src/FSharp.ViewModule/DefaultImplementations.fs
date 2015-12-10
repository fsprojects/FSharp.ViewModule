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

namespace FSharp.ViewModule

open System
open System.ComponentModel
open System.Collections.Generic
open System.Threading

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

type internal ValidationKey =
    | PropertyGeneratedValidation of ValidationResult
    | EntityGeneratedValidation of ValidationResult

type internal ValidationSource =
    | FromProperty
    | FromEntity
type internal ValidationEntry = 
    | PropertyEntry of propertyName : string * source : ValidationSource
    | EntityEntry of source : ValidationSource

type ValidationTracker(raiseErrorsChanged : string -> unit, propertyChanged : IObservable<PropertyChangedEventArgs>, entityValidator : string -> ValidationResult seq, propertiesToIgnore : Expr list) =
    let errorDictionary = Dictionary<ValidationEntry, string list>()
    let validationDictionary = Dictionary<string, unit -> string list>()
    let propertyNamesToIgnore = propertiesToIgnore |> List.map getPropertyNameFromExpression

    let getPropertyName ve =
        match ve with
        | PropertyEntry(pn, _) -> pn
        | EntityEntry(_) -> String.Empty

    let setErrorState key error =
        let changed = 
            match error with
            | [] -> errorDictionary.Remove(key)
            | err ->
                errorDictionary.[key] <- err
                true
        if changed then 
            let prop = getPropertyName key
            raiseErrorsChanged(prop)

    let setResult (vk : ValidationKey) =
        let key, error = 
            match vk with
            | PropertyGeneratedValidation(PropertyValidation(pn, err)) -> PropertyEntry(pn, FromProperty), err
            | EntityGeneratedValidation(PropertyValidation(pn, err)) -> PropertyEntry(pn, FromEntity), err 
            | PropertyGeneratedValidation(EntityValidation(err)) -> EntityEntry(FromProperty), err
            | EntityGeneratedValidation(EntityValidation(err)) -> EntityEntry(FromEntity), err

        setErrorState key error

    let validateOneProperty propertyName =
        let (exists, actions) = validationDictionary.TryGetValue propertyName
        if exists then
            let results = actions()
            setResult(PropertyGeneratedValidation(PropertyValidation(propertyName, results)))

    let validatePropertiesInternal (propertyName : string) =        
        // Validate using entity level validation first...
        let prop = propertyNamesToIgnore |> List.tryFind ((=) propertyName)
        if Option.isNone prop then
            entityValidator(propertyName)      
            |> Seq.iter (fun vr -> setResult(EntityGeneratedValidation(vr)))
        
        // Now check individual property validations
        if not(String.IsNullOrEmpty(propertyName)) then
            validateOneProperty propertyName
        else
            // Now we need to track each property in our dictionary, one at a time
            validationDictionary.Keys
            |> Seq.iter (fun k -> validateOneProperty k)

    let validateProperties (pcea : PropertyChangedEventArgs) =        
        validatePropertiesInternal pcea.PropertyName

    let extractEntityEntry ve value =
        match ve, value with
        | _, [] -> None
        | PropertyEntry(_,_), _ -> None
        | EntityEntry(_), v -> Some v

    let extractPropertyEntry ve value =
        match ve, value with
        | _, [] -> None
        | EntityEntry(_), _ -> None
        | PropertyEntry(pn,_), v -> Some (pn, v)

    let ctx = SynchronizationContext.Current
    do
        propertyChanged.Subscribe(validateProperties) |> ignore
        // We can't validate now, as we're not constructed completely at this point, so we post to the context
        // in the UI if possible to validate us later
        match ctx with
        | null -> () 
        | _ -> ctx.Post((fun _ -> validateProperties(PropertyChangedEventArgs(String.Empty))), null)

    member this.HasErrors with get() = errorDictionary.Count > 0
    member this.GetErrors propertyName =
        errorDictionary
        |> Seq.filter (fun kvp -> getPropertyName(kvp.Key) = propertyName)
        |> Seq.collect (fun kvp -> Seq.ofList kvp.Value)
        |> Array.ofSeq
        |> Seq.cast<string>

    member this.EntityErrors 
        with get() =
                errorDictionary
                |> Seq.choose (fun kvp -> extractEntityEntry kvp.Key kvp.Value)
                |> Seq.collect (fun i -> Seq.ofList(i))

    member this.PropertyErrors 
        with get() =
            seq {
                let dict = Dictionary<_,_>()
                errorDictionary
                |> Seq.choose (fun kvp -> extractPropertyEntry kvp.Key kvp.Value)
                |> Seq.iter (fun pe -> 
                    if not(dict.ContainsKey(fst pe)) then
                        dict.[fst pe] <- snd pe
                    else
                        dict.[fst pe] <- dict.[fst pe] @ (snd pe)
                    )

                yield! dict |> Seq.map (fun kvp -> PropertyValidation(kvp.Key, kvp.Value))
            }

    interface IValidationTracker with
        member this.SetPropertyValidationResult (vr : ValidationResult) = 
            setResult(PropertyGeneratedValidation(vr))
        
        member this.SetEntityValidationResult (vr : ValidationResult) = 
            setResult(EntityGeneratedValidation(vr))
        
        member this.ClearErrors() =
            errorDictionary.Clear()

        member this.Revalidate propertyName =
            validatePropertiesInternal propertyName

        member this.AddPropertyValidationWatcher propertyName validation =
            validationDictionary.Add(propertyName, validation)

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
