using System.IO;
using System.Text.Json;

namespace TempClear;

public class AppSettings
{
   private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

   public bool IsDarkMode { get; set; }

   private static string PrimarySettingsPath => Path.Combine(AppContext.BaseDirectory, "setting.json");

   private static string FallbackSettingsPath => Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "TempClear",
      "setting.json");

   public static AppSettings Load()
   {
      foreach (var path in new[] { PrimarySettingsPath, FallbackSettingsPath })
      {
         try
         {
            if (File.Exists(path))
            {
               var json = File.ReadAllText(path);
               return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
         }
         catch
         {
         }
      }

      var defaults = new AppSettings();
      defaults.Save();
      return defaults;
   }

   public void Save()
   {
      var json = JsonSerializer.Serialize(this, JsonOpts);
      try
      {
         File.WriteAllText(PrimarySettingsPath, json);
         return;
      }
      catch
      {
      }

      try
      {
         var dir = Path.GetDirectoryName(FallbackSettingsPath);
         if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
         File.WriteAllText(FallbackSettingsPath, json);
      }
      catch
      {
      }
   }
}
