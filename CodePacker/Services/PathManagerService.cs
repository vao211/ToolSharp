using System;
using System.IO;
using System.Linq;
using CodePacker.Models;

namespace CodePacker.Services;

public enum PathUpdateResult
{
   AlreadyExists,
   Added,
   Updated
}

public static class PathManagerService
{
   private const string LinuxMarker = "# CodePacker-PATH";

   public static PathUpdateResult RegisterOrUpdatePath(AppSettings settings)
   {
      var currentDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

      var result = OperatingSystem.IsWindows()
         ? RegisterWindows(currentDir, settings.LastRegisteredPath)
         : RegisterLinux(currentDir);

      if (settings.LastRegisteredPath != currentDir)
      {
         settings.LastRegisteredPath = currentDir;
         settings.Save();
      }

      return result;
   }

   private static PathUpdateResult RegisterWindows(string currentDir, string? previousDir)
   {
      var rawPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
      var entries = rawPath.Split(';', StringSplitOptions.RemoveEmptyEntries)
         .Select(p => p.Trim())
         .ToList();

      var alreadyExists = entries.Any(p =>
         string.Equals(p.TrimEnd('\\'), currentDir, StringComparison.OrdinalIgnoreCase));

      var updatedOld = false;
      if (!string.IsNullOrWhiteSpace(previousDir) &&
          !string.Equals(previousDir.TrimEnd('\\'), currentDir, StringComparison.OrdinalIgnoreCase))
      {
         var removed = entries.RemoveAll(p =>
            string.Equals(p.TrimEnd('\\'), previousDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
         updatedOld = removed > 0;
      }

      if (alreadyExists && !updatedOld)
         return PathUpdateResult.AlreadyExists;

      if (!alreadyExists)
         entries.Add(currentDir);

      Environment.SetEnvironmentVariable("Path", string.Join(';', entries), EnvironmentVariableTarget.User);
      return updatedOld ? PathUpdateResult.Updated : PathUpdateResult.Added;
   }

   private static PathUpdateResult RegisterLinux(string currentDir)
   {
      var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      var exportLine = $"export PATH=\"$PATH:{currentDir}\" {LinuxMarker}";

      string[] rcFiles = [".bashrc", ".zshrc", ".profile"];
      var targetFiles = rcFiles.Select(f => Path.Combine(home, f))
         .Where(File.Exists)
         .ToList();

      if (targetFiles.Count == 0)
         targetFiles.Add(Path.Combine(home, ".bashrc"));

      var anyUpdated = false;
      var anyAdded = false;

      foreach (var file in targetFiles)
      {
         var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : [];
         var idx = lines.FindIndex(l => l.Contains(LinuxMarker, StringComparison.Ordinal));

         if (idx >= 0)
         {
            if (lines[idx].Trim() == exportLine) continue;
            lines[idx] = exportLine;
            File.WriteAllLines(file, lines);
            anyUpdated = true;
         }
         else
         {
            lines.Add(exportLine);
            File.WriteAllLines(file, lines);
            anyAdded = true;
         }
      }

      if (anyUpdated) return PathUpdateResult.Updated;
      if (anyAdded) return PathUpdateResult.Added;
      return PathUpdateResult.AlreadyExists;
   }
}