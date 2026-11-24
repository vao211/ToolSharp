using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Data;

namespace MyWidget.Services;

public record LanguageOption(string FileName, string DisplayName)
{
   public override string ToString() => DisplayName;
}

public class LocalizationService : INotifyPropertyChanged
{
   public static LocalizationService Instance { get; } = new();

   private Dictionary<string, string> _fallback = new(StringComparer.OrdinalIgnoreCase);
   private Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

   private LocalizationService()
   {
      ScanLanguages();
      LoadFallback();
   }

   public string LangFolder => Path.Combine(AppContext.BaseDirectory, "lang");

   public List<LanguageOption> AvailableLanguages { get; private set; } = [];

   public string CurrentLanguageFile { get; private set; } = "en_us.json";

   public string this[string key] =>
      _strings.TryGetValue(key, out var val) ? val :
      _fallback.TryGetValue(key, out var fb) ? fb : key;

   public event PropertyChangedEventHandler? PropertyChanged;
   public event EventHandler? LanguageChanged;

   public string Format(string key, params object[] args)
   {
      var template = this[key];
      try
      {
         return string.Format(template, args);
      }
      catch
      {
         return template;
      }
   }

   public void ScanLanguages()
   {
      var list = new List<LanguageOption>();
      try
      {
         if (!Directory.Exists(LangFolder))
            Directory.CreateDirectory(LangFolder);

         foreach (var file in Directory.EnumerateFiles(LangFolder, "*.json"))
         {
            try
            {
               var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
               if (dict is null) continue;

               var fileName = Path.GetFileName(file);
               var displayName = dict.TryGetValue("language_name", out var name) && !string.IsNullOrWhiteSpace(name)
                  ? name
                  : Path.GetFileNameWithoutExtension(file);

               list.Add(new LanguageOption(fileName, displayName));
            }
            catch
            {
               // Skip malformed JSON files
            }
         }
      }
      catch
      {
      }

      AvailableLanguages = [.. list.OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
      LoadFallback();
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableLanguages)));
   }

   private void LoadFallback()
   {
      var enPath = Path.Combine(LangFolder, "en_us.json");
      if (!File.Exists(enPath)) return;

      try
      {
         var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(enPath));
         if (raw is not null)
            _fallback = new Dictionary<string, string>(raw, StringComparer.OrdinalIgnoreCase);
      }
      catch
      {
      }
   }

   public void LoadLanguage(string fileName)
   {
      if (string.IsNullOrWhiteSpace(fileName))
         fileName = "en_us.json";

      var path = Path.Combine(LangFolder, fileName);
      if (!File.Exists(path) && AvailableLanguages.Count > 0)
      {
         fileName = AvailableLanguages[0].FileName;
         path = Path.Combine(LangFolder, fileName);
      }

      CurrentLanguageFile = fileName;

      if (File.Exists(path))
      {
         try
         {
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            _strings = raw is not null
               ? new Dictionary<string, string>(raw, StringComparer.OrdinalIgnoreCase)
               : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
         }
         catch
         {
            _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
         }
      }

      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
      LanguageChanged?.Invoke(this, EventArgs.Empty);
   }
}
