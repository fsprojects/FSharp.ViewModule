module FSharp.ViewModule.Tests.DynamicViewModel

open FSharp.ViewModule
open NUnit.Framework
open System.ComponentModel

open DynamicVM

[<Test>]
let ``DynamicViewModel add property works`` () =
    Assert.DoesNotThrow(fun _ -> 
        let dvm = DynamicViewModel()
        let v = NotifyingValue(42)
        dvm.AddProperty("Test", v)
    )

[<Test>]
let ``DynamicViewModel add then read property works`` () =
    let dvm = DynamicViewModel()
    let v = NotifyingValue(42)
    dvm.AddProperty("Test", v)
    
    let props = TypeDescriptor.GetProperties(dvm)
    let prop = props.Find("Test", false)

    Assert.IsNotNull(prop)

[<Test>]
let ``DynamicViewModel add then read property value`` () =
    let v = nval 42
    let dynamicVm = 
        createVm ()
        |> add "Test" v
    
    let props = TypeDescriptor.GetProperties(dynamicVm)
    let prop = props.Find("Test", false)

    let v = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(42, v)

[<Test>]
let ``DynamicViewModel add then modify property value`` () =
    let v1 = nval 1
    let v2 = nval 2
    let dynamicVm = 
        createVm ()
        |> add "Test" v1
        |> add "Test2" v2
    
    let props = TypeDescriptor.GetProperties(dynamicVm)
    let prop = props.Find("Test", false)

    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(1, cur)

    v1.Value <- 55
    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(55, cur)

    let prop = props.Find("Test2", false)

    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(2, cur)

    v2.Value <- 29
    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(29, cur)

[<Test>]
let ``DynamicViewModel addConst sets property value`` () =
    let vm = 
        createVm ()
        |> readonly "Test" 55
    
    let props = TypeDescriptor.GetProperties(vm)
    let prop = props.Find("Test", false)

    let cur = unbox <| prop.GetValue(vm)
    Assert.AreEqual(55, cur)
