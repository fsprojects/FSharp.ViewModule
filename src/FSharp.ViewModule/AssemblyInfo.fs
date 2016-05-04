namespace FSharp.ViewModule.AssemblyInfo

open System.Reflection
open System.Runtime.CompilerServices

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.9.9.3"

[<assembly: AssemblyTitleAttribute("FSharp.ViewModule")>]
[<assembly: AssemblyProductAttribute("FSharp.ViewModule")>]
[<assembly: AssemblyDescriptionAttribute("Library providing MVVM and INotifyPropertyChanged support for F# projects")>]
[<assembly: AssemblyVersionAttribute(AssemblyVersionInformation.Version)>]
[<assembly: AssemblyFileVersionAttribute(AssemblyVersionInformation.Version)>]

[<assembly: InternalsVisibleTo("FSharp.ViewModule.Wpf")>]
do ()

