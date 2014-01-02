namespace FSharp.ViewModule

open Microsoft.FSharp.Quotations
open System.Windows.Input
open System.ComponentModel.DataAnnotations
open System.ComponentModel

/// This is the specification required to determine which platform target the type provider builds
/// For example, this would specify a standard .NET assembly vs. PCL Profile7, etc
type Platform = { Framework : string }

/// The Specification used by the type provider to generate a view model
/// This can be implemented to allow use of any ViewModel and Command
/// Framework with the type provider
type IViewModuleTypeSpecification =

    /// The type used for the View Model.  Should be an open generic (typedefof<T>) implementing IViewModel<'a>
    abstract ViewModelType : System.Type
    
    /// The type used for implementing ICommand.  Should implement INotifyCommand
    abstract CommandType : System.Type
    
    /// The type provider target platform
    abstract Platform : Platform