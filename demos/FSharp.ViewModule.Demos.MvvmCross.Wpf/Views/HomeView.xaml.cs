using Cirrious.MvvmCross.Wpf.Views;
using FSharp.ViewModule.Demos.Core.ViewModels;

namespace FSharp.ViewModule.Demos.MvvmCross.Wpf.Views
{
    public partial class HomeView : MvxWpfView
    {
        public new HomeViewModel ViewModel
        {
            get { return (HomeViewModel)base.ViewModel; }
            set { base.ViewModel = value; }
        }

        public HomeView()
        {
            InitializeComponent();
        }
    }
}
