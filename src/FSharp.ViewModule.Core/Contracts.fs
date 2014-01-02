namespace FSharp.ViewModule

open System.ComponentModel
open System.Windows.Input

/// <summary>Extension of ICommand with a public method to fire the CanExecuteChanged event</summary>
/// <remarks>This type should provide a constructor which accepts an Execute (obj -> unit) and CanExecute (obj -> bool) function</remarks>
type INotifyCommand =
    inherit ICommand 
    
    /// Trigger the CanExecuteChanged event for this specific ICommand
    abstract RaiseCanExecuteChanged : unit -> unit


/// <summary>Extension of INotifyPropertyChanged with a public method to fire the PropertyChanged event</summary>
/// <remarks>This type should provide a constructor which accepts an Execute (obj -> unit) and CanExecute (obj -> bool) function</remarks>
type IViewModel<'a> =
    inherit INotifyPropertyChanged
    inherit INotifyDataErrorInfo

    /// Trigger the PropertyChanged event for this specific ViewModel
    abstract RaisePropertyChanged : string -> unit

    /// Value used to notify view that an asynchronous operation is executing
    abstract Executing : bool