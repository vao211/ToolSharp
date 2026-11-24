using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TempClear;

public static class ThemeManager
{
   private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
   private const int DwmwaUseImmersiveDarkMode = 20;

   public static bool IsDarkMode { get; private set; }

   [DllImport("dwmapi.dll")]
   private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

   public static void ApplyTheme(bool isDark)
   {
      IsDarkMode = isDark;

      var app = Application.Current;
      if (app is null) return;

      var targetSource = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
      var newDict = new ResourceDictionary
      {
         Source = new Uri(targetSource, UriKind.Relative)
      };

      var merged = app.Resources.MergedDictionaries;
      var existingIndex = -1;
      for (var i = 0; i < merged.Count; i++)
      {
         var src = merged[i].Source?.OriginalString;
         if (src is not null &&
             (src.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
              src.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)))
         {
            existingIndex = i;
            break;
         }
      }

      if (existingIndex >= 0)
         merged[existingIndex] = newDict;
      else
         merged.Insert(0, newDict);

      foreach (Window window in app.Windows)
      {
         ApplyTitleBarTheme(window, isDark);
      }
   }

   public static void ApplyTitleBarTheme(Window window, bool isDark)
   {
      if (!OperatingSystem.IsWindows()) return;

      var hwnd = new WindowInteropHelper(window).Handle;
      if (hwnd == IntPtr.Zero) return;

      var useDark = isDark ? 1 : 0;
      if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
      {
         DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref useDark, sizeof(int));
      }
   }
}
