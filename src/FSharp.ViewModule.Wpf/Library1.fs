namespace FSharp.ViewModule

open System
open System.Collections.Generic
open System.ComponentModel
open System.Reflection


type internal IValueHolder =
    abstract member GetValue : unit -> obj
    abstract member SetValue : obj -> unit

type internal ValueHolder<'a>(value : INotifyingValue<'a>) =
    interface IValueHolder with
        member __.GetValue() = box value.Value
        member __.SetValue(v) = value.Value <- unbox v         

type [<TypeDescriptionProvider(typeof<DynamicViewModelTypeDescriptorProvider>)>] DynamicViewModel() = 
    inherit ViewModelBase()
    
    let propertyChanged = new Event<_, _>()

    let customProps = Dictionary<string, PropertyDescriptor * IValueHolder>()

    let makePD name prop = 
        NotifyingValuePropertyDescriptor(name,prop) :> PropertyDescriptor

    let makeIV prop =
        ValueHolder(prop) :> IValueHolder

    member internal __.CustomProperties = customProps

    member __.AddProperty(name,prop) =
        customProps.Add(name, ((makePD name prop), (makeIV prop)))    

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish    

and DynamicViewModelTypeDescriptorProvider(parent) =
    inherit TypeDescriptionProvider(parent)

    let mutable td = null, null
    new() = DynamicViewModelTypeDescriptorProvider(TypeDescriptor.GetProvider(typeof<DynamicViewModel>))

    override __.GetTypeDescriptor(objType, inst) =
        match td with
        | desc, i when desc <> null && obj.ReferenceEquals(i, inst) ->
            desc
        | _ ->
            let parent = base.GetTypeDescriptor(objType, inst)
            let desc = DynamicViewModelTypeDescriptor(parent, inst :?> DynamicViewModel) :> ICustomTypeDescriptor
            td <- desc, inst
            desc

and [<AllowNullLiteral>] internal DynamicViewModelTypeDescriptor(parent, inst : DynamicViewModel) =
    inherit CustomTypeDescriptor(parent)

    override __.GetProperties() =
        let newProps = 
            inst.CustomProperties.Values
            |> Seq.map fst
        let props = 
            base.GetProperties()
            |> Seq.cast<PropertyDescriptor>
            |> Seq.append newProps
            |> Array.ofSeq
        PropertyDescriptorCollection(props)

and internal NotifyingValuePropertyDescriptor<'a>(name : string, notifyingValue : INotifyingValue<'a>) =
    inherit PropertyDescriptor(name, [| |])

    override __.ComponentType = typeof<DynamicViewModel>
    override __.PropertyType = typeof<'a>
    override __.Description = String.Empty
    override __.IsBrowsable = true
    override __.IsReadOnly = false
    override __.CanResetValue(o) = false
    override __.GetValue(comp) =
        match comp with
        | :? DynamicViewModel as dvm ->
            let prop = dvm.CustomProperties.[name]
            let vh = snd prop
            vh.GetValue()
        | _ -> null
    override __.ResetValue(comp) = ()
    override __.SetValue(comp, v) =
        match comp with
        | :? DynamicViewModel as dvm ->
            let prop = dvm.CustomProperties.[name]
            let vh = snd prop
            vh.SetValue(v)
        | _ -> ()
    override __.ShouldSerializeValue(c) = false


module DynamicVM =
    let createVm() = DynamicViewModel()

    let nval initialValue = NotifyingValue(initialValue)

    let add name nv (vm : DynamicViewModel) =         
        vm.AddProperty(name, nv)        
        vm

    let readonly name value (vm : DynamicViewModel) =         
        vm.AddProperty(name, NotifyingValue(value))        
        vm


