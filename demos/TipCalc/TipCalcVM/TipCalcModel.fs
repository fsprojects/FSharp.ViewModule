namespace TipCalc

open FSharp.ViewModule
open System

type TipCalcModel() as self =
    inherit ViewModelBase()

    // Create our backing fields
    let subTotal = self.Factory.Backing(<@ self.SubTotal @>, 0.0)
    let postTaxTotal = self.Factory.Backing(<@ self.PostTaxTotal @>, 0.0)
    let tipPercent = self.Factory.Backing(<@ self.TipPercent @>, 15.0)

    // Add dependencies for INotifyPropertyChanged to be handled correctly
    do
        self.DependencyTracker.AddPropertyDependencies(<@ self.TipAmount @>, [ <@@ self.SubTotal @@> ; <@@ self.PostTaxTotal @@> ; <@@ self.TipPercent @@>])
        self.DependencyTracker.AddPropertyDependencies(<@ self.Total @>, [ <@@ self.SubTotal @@> ; <@@ self.PostTaxTotal @@> ; <@@ self.TipPercent @@>])
    
    member this.SubTotal with get() = subTotal.Value and set(v) = subTotal.Value <- v
    member this.PostTaxTotal with get() = postTaxTotal.Value and set(v) = postTaxTotal.Value <- v
    member this.TipPercent with get() = tipPercent.Value and set(v) = tipPercent.Value <- v

    member this.TipAmount = Math.Round(this.TipPercent * this.SubTotal / 100.0, 2)

    /// Total value, rounded to nearest quarter.
    member this.Total = Math.Round(4.0 * (this.PostTaxTotal + this.TipAmount)) / 4.0;
