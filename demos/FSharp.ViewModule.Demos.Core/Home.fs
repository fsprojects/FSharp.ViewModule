namespace FSharp.ViewModule.Demos.Core.Model

type Home = { FirstName : string ; LastName : string ; ClickCount : int }

[<ReflectedDefinition>]
module HomeModule =
    let init () = { FirstName = "" ; LastName = "" ; ClickCount = 0 }

    let fullName (model : Home) = model.FirstName + " " + model.LastName

    let click (model : Home) = { model with ClickCount = model.ClickCount + 1 }