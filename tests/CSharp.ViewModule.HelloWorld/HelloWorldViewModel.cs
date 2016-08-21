using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Threading;

using ViewModule;
using ViewModule.CSharp;
using ViewModule.Validation.CSharp;

namespace CSharp.ViewModule.HelloWorld
{
    class HelloWorldViewModel : ViewModelBase
    {
        private readonly INotifyingValue<string> _firstName;
        private readonly INotifyingValue<string> _lastName;
        private readonly IAsyncNotifyCommand _sayHello;
        private readonly INotifyCommand _cancelCommand;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public HelloWorldViewModel()
        {
            _firstName = Factory.Backing(nameof(FirstName), "Anton", 
                Validators.NotNullOrWhitespace()
                .Then(Validators.NotEqual("Foo")));

            _lastName = Factory.Backing(nameof(LastName), "Tcholakov",
                Validators.NotNullOrWhitespace()
                .Then(Validators.NotEqual("Bar")));

            _sayHello = Factory.CommandAsyncChecked(async ct =>
            {
                try
                {
                    await Task.Delay(2000, ct);
                    MessageBox.Show(Greeting);
                }
                finally { _cts.Dispose(); _cts = new CancellationTokenSource(); _sayHello.CancellationToken = _cts.Token; }
            },
            () => IsValid,
            _cts.Token, exn => MessageBox.Show("Sorry I was too slow :-(."),
            nameof(IsValid));

            _cancelCommand = Factory.CommandSyncChecked(() => _cts?.Cancel(),
                () => OperationExecuting, nameof(OperationExecuting));

            DependencyTracker.AddPropertyDependencies(nameof(ReadyToGreet), 
                nameof(OperationExecuting));
            DependencyTracker.AddPropertyDependencies(nameof(Name),
                nameof(FirstName), nameof(LastName));
            DependencyTracker.AddPropertyDependencies(nameof(Greeting),
                nameof(Name), nameof(NameLength));
        }

        public bool ReadyToGreet => !OperationExecuting && IsValid;
        public string FirstName { get { return _firstName.Value; } set { _firstName.Value = value; } }
        public string LastName { get { return _lastName.Value; } set { _lastName.Value = value; } }
        public string Name => string.Format("{0} {1}", FirstName, LastName);
        public int NameLength => Name.Length;
        public string Greeting => string.Format("Hello, {0}. Your name is {1} characters long.", Name, NameLength);

        public ICommand SayHello => _sayHello;
        public ICommand Cancel => _cancelCommand;
    }
}
