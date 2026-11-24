using System;

namespace FolderScannerUI.Models;

public static class SizeFormatter
{
   private static readonly string[] Suffixes = ["B", "KB", "MB", "GB", "TB"];

   public static string Format(long bytes)
   {
      if (bytes <= 0) return "0 B";

      var suffixIndex = 0;
      double size = bytes;

      while (size >= 1024 && suffixIndex < Suffixes.Length - 1)
      {
         size /= 1024;
         suffixIndex++;
      }

      return suffixIndex == 0
         ? $"{bytes} B"
         : $"{size:N2} {Suffixes[suffixIndex]}";
   }
}

public class FileSystemItemInfo
{
   public string Name { get; set; } = string.Empty;
   public string FullPath { get; set; } = string.Empty;
   public string RelativePath { get; set; } = string.Empty;
   public string Extension { get; set; } = string.Empty;
   public long SizeInBytes { get; set; }
   public DateTime CreatedDate { get; set; }
   public bool IsDirectory { get; set; }

   public double Percentage { get; set; }
   public double BarWidthRatio => Math.Clamp(Percentage / 100.0, 0.0, 1.0);
   public string PercentageText => $"{Percentage:F1}%";
   public string FormattedSize => SizeFormatter.Format(SizeInBytes);
   public string CreatedDateText => CreatedDate.ToString("yyyy-MM-dd HH:mm");

   private double SizeInMb => SizeInBytes / (1024.0 * 1024.0);
   public bool IsSizeCritical => SizeInMb >= 1000;
   public bool IsSizeHigh => SizeInMb is >= 100 and < 1000;
   public bool IsSizeMed => SizeInMb is >= 10 and < 100;
   public bool IsSizeLow => SizeInMb < 10;
}
