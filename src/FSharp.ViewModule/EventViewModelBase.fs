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

namespace ViewModule

open System
open System.Windows.Input
open Microsoft.FSharp.Quotations


[<AbstractClass>]
type EventViewModelBase<'a>() =
    inherit ViewModule.Internal.ViewModelUntyped()
    
    let eventStream = Event<'a>()

    let (propChanged, addCommandDependencies, getExecuting, setExecuting, operationExecutingProp, validationTracker) = (base.Factory :?> ViewModelPropertyFactory).Delegators

    let factory = EventViewModelPropertyFactory<'a>(eventStream.Trigger, propChanged, addCommandDependencies, getExecuting, setExecuting, operationExecutingProp, validationTracker)

    member __.EventStream = eventStream.Publish :> IObservable<'a>

    member __.RaiseEvent = eventStream.Trigger

    member this.Factory with get() = factory :> IEventViewModelPropertyFactory<'a>        

namespace ViewModule.FSharp

open ViewModule

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

[<AutoOpen>]
module Extensions =

    type IEventViewModelPropertyFactory<'a> with
        member x.EventValueCommand(value : 'a) =
            (x :?> EventViewModelPropertyFactory<'a>).EventValueCommandI(value)

        member x.EventValueCommand() =
            (x :?> EventViewModelPropertyFactory<'a>).EventValueCommandI()

        member x.EventValueCommand(valueFactory : 'b -> 'a) =
            (x :?> EventViewModelPropertyFactory<'a>).EventValueCommandI(valueFactory)

        member x.EventValueCommandChecked(value : 'a, canExecute : unit -> bool, ?dependentProperties : Expr list) =
            let deps = defaultArg dependentProperties [] |> List.map getPropertyNameFromExpression |> Some
            (x :?> EventViewModelPropertyFactory<'a>).EventValueCommandCheckedI(value, canExecute, deps)
            
        member x.EventValueCommandChecked(canExecute : 'a -> bool, ?dependentProperties : Expr list) =
            let deps = defaultArg dependentProperties [] |> List.map getPropertyNameFromExpression  |> Some
            (x :?> EventViewModelPropertyFactory<'a>).EventValueCommandCheckedI(canExecute, deps)
            
        member x.EventValueCommandChecked(valueFactory : 'b -> 'a, canExecute : 'b -> bool, ?dependentProperties : Expr list) =
            let deps = defaultArg dependentProperties [] |> List.map getPropertyNameFromExpression |> Some
            (x :?> EventViewModelPropertyFactory<'a>).EventValueCommandCheckedI(valueFactory, canExecute, deps)


namespace ViewModule.CSharp

open ViewModule

open System
open System.Runtime.CompilerServices

[<Extension>]
type EventExtensions =
    
    [<Extension>]
    static member EventValueCommand(factory : IEventViewModelPropertyFactory<'TEvent>, value : 'TEvent) =
        (factory :?> EventViewModelPropertyFactory<'TEvent>).EventValueCommandI(value)
            
    [<Extension>]
    static member EventValueCommand(factory : IEventViewModelPropertyFactory<'TEvent>) =
        (factory :?> EventViewModelPropertyFactory<'TEvent>).EventValueCommandI()   
         
    [<Extension>]
    static member EventValueCommand(factory : IEventViewModelPropertyFactory<'TEvent>, valueSelector : Func<'TSource, 'TEvent>) =
        (factory :?> EventViewModelPropertyFactory<'TEvent>).EventValueCommandI(valueSelector.Invoke)

    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, value : 'TEvent, canExecute : Func<bool>, [<ParamArray>] dependentProperties : string array) =
        (factory :?> EventViewModelPropertyFactory<'TEvent>).EventValueCommandCheckedI(value, canExecute.Invoke, dependentProperties |> List.ofArray |> Some)
            
    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, canExecute : Func<'TEvent, bool>, [<ParamArray>] dependentProperties : string array) =
        (factory :?> EventViewModelPropertyFactory<'TEvent>).EventValueCommandCheckedI(canExecute.Invoke, dependentProperties |> List.ofArray |> Some)
         
    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, valueSelector : Func<'TSource, 'TEvent>, canExecute : Func<'TSource, bool>, [<ParamArray>] dependentProperties : string array) =
        (factory :?> EventViewModelPropertyFactory<'TEvent>).EventValueCommandCheckedI(valueSelector.Invoke, canExecute.Invoke, dependentProperties |> List.ofArray |> Some)


namespace ViewModule.CSharp.Expressions

open ViewModule

open System
open System.Linq.Expressions
open System.Runtime.CompilerServices

[<Extension>]
type EventExtensions =
    
    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, value : 'TEvent, canExecute : Func<bool>, [<ParamArray>] dependentProperties : PropertyExpr array) =
        let deps = dependentProperties |> Array.map getPropertyNameFromLinqExpression
        (factory :?> EventViewModelPropertyFactory<'TEvent>).EventValueCommandCheckedI(value, canExecute.Invoke, deps |> List.ofArray |> Some)
            
    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, canExecute : Func<'TEvent, bool>, [<ParamArray>] dependentProperties : PropertyExpr array) =
        let deps = dependentProperties |> Array.map getPropertyNameFromLinqExpression
        (factory :?> EventViewModelPropertyFactory<'TEvent>).EventValueCommandCheckedI(canExecute.Invoke, deps |> List.ofArray |> Some)
         
    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, valueSelector : Func<'TSource, 'TEvent>, canExecute : Func<'TSource, bool>, [<ParamArray>] dependentProperties : PropertyExpr array) =
        let deps = dependentProperties |> Array.map getPropertyNameFromLinqExpression 
        (factory :?> EventViewModelPropertyFactory<'TEvent>).EventValueCommandCheckedI(valueSelector.Invoke, canExecute.Invoke, deps |> List.ofArray |> Some)