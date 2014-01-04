namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.ViewModule")>]
[<assembly: AssemblyProductAttribute("FSharp.ViewModule")>]
[<assembly: AssemblyDescriptionAttribute("FSharp.ViewModule - Idiomatic F# for MVVM")>]
[<assembly: AssemblyVersionAttribute("1.0.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0.0"
