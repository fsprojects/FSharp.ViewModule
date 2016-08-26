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

namespace ViewModule

open System
open System.Windows.Input
open Microsoft.FSharp.Quotations

/// This interface is a tag for implementation of extension methods. Do not implement externally. 
type IEventViewModelPropertyFactory<'a> =
    inherit IViewModelPropertyFactory
    
    // The only type which should implement this interface is EventViewModelBase. Therefore, the implementation can be cast to this type
    // and used to access the following internal members:
    //
    // EventValueCommandI<'a> : value:'a -> ICommand
    // EventValueCommandI<'a> : unit -> ICommand
    // EventValueCommandI<'a,'b> : valueFactory:('b -> 'a) -> ICommand
    //
    // EventValueCommandCheckedI<'a> : value:'a * canExecute:(unit -> bool) * ?dependentProperties: string list -> INotifyCommand
    // EventValueCommandCheckedI<'a> : canExecute:('a -> bool) * ?dependentProperties: string list -> INotifyCommand
    // EventValueCommandCheckedI<'a,'b> : valueFactory:('b -> 'a) * canExecute:('b -> bool) * ?dependentProperties: string list -> INotifyCommand


    // The F# API is implemented with members having the following signatures via extension methods in the ViewModule.FSharp namespace:
    // (note that string property names are replaced by quotation expressions of type Expr)
    //
    // EventValueCommand<'a> : value:'a -> ICommand
    // EventValueCommand<'a> : unit -> ICommand
    // EventValueCommand<'a,'b> : valueFactory:('b -> 'a) -> ICommand
    //
    // EventValueCommandChecked<'a> : value:'a * canExecute:(unit -> bool) * ?dependentProperties: Expr list -> INotifyCommand
    // EventValueCommandChecked<'a> : canExecute:('a -> bool) * ?dependentProperties: Expr list -> INotifyCommand
    // EventValueCommandChecked<'a,'b> : valueFactory:('b -> 'a) * canExecute:('b -> bool) * ?dependentProperties: Expr list -> INotifyCommand

    // The C# API is implemented with members having the following signatures via extension methods in the ViewModule.CSharp namespace:
    //
    // EventValueCommand<'TEvent> : value:'TEvent -> ICommand
    // EventValueCommand<'TEvent> : unit -> ICommand
    // EventValueCommand<'TEvent, 'TSource> : valueSelector: Func<'TSource, 'TEvent> -> ICommand
    //
    // EventValueCommandChecked<'TEvent> : value:'TEvent * canExecute: Func<bool> * [<ParamArray>] dependentProperties: string array -> INotifyCommand
    // EventValueCommandChecked<'TEvent> : canExecute: Func<'TEvent, bool> * [<ParamArray>] dependentProperties: string array -> INotifyCommand
    // EventValueCommandChecked<'TEvent,' TSource> : valueSelector: Func<'TSource, 'TEvent> * canExecute: Func<'TSource, bool> * [<ParamArray>] dependentProperties: string array -> INotifyCommand
    //
    // In addition, the following extension methods are implemented in the namespace ViewModule.CSharp.Expressions
    // to support LINQ Expression trees for C# versions which do not have nameof:
    //
    // EventValueCommandChecked<'TEvent> : value:'TEvent * canExecute: Func<bool> * [<ParamArray>] dependentProperties: Expression<Func<obj>> array -> INotifyCommand
    // EventValueCommandChecked<'TEvent> : canExecute: Func<'TEvent, bool> * [<ParamArray>] dependentProperties: Expression<Func<obj>> array -> INotifyCommand
    // EventValueCommandChecked<'TEvent,' TSource> : valueSelector: Func<'TSource, 'TEvent> * canExecute: Func<'TSource, bool> * [<ParamArray>] dependentProperties: Expression<Func<obj>> array -> INotifyCommand

[<AbstractClass>]
type EventViewModelBase<'a>() =
    inherit ViewModule.Internal.ViewModelUntyped()
    
    let eventStream = Event<'a>()

    let addCommandDependencies cmd dependentProperties (tracker : IDependencyTracker) =
        dependentProperties |> List.iter (fun prop -> (tracker :?> DependencyTracker).AddCommandDependencyI(cmd, prop)) 

    member __.EventStream = eventStream.Publish :> IObservable<'a>

    member __.RaiseEvent = eventStream.Trigger

    member this.Factory with get() = this :> IEventViewModelPropertyFactory<'a>

    interface IEventViewModelPropertyFactory<'a>

    member internal __.EventValueCommandI<'a> value =
        let execute = fun _ -> eventStream.Trigger value
        Commands.createSyncInternal execute (fun _ -> true) :> ICommand

    member internal __.EventValueCommandI<'a,'b> (valueFactory : 'b -> 'a) =
        let execute = valueFactory >> eventStream.Trigger 
        Commands.createSyncParamInternal execute (fun _ -> true) :> ICommand

    member internal this.EventValueCommandI<'a>() =
        let execute = fun (args:'a) -> eventStream.Trigger(args)
        Commands.createSyncParamInternal execute (fun _ -> true) :> ICommand

    member internal this.EventValueCommandCheckedI<'a>(value, canExecute, dependentProperties) =
        let execute = fun _ -> eventStream.Trigger(value)
        let cmd = Commands.createSyncInternal execute canExecute
        addCommandDependencies cmd dependentProperties this.DependencyTracker
        cmd

    member internal this.EventValueCommandCheckedI<'a>(canExecute, dependentProperties) =
        let execute = fun (args:'a) -> eventStream.Trigger(args)
        let cmd = Commands.createSyncParamInternal execute canExecute
        addCommandDependencies cmd dependentProperties this.DependencyTracker
        cmd

    member internal this.EventValueCommandCheckedI<'a,'b>(valueFactory, canExecute, dependentProperties) =
        let execute = fun (args:'b) -> eventStream.Trigger(valueFactory(args))
        let cmd = Commands.createSyncParamInternal execute canExecute
        addCommandDependencies cmd dependentProperties this.DependencyTracker
        cmd
        

namespace ViewModule.FSharp

open ViewModule

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

[<AutoOpen>]
module Extensions =

    type IEventViewModelPropertyFactory<'a> with
        member x.EventValueCommand(value : 'a) =
            (x :?> EventViewModelBase<'a>).EventValueCommandI(value)

        member x.EventValueCommand() =
            (x :?> EventViewModelBase<'a>).EventValueCommandI()

        member x.EventValueCommand(valueFactory : 'b -> 'a) =
            (x :?> EventViewModelBase<'a>).EventValueCommandI(valueFactory)

        member x.EventValueCommandChecked(value : 'a, canExecute : unit -> bool, ?dependentProperties : Expr list) =
            let deps = defaultArg dependentProperties [] |> List.map getPropertyNameFromExpression 
            (x :?> EventViewModelBase<'a>).EventValueCommandCheckedI(value, canExecute, deps)
            
        member x.EventValueCommandChecked(canExecute : 'a -> bool, ?dependentProperties : Expr list) =
            let deps = defaultArg dependentProperties [] |> List.map getPropertyNameFromExpression 
            (x :?> EventViewModelBase<'a>).EventValueCommandCheckedI(canExecute, deps)
            
        member x.EventValueCommandChecked(valueFactory : 'b -> 'a, canExecute : 'b -> bool, ?dependentProperties : Expr list) =
            let deps = defaultArg dependentProperties [] |> List.map getPropertyNameFromExpression 
            (x :?> EventViewModelBase<'a>).EventValueCommandCheckedI(valueFactory, canExecute, deps)


namespace ViewModule.CSharp

open ViewModule

open System
open System.Runtime.CompilerServices

[<Extension>]
type EventExtensions =
    
    [<Extension>]
    static member EventValueCommand(factory : IEventViewModelPropertyFactory<'TEvent>, value : 'TEvent) =
        (factory :?> EventViewModelBase<'TEvent>).EventValueCommandI(value)
            
    [<Extension>]
    static member EventValueCommand(factory : IEventViewModelPropertyFactory<'TEvent>) =
        (factory :?> EventViewModelBase<'TEvent>).EventValueCommandI()   
         
    [<Extension>]
    static member EventValueCommand(factory : IEventViewModelPropertyFactory<'TEvent>, valueSelector : Func<'TSource, 'TEvent>) =
        (factory :?> EventViewModelBase<'TEvent>).EventValueCommandI(valueSelector.Invoke)

    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, value : 'TEvent, canExecute : Func<bool>, [<ParamArray>] dependentProperties : string array) =
        (factory :?> EventViewModelBase<'TEvent>).EventValueCommandCheckedI(value, canExecute.Invoke, dependentProperties |> List.ofArray)
            
    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, canExecute : Func<'TEvent, bool>, [<ParamArray>] dependentProperties : string array) =
        (factory :?> EventViewModelBase<'TEvent>).EventValueCommandCheckedI(canExecute.Invoke, dependentProperties |> List.ofArray)
         
    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, valueSelector : Func<'TSource, 'TEvent>, canExecute : Func<'TSource, bool>, [<ParamArray>] dependentProperties : string array) =
        (factory :?> EventViewModelBase<'TEvent>).EventValueCommandCheckedI(valueSelector.Invoke, canExecute.Invoke, dependentProperties |> List.ofArray)


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
        (factory :?> EventViewModelBase<'TEvent>).EventValueCommandCheckedI(value, canExecute.Invoke, deps |> List.ofArray)
            
    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, canExecute : Func<'TEvent, bool>, [<ParamArray>] dependentProperties : PropertyExpr array) =
        let deps = dependentProperties |> Array.map getPropertyNameFromLinqExpression
        (factory :?> EventViewModelBase<'TEvent>).EventValueCommandCheckedI(canExecute.Invoke, deps |> List.ofArray)
         
    [<Extension>]
    static member EventValueCommandChecked(factory : IEventViewModelPropertyFactory<'TEvent>, valueSelector : Func<'TSource, 'TEvent>, canExecute : Func<'TSource, bool>, [<ParamArray>] dependentProperties : PropertyExpr array) =
        let deps = dependentProperties |> Array.map getPropertyNameFromLinqExpression
        (factory :?> EventViewModelBase<'TEvent>).EventValueCommandCheckedI(valueSelector.Invoke, canExecute.Invoke, deps |> List.ofArray)