using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media.Imaging;
using MyWidget.Models;

namespace MyWidget.Services;

public class DataStorageService
{
   public static DataStorageService Instance { get; } = new();

   private static readonly JsonSerializerOptions JsonOpts = new()
   {
      WriteIndented = true
   };

   private readonly object _saveLock = new();

   private DataStorageService()
   {
      DataFolder = ResolveWritableDataFolder();
      MediaFolder = Path.Combine(DataFolder, "media");
      EnsureDirectories();
   }

   public string DataFolder { get; }
   public string MediaFolder { get; }
   public string SettingsPath => Path.Combine(DataFolder, "setting.json");
   public string NotesPath => Path.Combine(DataFolder, "notes.json");

   private static string ResolveWritableDataFolder()
   {
      var portableDir = Path.Combine(AppContext.BaseDirectory, "data");
      try
      {
         Directory.CreateDirectory(portableDir);
         var probe = Path.Combine(portableDir, ".write_test");
         File.WriteAllText(probe, "ok");
         File.Delete(probe);
         return portableDir;
      }
      catch
      {
         var localDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyWidget",
            "data");
         Directory.CreateDirectory(localDir);
         return localDir;
      }
   }

   public void EnsureDirectories()
   {
      try
      {
         Directory.CreateDirectory(DataFolder);
         Directory.CreateDirectory(MediaFolder);
      }
      catch
      {
      }
   }

   public AppSettings LoadSettings()
   {
      try
      {
         if (File.Exists(SettingsPath))
         {
            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded is not null)
               return loaded;
         }
      }
      catch
      {
      }

      var defaults = new AppSettings();
      SaveSettings(defaults);
      return defaults;
   }

   public void SaveSettings(AppSettings settings)
   {
      lock (_saveLock)
      {
         try
         {
            EnsureDirectories();
            var json = JsonSerializer.Serialize(settings, JsonOpts);
            var tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, SettingsPath, overwrite: true);
         }
         catch
         {
         }
      }
   }

   public List<NoteModel> LoadNotes()
   {
      try
      {
         if (File.Exists(NotesPath))
         {
            var json = File.ReadAllText(NotesPath);
            var loaded = JsonSerializer.Deserialize<List<NoteModel>>(json);
            if (loaded is not null)
               return loaded;
         }
      }
      catch
      {
      }

      return [];
   }

   public void SaveNotes(IEnumerable<NoteModel> notes)
   {
      lock (_saveLock)
      {
         try
         {
            EnsureDirectories();
            var list = notes.ToList();
            var json = JsonSerializer.Serialize(list, JsonOpts);
            var tmp = NotesPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, NotesPath, overwrite: true);
         }
         catch
         {
         }
      }
   }

   public (string StoredPath, bool IsAppResource) ImportImageFile(string sourceFilePath, bool copyToAppData)
   {
      var fullSource = Path.GetFullPath(sourceFilePath);
      if (!File.Exists(fullSource))
         return (fullSource, false);

      if (!copyToAppData)
         return (fullSource, false);

      try
      {
         EnsureDirectories();

         // If already inside MediaFolder, store relative path directly
         if (fullSource.StartsWith(MediaFolder, StringComparison.OrdinalIgnoreCase))
         {
            var relExisting = Path.GetRelativePath(DataFolder, fullSource).Replace('\\', '/');
            return (relExisting, true);
         }

         var bytes = File.ReadAllBytes(fullSource);
         var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant();
         var ext = Path.GetExtension(fullSource);
         if (string.IsNullOrWhiteSpace(ext))
            ext = ".png";

         var destFileName = $"img_{hash}{ext.ToLowerInvariant()}";
         var destFullPath = Path.Combine(MediaFolder, destFileName);
         if (!File.Exists(destFullPath))
            File.WriteAllBytes(destFullPath, bytes);

         return ($"media/{destFileName}", true);
      }
      catch
      {
         return (fullSource, false);
      }
   }

   public (string StoredPath, bool IsAppResource) SaveClipboardBitmap(BitmapSource bitmap)
   {
      EnsureDirectories();
      using var ms = new MemoryStream();
      var encoder = new PngBitmapEncoder();
      encoder.Frames.Add(BitmapFrame.Create(bitmap));
      encoder.Save(ms);
      var bytes = ms.ToArray();

      var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant();
      var destFileName = $"clip_{hash}.png";
      var destFullPath = Path.Combine(MediaFolder, destFileName);
      if (!File.Exists(destFullPath))
         File.WriteAllBytes(destFullPath, bytes);

      return ($"media/{destFileName}", true);
   }

   public string ResolveImageFullPath(string storedPath, bool isAppResource)
   {
      if (string.IsNullOrWhiteSpace(storedPath))
         return string.Empty;

      if (isAppResource || !Path.IsPathRooted(storedPath))
      {
         var normalized = storedPath.Replace('/', Path.DirectorySeparatorChar);
         return Path.GetFullPath(Path.Combine(DataFolder, normalized));
      }

      return storedPath;
   }

   public BitmapImage? LoadBitmapSafe(string fullPath, int? decodePixelWidth = null)
   {
      try
      {
         if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            return null;

         using var fs = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

         var bmp = new BitmapImage();
         bmp.BeginInit();
         bmp.CacheOption = BitmapCacheOption.OnLoad;
         if (decodePixelWidth.HasValue && decodePixelWidth.Value > 0)
            bmp.DecodePixelWidth = decodePixelWidth.Value;
         bmp.StreamSource = fs;
         bmp.EndInit();
         bmp.Freeze();
         return bmp;
      }
      catch
      {
         return null;
      }
   }

   public int MigrateExternalImagesToAppData(IEnumerable<NoteModel> notes)
   {
      var count = 0;
      foreach (var note in notes)
      {
         foreach (var block in note.Blocks)
         {
            if (block.Type != NoteBlockType.Image || block.IsAppResource || string.IsNullOrWhiteSpace(block.ImagePath))
               continue;

            var full = ResolveImageFullPath(block.ImagePath, block.IsAppResource);
            if (!File.Exists(full))
               continue;

            var (storedPath, isAppRes) = ImportImageFile(full, copyToAppData: true);
            if (isAppRes)
            {
               block.ImagePath = storedPath;
               block.IsAppResource = true;
               count++;
            }
         }
      }

      return count;
   }

   public int CleanUnusedMedia(IEnumerable<NoteModel> notes)
   {
      EnsureDirectories();
      var usedFullPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var note in notes)
      {
         foreach (var block in note.Blocks)
         {
            if (block.Type == NoteBlockType.Image && !string.IsNullOrWhiteSpace(block.ImagePath))
            {
               var full = ResolveImageFullPath(block.ImagePath, block.IsAppResource);
               if (!string.IsNullOrWhiteSpace(full))
                  usedFullPaths.Add(full);
            }
         }
      }

      var deleted = 0;
      try
      {
         foreach (var file in Directory.EnumerateFiles(MediaFolder))
         {
            var full = Path.GetFullPath(file);
            if (!usedFullPaths.Contains(full))
            {
               try
               {
                  File.Delete(full);
                  deleted++;
               }
               catch
               {
               }
            }
         }
      }
      catch
      {
      }

      return deleted;
   }
}
