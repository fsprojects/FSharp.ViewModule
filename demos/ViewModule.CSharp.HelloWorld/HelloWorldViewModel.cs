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
        private readonly INotifyingValue<string> firstName;
        private readonly INotifyingValue<string> lastName;
        public readonly IAsyncNotifyCommand sayHello;
        private readonly INotifyCommand cancelCommand;
        private CancellationTokenSource cts = new CancellationTokenSource();

        public HelloWorldViewModel()
        {
            firstName = Factory.Backing(nameof(FirstName), "Anton", NotNullOrWhitespace.Then(NotEqual("Foo")));

            lastName = Factory.Backing(
                    nameof(LastName), 
                    "Tcholakov", 
                    NotNullOrWhitespace.Then(NotEqual("Bar")).Then(x => x.Length < 10, "Length cannot exceed 10 characters"));

            sayHello = Factory.CommandAsyncChecked(
                    Greet, 
                    () => this.IsValid && !this.OperationExecuting,
                    cts.Token, 
                    exn => MessageBox.Show("Sorry I was too slow :-(."),
                    nameof(IsValid), 
                    nameof(OperationExecuting));

            cancelCommand = Factory.CommandSyncChecked(
                    () => this.cts?.Cancel(),
                    () => this.OperationExecuting, 
                    nameof(OperationExecuting));

            DependencyTracker.AddPropertyDependencies(nameof(ReadyToGreet), nameof(OperationExecuting));
            DependencyTracker.AddPropertyDependencies(nameof(FullName), nameof(FirstName), nameof(LastName));
            DependencyTracker.AddPropertyDependencies(nameof(Greeting), nameof(FullName), nameof(NameLength));
        }

        public bool ReadyToGreet => !this.OperationExecuting && this.IsValid;
        public string FirstName { get { return firstName.Value; } set { firstName.Value = value; } }
        public string LastName { get { return lastName.Value; } set { lastName.Value = value; } }
        public string FullName => $"{FirstName} {LastName}";
        public string Greeting => $"Hello, {FullName}. Your name is {NameLength} characters long.";

        public ICommand SayHello => sayHello;
        public ICommand Cancel => cancelCommand;

        private int NameLength => FullName.Length;

        private async Task Greet(CancellationToken token)
        {
            try
            {
                await Task.Delay(2000, token);
                MessageBox.Show(Greeting);
            }
            finally
            {
                this.cts.Dispose();
                this.cts = new CancellationTokenSource();
                this.sayHello.CancellationToken = this.cts.Token;
            }
        }

        public override IEnumerable<ValidationState> Validate(string propertyName)
        {
            if (propertyName == nameof(FullName))
            {
                var errors = NotEqual("Reed Copsey").Validate(FullName, "This is a poor choice of names.");
                yield return ValidationState.NewPropertyErrors(nameof(FullName), errors);
                yield return ValidationState.NewEntityErrors(errors);
            }
        }
    }
}
