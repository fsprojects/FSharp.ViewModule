module FSharp.ViewModule.Demos.Core.ViewModels.App

open Cirrious.CrossCore
open Cirrious.MvvmCross.ViewModels

type App () =
    inherit MvxApplication ()

    do
        Mvx.RegisterSingleton<IMvxAppStart> (MvxAppStart<HomeViewModel> ())