module FSharp.ViewModule.Tests

open NUnit.Framework
open NUnit.Framework.Constraints
open FsUnit
open FSharp.ViewModule
open System.ComponentModel

type ViewModels = ViewModelProvider<"FSharp.ViewModule.Tests.Model", "FSharp.ViewModule.MvvmCross">

[<Test>]
let ``Can create an instance of Home ViewModule`` () =
  let home = ViewModels.Home()
  home.Fullname |> should equal " "

[<Test>]
let ``Setting names in Home ViewModule should raise Property Changed`` () =
  let home = ViewModels.Home()
  home.ShouldAlwaysRaiseInpcOnUserInterfaceThread(false) // Required for MvvmCross to not delay the prop changed events
  let resArr = ResizeArray<string>()
  use subscription = home.PropertyChanged.Subscribe(fun args -> resArr.Add(args.PropertyName))
  home.Firstname <- "Foo"
  home.Lastname <- "Bar"

  resArr.Count |> should equal 4
  resArr.[0] |> should equal "Firstname"
  resArr.[1] |> should equal "Fullname"
  resArr.[2] |> should equal "Lastname"
  resArr.[3] |> should equal "Fullname"

[<Test>]
let ``Click in command should increment ClickCount`` () =
    let home = ViewModels.Home()
    home.ClickCount |> should equal 0
    home.Click.Execute(null)
    home.ClickCount |> should equal 1