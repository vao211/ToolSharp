using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using AutoKeySpammer.ViewModels;
using AutoKeySpammer.Views;

namespace AutoKeySpammer;

public partial class App : Application
{
   public override void Initialize()
   {
      AvaloniaXamlLoader.Load(this);
   }

   public override void OnFrameworkInitializationCompleted()
   {
      if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
      {
         desktop.MainWindow = new MainWindow
         {
            DataContext = new MainWindowViewModel(),
         };
      }

      base.OnFrameworkInitializationCompleted();
   }
}