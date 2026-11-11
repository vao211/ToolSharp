using Avalonia.Controls;
using Avalonia.Input;
using System.Windows.Input;
using AutoKeySpammer.ViewModels;

namespace AutoKeySpammer.Views;

public partial class MainWindow : Window
{
   public MainWindow()
   {
      InitializeComponent();
      this.KeyDown += OnWindowKeyDown;
   }

   private void OnWindowKeyDown(object? sender, KeyEventArgs e)
   {
      if (DataContext is MainWindowViewModel vm)
      {
         if (vm.IsWaitingForKey)
         {
            vm.SetKey(e.Key.ToString());
            e.Handled = true;
            return;
         }

         if (e.Key == Key.Escape)
         {
            ICommand stopCmd = vm.StopCommand;
            if (stopCmd.CanExecute(null))
            {
               stopCmd.Execute(null);
            }
         }
      }
   }
}