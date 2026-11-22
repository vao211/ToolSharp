using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CodePacker.Models;

public enum OutputFormat
{
   Markdown,
   Xml,
   PlainText,
   Json
}

public class AppSettings
{
   private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
   public string DefaultOutputFileName { get; set; } = "project_code";
   public OutputFormat DefaultFormat { get; set; } = OutputFormat.Markdown;
   public bool SkipBinaryFiles { get; set; } = true;
   public bool IsDarkMode { get; set; } = true;
   public string LanguageFile { get; set; } = "vi_vn.json";
   public string? LastRegisteredPath { get; set; }

   public bool SyncOutputFolderWithSource { get; set; } = true;

   public List<string> ExcludedExtensions { get; set; } =
   [
      ".exe", ".dll", ".pdb", ".so", ".dylib", ".bin", ".obj",
      ".png", ".jpg", ".jpeg", ".gif", ".ico", ".webp", ".svg",
      ".mp3", ".mp4", ".wav", ".pdf", ".zip", ".7z", ".rar", ".tar", ".gz",
      ".woff", ".woff2", ".ttf", ".eot", ".db", ".sqlite", ".suo", ".user"
   ];

   public List<string> ExcludedNames { get; set; } =
   [
      ".git", ".vs", ".vscode", ".idea", "node_modules",
      "bin", "obj", "dist", "build", "target", "__pycache__", ".venv",
      "package-lock.json", "yarn.lock", "pnpm-lock.yaml", ".DS_Store", "project_code.txt"
   ];

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
      var json = JsonSerializer.Serialize(this, JsonOpts);
      File.WriteAllText(SettingsFilePath, json);
   }
}