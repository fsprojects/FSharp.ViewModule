namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.ViewModule")>]
[<assembly: AssemblyProductAttribute("FSharp.ViewModule")>]
[<assembly: AssemblyDescriptionAttribute("Library providing MVVM and INotifyPropertyChanged support for F# projects")>]
[<assembly: AssemblyVersionAttribute("0.9.2")>]
[<assembly: AssemblyFileVersionAttribute("0.9.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.9.2"
