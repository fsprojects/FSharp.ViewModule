module FSharp.ViewModule.Tests

open FSharp.ViewModule
open NUnit.Framework

[<Test>]
let ``castAs unboxes successfully`` () =
  let initial : obj = box <| ResizeArray<int>()
  let cast : ResizeArray<int> = castAs<ResizeArray<int>>(initial)
  Assert.IsNotNull(cast)