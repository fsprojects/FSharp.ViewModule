namespace FSharp.ViewModule

open Microsoft.FSharp.Quotations
open System.Windows.Input
open System.ComponentModel.DataAnnotations
open System.ComponentModel

/// The Specification used by the type provider to generate a view model
type IViewModuleTypeSpecification =
    /// The type used for the View Model.
    abstract ViewModelType : System.Type
    /// <summary>A quotation used to build the method to raise PropertyChanged on the ViewModel. </summary>
    /// <remarks>This can typically be written as <c>&lt;@ ViewModelBase().RaisePropertyChanged("") @&gt;</c></remarks>
    abstract RaisePropertyChangedCallQuotation : Expr<unit>
    
    /// The type used for implementing ICommand
    abstract CommandType : System.Type
    /// <summary>A quotation used to build the method to raise CanExecuteChanged on a Command. </summary>
    /// <remarks>This can typically be written as <c>&lt;@ CommandBase().RaiseCanExecuteChanged("") @&gt;</c></remarks>
    abstract RaiseCanExecuteChangedCallQuotation : Expr<unit>
    

type Point = {
    [<Range(10.0, 1000.0, ErrorMessage = "Value for {0} must be between {1} and {2}.")>] X: float;
    Y: float;
    [<StringLength(5, ErrorMessage = "The Str value cannot exceed 5 characters.")>] Str: string;
}

