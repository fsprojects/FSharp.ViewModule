namespace FSharp.ViewModule.TypeProvider

type DefaultViewModuleTypeSpecification() =
    interface FSharp.ViewModule.IViewModuleTypeSpecification with
        member this.ViewModelType = typeof<FSharp.ViewModule.ViewModelBase>
        member this.CommandType = typeof<FSharp.ViewModule.FunCommand>
        member this.Platform = { Framework = ".NET" }


