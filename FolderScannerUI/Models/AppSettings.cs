using System;
using System.IO;
using System.Text.Json;

namespace FolderScannerUI.Models;

public class AppSettings
{
   private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

   public bool IsDarkMode { get; set; } = true;
   public string LanguageFile { get; set; } = "vi_vn.json";
   public string LastFolderPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   public ScanMode LastScanMode { get; set; } = ScanMode.FolderSize;
   public bool IncludeFilesInFolderScan { get; set; } = true;
   public bool DeepScanIncludeFolders { get; set; } = false;
   public string LastExtensionFilter { get; set; } = ".mp4, .mkv";

   private static string SettingsFilePath => Path.Combine(AppContext.BaseDirectory, "setting.json");

   public static AppSettings Load()
   {
      try
      {
         if (File.Exists(SettingsFilePath))
         {
            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
         }
      }
      catch
      {
      }

      var defaults = new AppSettings();
      defaults.Save();
      return defaults;
   }

   public void Save()
   {
      try
      {
         var json = JsonSerializer.Serialize(this, JsonOpts);
         File.WriteAllText(SettingsFilePath, json);
      }
      catch
      {
      }
   }
}
