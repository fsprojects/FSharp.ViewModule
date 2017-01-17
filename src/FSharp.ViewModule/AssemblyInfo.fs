namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.ViewModule")>]
[<assembly: AssemblyProductAttribute("FSharp.ViewModule")>]
[<assembly: AssemblyDescriptionAttribute("Library providing MVVM and INotifyPropertyChanged support for F# projects")>]
[<assembly: AssemblyVersionAttribute("1.0.5.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0.5.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0.5.0"
    let [<Literal>] InformationalVersion = "1.0.5.0"
