using System.Windows.Threading;
using Cirrious.MvvmCross.ViewModels;
using Cirrious.MvvmCross.Wpf.Platform;
using Cirrious.MvvmCross.Wpf.Views;

namespace FSharp.ViewModule.Demos.MvvmCross.Wpf
{
    public class Setup : MvxWpfSetup
    {
        public Setup(Dispatcher uiThreadDispatcher,
        IMvxWpfViewPresenter presenter)
            : base(uiThreadDispatcher, presenter)
        {
        }

        protected override IMvxApplication CreateApp()
        {
            return new FSharp.ViewModule.Demos.Core.ViewModels.App.App();
        }
    }
}
