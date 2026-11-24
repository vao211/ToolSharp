using System.Windows;

namespace TempClear;

public partial class MainWindow : Window
{
   public MainWindow()
   {
      InitializeComponent();
      SourceInitialized += (_, _) => ThemeManager.ApplyTitleBarTheme(this, ThemeManager.IsDarkMode);
   }
}