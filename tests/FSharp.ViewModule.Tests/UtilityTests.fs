module ViewModule.Tests.Utilities

open ViewModule
open NUnit.Framework

[<Test>]
let ``castAs unboxes successfully`` () =
  let initial : obj = box <| ResizeArray<int>()
  let cast : ResizeArray<int> = castAs<ResizeArray<int>>(initial)
  Assert.IsNotNull(cast)

[<Test>]
let ``castAs returns null when provided null`` () =
  let initial : obj = null
  let cast : ResizeArray<float> = castAs<ResizeArray<float>>(initial)
  Assert.IsNull(cast)

[<Test>]
let ``castAs returns null when type doesn't match`` () =
  let initial : obj = box <| ResizeArray<int>()
  let cast : ResizeArray<float> = castAs<ResizeArray<float>>(initial)
  Assert.IsNull(cast)

type Test() =
    member val Prop = 42 with get, set

[<Test>]
let ``getPropertyNameFromExpression returns correct name`` () =
  let inst = Test()
  let name = getPropertyNameFromExpression(<@ inst.Prop @>)
  Assert.AreEqual("Prop", name)

[<Test>]
let ``Helpers getPropertyNameFromExpression returns correct name`` () =
  let inst = Test()
  let name = Helpers.getPropertyNameFromExpression(<@ inst.Prop @>)
  Assert.AreEqual("Prop", name)
