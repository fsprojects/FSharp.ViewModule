namespace FSharp.ViewModule.Demos.Core.ViewModels

open FSharp.ViewModule

type ViewModels = ViewModelProvider<"FSharp.ViewModule.Demos.Core", "FSharp.ViewModule.MvvmCross", "FSharp.ViewModule.MvvmCross.ViewModuleTypeSpecification">

type HomeViewModel () =
    inherit ViewModels.Home ()