namespace FSharp.ViewModule.Tests.Model

type Home = { Firstname : string ; Lastname : string ; ClickCount : int }

[<ReflectedDefinition>]
module HomeModule =
    let init() = { Firstname = "" ; Lastname = "" ; ClickCount = 0 }

    let Fullname (model : Home) = model.Firstname + " " + model.Lastname

    let Click (model : Home) = { model with ClickCount = model.ClickCount + 1 }