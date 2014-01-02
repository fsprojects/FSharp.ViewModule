namespace FSharp.ViewModule

open Microsoft.FSharp.Quotations
open System.Windows.Input
open System.ComponentModel.DataAnnotations
open System.ComponentModel

/// The Specification used by the type provider to generate a view model
type IViewModuleTypeSpecification =

    /// The type used for the View Model.  Should implement IViewModel
    abstract ViewModelType : System.Type
    
    /// The type used for implementing ICommand.  Should implement INotifyCommand
    abstract CommandType : System.Type
    