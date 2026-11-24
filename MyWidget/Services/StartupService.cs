using Microsoft.Win32;

namespace MyWidget.Services;

public static class StartupService
{
   private const string RunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
   private const string AppRegistryName = "MyWidget";

   public static bool IsStartupEnabled()
   {
      try
      {
         using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
         var val = key?.GetValue(AppRegistryName) as string;
         return !string.IsNullOrWhiteSpace(val);
      }
      catch
      {
         return false;
      }
   }

   public static bool SetStartup(bool enable)
   {
      try
      {
         using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true);
         if (key is null) return false;

         if (enable)
         {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
               return false;

            key.SetValue(AppRegistryName, $"\"{exePath}\" --startup");
         }
         else
         {
            key.DeleteValue(AppRegistryName, throwOnMissingValue: false);
         }

         return true;
      }
      catch
      {
         return false;
      }
   }
}
