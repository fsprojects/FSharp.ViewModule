namespace System
open System.Reflection

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.9.2"

[<assembly: AssemblyTitleAttribute("FSharp.ViewModule")>]
[<assembly: AssemblyProductAttribute("FSharp.ViewModule")>]
[<assembly: AssemblyDescriptionAttribute("FSharp.ViewModule - Idiomatic F# for MVVM")>]
[<assembly: AssemblyVersionAttribute(AssemblyVersionInformation.Version)>]
[<assembly: AssemblyFileVersionAttribute(AssemblyVersionInformation.Version)>]
do ()

