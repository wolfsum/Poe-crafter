using System.Windows.Input;

namespace Poe2Crafter.ViewModels;

public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    public event Action? Executed;

    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _)    { Executed?.Invoke(); execute(); }
}

public class RelayCommand<T>(Action<T> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? _) => true;
    public void Execute(object? parameter) { if (parameter is T t) execute(t); }
}

// RelayCommand with manual CanExecuteChanged refresh
public class RefreshableCommand(Action execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public bool CanExecute(object? _) => canExecute();
    public void Execute(object? _)    => execute();
}
