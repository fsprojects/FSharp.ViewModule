using FSharp.ViewModule;
using System.Windows;
using System.Windows.Input;

namespace CSharp.ViewModule.HelloWorld
{
    class HelloWorldViewModel : ViewModelBase
    {
        private readonly INotifyingValue<string> _name;
        private readonly INotifyCommand _sayHello;

        public HelloWorldViewModel()
        {
            _name = Factory.Backing(() => Name, "Anton");
            _sayHello = Factory.CommandSync(() => MessageBox.Show(Greeting));
            DependencyTracker.AddPropertyDependencies(() => Greeting,
                () => Name, () => NameLength);
        }

        public string Name { get { return _name.Value; } set { _name.Value = value; } }
        public int NameLength => Name.Length;
        public string Greeting => string.Format("Hello, {0}. Your name is {1} characters long.", Name, NameLength);

        public ICommand SayHello => _sayHello;
    }
}
