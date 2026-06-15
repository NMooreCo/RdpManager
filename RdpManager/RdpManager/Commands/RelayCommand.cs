using System;
using System.Windows.Input;

namespace RdpManager.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
#if NETFRAMEWORK
        private readonly Func<bool> _canExecute;
        
        public RelayCommand(Action execute, Func<bool> canExecute = null)
#else
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
#endif
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

#if NETFRAMEWORK
        public event EventHandler CanExecuteChanged
#else
        public event EventHandler? CanExecuteChanged
#endif
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

#if NETFRAMEWORK
        public bool CanExecute(object parameter)
#else
        public bool CanExecute(object? parameter)
#endif
        {
            return _canExecute?.Invoke() ?? true;
        }

#if NETFRAMEWORK
        public void Execute(object parameter)
#else
        public void Execute(object? parameter)
#endif
        {
            _execute();
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
#if NETFRAMEWORK
        private readonly Predicate<T> _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
#else
        private readonly Predicate<T>? _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
#endif
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

#if NETFRAMEWORK
        public event EventHandler CanExecuteChanged
#else
        public event EventHandler? CanExecuteChanged
#endif
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

#if NETFRAMEWORK
        public bool CanExecute(object parameter)
#else
        public bool CanExecute(object? parameter)
#endif
        {
            return _canExecute?.Invoke((T)parameter) ?? true;
        }

#if NETFRAMEWORK
        public void Execute(object parameter)
#else
        public void Execute(object? parameter)
#endif
        {
            _execute((T)parameter);
        }
    }
}