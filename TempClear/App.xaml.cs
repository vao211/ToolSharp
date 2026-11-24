using System.Windows;

namespace TempClear;

/// <summary>
///    Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
   protected override void OnStartup(StartupEventArgs e)
   {
      base.OnStartup(e);
      var settings = AppSettings.Load();
      ThemeManager.ApplyTheme(settings.IsDarkMode);
   }
}