using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;

namespace TempClear;

public class ViewModelBase : INotifyPropertyChanged
{
   public event PropertyChangedEventHandler? PropertyChanged;

   protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
   {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
   }
}

public class RelayCommand : ICommand
{
   private readonly Predicate<object> _canExecute;
   private readonly Action<object> _execute;

   public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
   {
      _execute = execute;
      _canExecute = canExecute;
   }

   public bool CanExecute(object parameter)
   {
      return _canExecute == null || _canExecute(parameter);
   }

   public void Execute(object parameter)
   {
      _execute(parameter);
   }

   public event EventHandler CanExecuteChanged
   {
      add => CommandManager.RequerySuggested += value;
      remove => CommandManager.RequerySuggested -= value;
   }
}

public class InverseBooleanConverter : IValueConverter
{
   public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
   {
      if (value is bool boolValue) return !boolValue;
      return true;
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
   {
      throw new NotImplementedException();
   }
}