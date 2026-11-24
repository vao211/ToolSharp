using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FolderScannerUI.Models;

namespace FolderScannerUI.Services;

public static class ExportService
{
   private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

   public static void Export(
      string outputPath,
      ExportFormat format,
      IReadOnlyList<FileSystemItemInfo> items,
      string rootPath,
      string scanModeLabel,
      LocalizationService loc)
   {
      switch (format)
      {
         case ExportFormat.Csv:
            ExportToCsv(outputPath, items);
            break;
         case ExportFormat.Txt:
            ExportToTxt(outputPath, items, rootPath, scanModeLabel, loc);
            break;
         case ExportFormat.Json:
            ExportToJson(outputPath, items, rootPath, scanModeLabel);
            break;
      }
   }

   private static void ExportToCsv(string outputPath, IReadOnlyList<FileSystemItemInfo> items)
   {
      var sb = new StringBuilder();
      sb.AppendLine("Type,Name,Extension,Size (Formatted),Size (MB),Size (Bytes),Share (%),Created Date,Full Path");

      foreach (var item in items)
      {
         var type = item.IsDirectory ? "Folder" : "File";
         var sizeMb = item.SizeInBytes / (1024.0 * 1024.0);
         var safeName = EscapeCsv(item.Name);
         var safePath = EscapeCsv(item.FullPath);

         sb.AppendLine(
            $"{type},\"{safeName}\",\"{item.Extension}\",\"{item.FormattedSize}\",{sizeMb:F2},{item.SizeInBytes},{item.Percentage:F2},\"{item.CreatedDate:yyyy-MM-dd HH:mm:ss}\",\"{safePath}\"");
      }

      // UTF-8 with BOM so Excel opens Vietnamese characters cleanly
      File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(true));
   }

   private static void ExportToTxt(
      string outputPath,
      IReadOnlyList<FileSystemItemInfo> items,
      string rootPath,
      string scanModeLabel,
      LocalizationService loc)
   {
      var sb = new StringBuilder();

      sb.AppendLine("========================================================================================");
      sb.AppendLine($"  {loc["TxtReportTitle"]}");
      sb.AppendLine("========================================================================================");
      sb.AppendLine();
      sb.AppendLine($"{loc["TxtRootFolder"]}: {rootPath}");
      sb.AppendLine($"{loc["TxtScanMode"]}: {scanModeLabel}");
      sb.AppendLine($"{loc["TxtScanTime"]}: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
      sb.AppendLine();
      sb.AppendLine("----------------------------------------------------------------------------------------");
      sb.AppendLine(
         $"{loc["TxtColIndex"],-6} {loc["TxtColType"],-10} {loc["TxtColName"],-40} {loc["TxtColSize"],-14} {loc["TxtColDate"],-18}");
      sb.AppendLine("----------------------------------------------------------------------------------------");

      var index = 1;
      foreach (var item in items)
      {
         var type = item.IsDirectory ? $"[{loc["BadgeFolder"]}]" : $"[{loc["BadgeFile"]}]";
         var name = item.Name.Length > 38 ? item.Name[..35] + "..." : item.Name;
         sb.AppendLine(
            $"{index,-6} {type,-10} {name,-40} {item.FormattedSize,-14} {item.CreatedDate:yyyy-MM-dd HH:mm}");
         index++;
      }

      sb.AppendLine("----------------------------------------------------------------------------------------");
      sb.AppendLine();
      sb.AppendLine($"{loc["TxtSummary"]}:");
      sb.AppendLine($"  - {loc["TxtTotalItems"]}: {items.Count}");
      sb.AppendLine($"  - {loc["TxtFolders"]}: {items.Count(x => x.IsDirectory)}");
      sb.AppendLine($"  - {loc["TxtFiles"]}: {items.Count(x => !x.IsDirectory)}");
      sb.AppendLine($"  - {loc["TxtTotalSize"]}: {SizeFormatter.Format(items.Sum(x => x.SizeInBytes))}");
      sb.AppendLine("========================================================================================");

      File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
   }

   private static void ExportToJson(
      string outputPath,
      IReadOnlyList<FileSystemItemInfo> items,
      string rootPath,
      string scanModeLabel)
   {
      var payload = new
      {
         RootPath = rootPath,
         ScanMode = scanModeLabel,
         ExportedAt = DateTime.Now,
         TotalItems = items.Count,
         TotalBytes = items.Sum(x => x.SizeInBytes),
         Items = items.Select(x => new
         {
            Type = x.IsDirectory ? "Folder" : "File",
            x.Name,
            x.Extension,
            x.SizeInBytes,
            x.FormattedSize,
            SharePercent = Math.Round(x.Percentage, 2),
            x.CreatedDate,
            x.FullPath
         })
      };

      var json = JsonSerializer.Serialize(payload, JsonOpts);
      File.WriteAllText(outputPath, json, Encoding.UTF8);
   }

   private static string EscapeCsv(string value) => value.Replace("\"", "\"\"");
}
