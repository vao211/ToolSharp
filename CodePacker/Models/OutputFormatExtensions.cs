using System;

namespace CodePacker.Models;

public static class OutputFormatExtensions
{
   public static string GetExtension(this OutputFormat format)
   {
      return format switch
      {
         OutputFormat.Markdown => ".md",
         OutputFormat.Xml => ".xml",
         OutputFormat.Json => ".json",
         _ => ".txt"
      };
   }

   public static string EnsureExtension(string fileName, OutputFormat format)
   {
      var ext = format.GetExtension();
      var cleanName = string.IsNullOrWhiteSpace(fileName) ? "project_code" : fileName.Trim();

      return cleanName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
         ? cleanName
         : cleanName + ext;
   }
}