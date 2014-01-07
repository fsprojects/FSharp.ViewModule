/*
Copyright (c) 2013-2014 FSharp.ViewModule Team

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

namespace FSharp.ViewModule.Demos.MvvmCross.Wpf
{
    using System.Windows.Threading;
    using Cirrious.MvvmCross.ViewModels;
    using Cirrious.MvvmCross.Wpf.Platform;
    using Cirrious.MvvmCross.Wpf.Views;

    public class Setup : MvxWpfSetup
    {
        public Setup(Dispatcher uiThreadDispatcher, IMvxWpfViewPresenter presenter)
            : base(uiThreadDispatcher, presenter)
        {
        }

        protected override IMvxApplication CreateApp()
        {
            return new Core.ViewModels.App.App();
        }
    }
}
