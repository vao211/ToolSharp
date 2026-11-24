using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderScannerUI.Models;

namespace FolderScannerUI.Services;

public record ScanProgressReport(string CurrentFolder, int ItemsFound);

public record ScanResult(
   List<FileSystemItemInfo> Items,
   long TotalBytes,
   int FolderCount,
   int FileCount,
   int SkippedCount,
   TimeSpan Duration);

public static class FolderScannerService
{
   public static Task<ScanResult> ScanAsync(
      string rootPath,
      ScanMode mode,
      bool includeFilesInFolderScan,
      bool deepScanIncludeFolders,
      string extensionFilter,
      IProgress<ScanProgressReport>? progress,
      CancellationToken cancellationToken)
   {
      return Task.Run(() =>
      {
         var sw = Stopwatch.StartNew();
         var skippedCount = 0;
         var items = new List<FileSystemItemInfo>();
         var rootFullPath = Path.GetFullPath(rootPath);

         switch (mode)
         {
            case ScanMode.FolderSize:
               items = ScanFolderSummary(rootFullPath, includeFilesInFolderScan, progress, ref skippedCount,
                  cancellationToken);
               break;

            case ScanMode.DeepScan:
               items = PerformDeepScan(rootFullPath, deepScanIncludeFolders, progress, ref skippedCount,
                  cancellationToken);
               break;

            case ScanMode.ExtensionFilter:
               var extensions = ParseExtensions(extensionFilter);
               items = PerformExtensionFilter(rootFullPath, extensions, progress, ref skippedCount,
                  cancellationToken);
               break;
         }

         sw.Stop();

         long totalBytes;
         if (mode == ScanMode.DeepScan && deepScanIncludeFolders)
         {
            // Avoid double-counting folders + their nested files
            totalBytes = items.Where(x => !x.IsDirectory).Sum(x => x.SizeInBytes);
         }
         else
         {
            totalBytes = items.Sum(x => x.SizeInBytes);
         }

         if (totalBytes > 0)
         {
            foreach (var item in items)
               item.Percentage = Math.Min(100.0, item.SizeInBytes * 100.0 / totalBytes);
         }

         var folderCount = items.Count(x => x.IsDirectory);
         var fileCount = items.Count(x => !x.IsDirectory);

         return new ScanResult(items, totalBytes, folderCount, fileCount, skippedCount, sw.Elapsed);
      }, cancellationToken);
   }

   private static HashSet<string> ParseExtensions(string raw)
   {
      var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (string.IsNullOrWhiteSpace(raw)) return set;

      var parts = raw.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      foreach (var part in parts)
      {
         var ext = part.Trim();
         if (string.IsNullOrEmpty(ext)) continue;
         if (!ext.StartsWith('.')) ext = "." + ext;
         set.Add(ext);
      }

      return set;
   }

   private static List<FileSystemItemInfo> ScanFolderSummary(
      string rootPath,
      bool includeFiles,
      IProgress<ScanProgressReport>? progress,
      ref int skippedCount,
      CancellationToken ct)
   {
      var items = new List<FileSystemItemInfo>();
      var reportCounter = 0;

      try
      {
         foreach (var dir in Directory.EnumerateDirectories(rootPath))
         {
            ct.ThrowIfCancellationRequested();

            try
            {
               var dirInfo = new DirectoryInfo(dir);
               if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                  continue;

               progress?.Report(new ScanProgressReport(dirInfo.Name, items.Count));
               var size = CalculateDirectorySize(dir, ref skippedCount, ct);

               items.Add(new FileSystemItemInfo
               {
                  Name = dirInfo.Name,
                  FullPath = dirInfo.FullName,
                  RelativePath = dirInfo.Name,
                  Extension = string.Empty,
                  SizeInBytes = size,
                  CreatedDate = dirInfo.CreationTime,
                  IsDirectory = true
               });
            }
            catch (OperationCanceledException)
            {
               throw;
            }
            catch
            {
               skippedCount++;
            }
         }

         if (includeFiles)
         {
            foreach (var file in Directory.EnumerateFiles(rootPath))
            {
               ct.ThrowIfCancellationRequested();

               try
               {
                  var fi = new FileInfo(file);
                  reportCounter++;
                  if (reportCounter % 25 == 0)
                     progress?.Report(new ScanProgressReport(fi.Name, items.Count));

                  items.Add(new FileSystemItemInfo
                  {
                     Name = fi.Name,
                     FullPath = fi.FullName,
                     RelativePath = fi.Name,
                     Extension = fi.Extension.ToLowerInvariant(),
                     SizeInBytes = fi.Length,
                     CreatedDate = fi.CreationTime,
                     IsDirectory = false
                  });
               }
               catch (OperationCanceledException)
               {
                  throw;
               }
               catch
               {
                  skippedCount++;
               }
            }
         }
      }
      catch (OperationCanceledException)
      {
         throw;
      }
      catch
      {
         skippedCount++;
      }

      return items;
   }

   private static long CalculateDirectorySize(string rootDir, ref int skippedCount, CancellationToken ct)
   {
      long totalSize = 0;
      var stack = new Stack<string>();
      stack.Push(rootDir);

      while (stack.Count > 0)
      {
         ct.ThrowIfCancellationRequested();
         var current = stack.Pop();

         try
         {
            foreach (var file in Directory.EnumerateFiles(current))
            {
               ct.ThrowIfCancellationRequested();
               try
               {
                  totalSize += new FileInfo(file).Length;
               }
               catch
               {
                  skippedCount++;
               }
            }

            foreach (var subDir in Directory.EnumerateDirectories(current))
            {
               try
               {
                  var attrs = File.GetAttributes(subDir);
                  if ((attrs & FileAttributes.ReparsePoint) == 0)
                     stack.Push(subDir);
               }
               catch
               {
                  skippedCount++;
               }
            }
         }
         catch (OperationCanceledException)
         {
            throw;
         }
         catch
         {
            skippedCount++;
         }
      }

      return totalSize;
   }

   private static List<FileSystemItemInfo> PerformDeepScan(
      string rootPath,
      bool includeFolders,
      IProgress<ScanProgressReport>? progress,
      ref int skippedCount,
      CancellationToken ct)
   {
      var items = new List<FileSystemItemInfo>();
      var folderEntries = new Dictionary<string, FileSystemItemInfo>(StringComparer.OrdinalIgnoreCase);
      var stack = new Stack<string>();
      stack.Push(rootPath);
      var dirCounter = 0;

      while (stack.Count > 0)
      {
         ct.ThrowIfCancellationRequested();
         var currentDir = stack.Pop();
         dirCounter++;
         if (dirCounter % 15 == 0 || dirCounter == 1)
         {
            var label = Path.GetFileName(currentDir);
            if (string.IsNullOrEmpty(label)) label = currentDir;
            progress?.Report(new ScanProgressReport(label, items.Count));
         }

         try
         {
            foreach (var filePath in Directory.EnumerateFiles(currentDir))
            {
               ct.ThrowIfCancellationRequested();
               try
               {
                  var fi = new FileInfo(filePath);
                  var rel = Path.GetRelativePath(rootPath, fi.FullName);

                  items.Add(new FileSystemItemInfo
                  {
                     Name = fi.Name,
                     FullPath = fi.FullName,
                     RelativePath = rel,
                     Extension = fi.Extension.ToLowerInvariant(),
                     SizeInBytes = fi.Length,
                     CreatedDate = fi.CreationTime,
                     IsDirectory = false
                  });

                  if (includeFolders)
                  {
                     var parent = fi.DirectoryName;
                     while (!string.IsNullOrEmpty(parent) &&
                            parent.Length > rootPath.Length &&
                            folderEntries.TryGetValue(parent, out var dirItem))
                     {
                        dirItem.SizeInBytes += fi.Length;
                        parent = Path.GetDirectoryName(parent);
                     }
                  }
               }
               catch (OperationCanceledException)
               {
                  throw;
               }
               catch
               {
                  skippedCount++;
               }
            }

            foreach (var subDir in Directory.EnumerateDirectories(currentDir))
            {
               try
               {
                  var di = new DirectoryInfo(subDir);
                  if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                     continue;

                  stack.Push(subDir);

                  if (includeFolders)
                  {
                     var rel = Path.GetRelativePath(rootPath, di.FullName);
                     var dirEntry = new FileSystemItemInfo
                     {
                        Name = di.Name,
                        FullPath = di.FullName,
                        RelativePath = rel,
                        Extension = string.Empty,
                        SizeInBytes = 0,
                        CreatedDate = di.CreationTime,
                        IsDirectory = true
                     };
                     folderEntries[di.FullName] = dirEntry;
                     items.Add(dirEntry);
                  }
               }
               catch
               {
                  skippedCount++;
               }
            }
         }
         catch (OperationCanceledException)
         {
            throw;
         }
         catch
         {
            skippedCount++;
         }
      }

      return items;
   }

   private static List<FileSystemItemInfo> PerformExtensionFilter(
      string rootPath,
      HashSet<string> extensions,
      IProgress<ScanProgressReport>? progress,
      ref int skippedCount,
      CancellationToken ct)
   {
      var items = new List<FileSystemItemInfo>();
      if (extensions.Count == 0) return items;

      var stack = new Stack<string>();
      stack.Push(rootPath);
      var dirCounter = 0;

      while (stack.Count > 0)
      {
         ct.ThrowIfCancellationRequested();
         var currentDir = stack.Pop();
         dirCounter++;
         if (dirCounter % 15 == 0 || dirCounter == 1)
         {
            var label = Path.GetFileName(currentDir);
            if (string.IsNullOrEmpty(label)) label = currentDir;
            progress?.Report(new ScanProgressReport(label, items.Count));
         }

         try
         {
            foreach (var filePath in Directory.EnumerateFiles(currentDir))
            {
               ct.ThrowIfCancellationRequested();
               try
               {
                  var ext = Path.GetExtension(filePath);
                  if (!extensions.Contains(ext))
                     continue;

                  var fi = new FileInfo(filePath);
                  items.Add(new FileSystemItemInfo
                  {
                     Name = fi.Name,
                     FullPath = fi.FullName,
                     RelativePath = Path.GetRelativePath(rootPath, fi.FullName),
                     Extension = fi.Extension.ToLowerInvariant(),
                     SizeInBytes = fi.Length,
                     CreatedDate = fi.CreationTime,
                     IsDirectory = false
                  });
               }
               catch (OperationCanceledException)
               {
                  throw;
               }
               catch
               {
                  skippedCount++;
               }
            }

            foreach (var subDir in Directory.EnumerateDirectories(currentDir))
            {
               try
               {
                  var attrs = File.GetAttributes(subDir);
                  if ((attrs & FileAttributes.ReparsePoint) == 0)
                     stack.Push(subDir);
               }
               catch
               {
                  skippedCount++;
               }
            }
         }
         catch (OperationCanceledException)
         {
            throw;
         }
         catch
         {
            skippedCount++;
         }
      }

      return items;
   }
}
