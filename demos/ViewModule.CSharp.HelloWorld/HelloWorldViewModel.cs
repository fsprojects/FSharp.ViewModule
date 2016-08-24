using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using ViewModule;
using ViewModule.CSharp;
using static ViewModule.Validation.CSharp.Validators;

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
                NotNullOrWhitespace
                .Then(NotEqual("Foo")));

            _lastName = Factory.Backing(nameof(LastName), "Tcholakov",
                NotNullOrWhitespace
                .Then(NotEqual("Bar"))
                .Then(x => x.Length < 10, "Length cannot exceed 10 characters"));

            _sayHello = 
                Factory.CommandAsyncChecked(Greet, () => IsValid && !OperationExecuting,
                    _cts.Token, exn => MessageBox.Show("Sorry I was too slow :-(."),
                    nameof(IsValid), nameof(OperationExecuting));

            _cancelCommand = Factory.CommandSyncChecked(() => _cts?.Cancel(),
                () => OperationExecuting, nameof(OperationExecuting));

            DependencyTracker.AddPropertyDependencies(nameof(ReadyToGreet), 
                nameof(OperationExecuting));
            DependencyTracker.AddPropertyDependencies(nameof(FullName),
                nameof(FirstName), nameof(LastName));
            DependencyTracker.AddPropertyDependencies(nameof(Greeting),
                nameof(FullName), nameof(NameLength));
        }

        private async Task Greet(CancellationToken token)
        {
            try
            {
                await Task.Delay(2000, token);
                MessageBox.Show(Greeting);
            }
            finally { _cts.Dispose(); _cts = new CancellationTokenSource(); _sayHello.CancellationToken = _cts.Token; }
        }

        public override IEnumerable<ValidationState> Validate(string propertyName)
        {
            if (propertyName == nameof(FullName))
            {
                var errors = FullName.Validate(NotEqual("Reed Copsey"), "This is a poor choice of names.");
                yield return ValidationState.NewPropertyErrors(nameof(FullName), errors);
                yield return ValidationState.NewEntityErrors(errors);
            }
        }

        public bool ReadyToGreet => !OperationExecuting && IsValid; 
        public string FirstName { get { return _firstName.Value; } set { _firstName.Value = value; } }
        public string LastName { get { return _lastName.Value; } set { _lastName.Value = value; } }
        public string FullName => $"{FirstName} {LastName}";
        public int NameLength => FullName.Length;
        public string Greeting => $"Hello, {FullName}. Your name is {NameLength} characters long.";

        public ICommand SayHello => _sayHello;
        public ICommand Cancel => _cancelCommand;
    }
}
