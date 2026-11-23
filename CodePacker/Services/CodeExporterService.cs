using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodePacker.Models;

namespace CodePacker.Services;

public record ExportResult(int FileCount, string OutputFullPath, int OutputFileCount = 1);

public static class CodeExporterService
{
    private record FileItem(string FullPath, string RelPath);

    private static readonly string[] PriorityRootFiles =
    [
        "readme.md", "dockerfile", "docker-compose.yml",
        "package.json", "tsconfig.json", "program.cs", "main.py", "index.ts", "index.js"
    ];

    public static async Task<ExportResult> ExportAsync(
        string sourceDir,
        string outputFullPath,
        OutputFormat format,
        IEnumerable<string> excludedExtensions,
        IEnumerable<string> excludedNames,
        bool skipBinary,
        bool exportByModule = false,
        CancellationToken ct = default)
    {
        var extSet = new HashSet<string>(
            excludedExtensions.Select(NormalizeExt),
            StringComparer.OrdinalIgnoreCase);

        var nameSet = new HashSet<string>(excludedNames, StringComparer.OrdinalIgnoreCase);

        var outDir = Path.GetDirectoryName(outputFullPath) ?? sourceDir;
        var ext = format.GetExtension();
        var outFile = OutputFormatExtensions.EnsureExtension(Path.GetFileName(outputFullPath), format);
        var baseName = outFile[..^ext.Length]; // Tên gốc chưa có đuôi format
        var normalizedOutput = Path.GetFullPath(Path.Combine(outDir, outFile));
        var normalizedOutDir = Path.GetFullPath(outDir);

        var files = new List<string>();
        CollectFiles(sourceDir, normalizedOutput, normalizedOutDir, baseName, ext, exportByModule, extSet, nameSet, skipBinary, files, ct);

        var sortedFiles = files
            .Select(full => new FileItem(full, Path.GetRelativePath(sourceDir, full).Replace('\\', '/')))
            .OrderBy(f => GetSortPriority(f.RelPath))
            .ThenBy(f => Path.GetDirectoryName(f.RelPath)?.Replace('\\', '/') ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => Path.GetFileName(f.RelPath), StringComparer.OrdinalIgnoreCase)
            .ToList();

        Directory.CreateDirectory(normalizedOutDir);
        var projectName = Path.GetFileName(sourceDir.TrimEnd('\\', '/'));

        if (!exportByModule)
        {
            await WriteBundleAsync(normalizedOutput, projectName, format, sortedFiles, ct);
            return new ExportResult(sortedFiles.Count, normalizedOutput, 1);
        }

        var groups = sortedFiles
            .GroupBy(f => Path.GetDirectoryName(f.RelPath)?.Replace('\\', '/') ?? "")
            .ToList();

        var moduleEntries = new List<(string ModulePath, string FileName, int FileCount)>();

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();
            var moduleSuffix = SanitizeModuleName(group.Key);
            var moduleFileName = $"{baseName}_{moduleSuffix}{ext}";
            var moduleFilePath = Path.Combine(normalizedOutDir, moduleFileName);
            var moduleTitle = string.IsNullOrEmpty(group.Key) ? $"{projectName} (root)" : $"{projectName}/{group.Key}";

            await WriteBundleAsync(moduleFilePath, moduleTitle, format, [.. group], ct);
            moduleEntries.Add((string.IsNullOrEmpty(group.Key) ? "(root)" : group.Key, moduleFileName, group.Count()));
        }

        // Mặc định xuất thêm file cấu trúc tổng thể của toàn dự án
        var structureFilePath = Path.Combine(normalizedOutDir, $"{baseName}_structure{ext}");
        await WriteStructureFileAsync(structureFilePath, projectName, format, sortedFiles, moduleEntries, ct);

        return new ExportResult(sortedFiles.Count, normalizedOutDir, groups.Count + 1);
    }

    private static string SanitizeModuleName(string relDir)
    {
        if (string.IsNullOrWhiteSpace(relDir))
            return "root";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(relDir.Length);
        foreach (var ch in relDir)
        {
            sb.Append(ch == '/' || ch == '\\' || invalidChars.Contains(ch) ? '_' : ch);
        }
        return sb.ToString();
    }

    private static async Task WriteBundleAsync(
        string targetFilePath,
        string bundleTitle,
        OutputFormat format,
        List<FileItem> items,
        CancellationToken ct)
    {
        var tree = BuildDirectoryTree(bundleTitle, items.Select(f => f.RelPath));

        if (format == OutputFormat.Json)
        {
            var jsonItems = new List<object>();
            foreach (var f in items)
            {
                ct.ThrowIfCancellationRequested();
                var content = await File.ReadAllTextAsync(f.FullPath, ct);
                jsonItems.Add(new { path = f.RelPath, lines = content.Split('\n').Length, content });
            }

            var payload = new
            {
                project = bundleTitle,
                exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                totalFiles = items.Count,
                tree,
                files = jsonItems
            };
            await File.WriteAllTextAsync(targetFilePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8, ct);
            return;
        }

        var sb = new StringBuilder();
        WriteHeader(sb, format, bundleTitle, items.Count, tree);

        foreach (var f in items)
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(f.FullPath, ct);
            AppendFileBlock(sb, format, f.RelPath, content);
        }

        if (format == OutputFormat.Xml)
            sb.AppendLine("</project>");

        await File.WriteAllTextAsync(targetFilePath, sb.ToString(), new UTF8Encoding(false), ct);
    }

    private static async Task WriteStructureFileAsync(
        string targetFilePath,
        string projectName,
        OutputFormat format,
        List<FileItem> allFiles,
        List<(string ModulePath, string FileName, int FileCount)> modules,
        CancellationToken ct)
    {
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var fullTree = BuildDirectoryTree(projectName, allFiles.Select(f => f.RelPath));

        if (format == OutputFormat.Json)
        {
            var payload = new
            {
                project = projectName,
                exportedAt = now,
                totalFiles = allFiles.Count,
                totalModules = modules.Count,
                tree = fullTree,
                modules = modules.Select(m => new { module = m.ModulePath, file = m.FileName, files = m.FileCount })
            };
            await File.WriteAllTextAsync(targetFilePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8, ct);
            return;
        }

        var sb = new StringBuilder();
        switch (format)
        {
            case OutputFormat.Markdown:
                sb.AppendLine($"# Project Structure: {projectName}");
                sb.AppendLine($"> Exported: {now} | Total files: {allFiles.Count} | Modules: {modules.Count}\n");
                sb.AppendLine("## Full Directory Tree");
                sb.AppendLine("```text");
                sb.Append(fullTree);
                sb.AppendLine("```\n");
                sb.AppendLine("## Exported Module Files");
                foreach (var m in modules)
                    sb.AppendLine($"- `{m.ModulePath}` → `{m.FileName}` ({m.FileCount} files)");
                break;

            case OutputFormat.Xml:
                sb.AppendLine($"<project_structure name=\"{SecurityElement.Escape(projectName)}\" exported=\"{now}\" total_files=\"{allFiles.Count}\" total_modules=\"{modules.Count}\">");
                sb.AppendLine("<directory_tree>");
                sb.Append(SecurityElement.Escape(fullTree));
                sb.AppendLine("</directory_tree>");
                sb.AppendLine("<modules>");
                foreach (var m in modules)
                    sb.AppendLine($"  <module path=\"{SecurityElement.Escape(m.ModulePath)}\" file=\"{SecurityElement.Escape(m.FileName)}\" files=\"{m.FileCount}\" />");
                sb.AppendLine("</modules>");
                sb.AppendLine("</project_structure>");
                break;

            case OutputFormat.PlainText:
                sb.AppendLine($"=== PROJECT STRUCTURE: {projectName} | EXPORTED: {now} | FILES: {allFiles.Count} | MODULES: {modules.Count} ===");
                sb.AppendLine(fullTree);
                sb.AppendLine("=== EXPORTED MODULE FILES ===");
                foreach (var m in modules)
                    sb.AppendLine($"- {m.ModulePath} -> {m.FileName} ({m.FileCount} files)");
                break;
        }

        await File.WriteAllTextAsync(targetFilePath, sb.ToString(), new UTF8Encoding(false), ct);
    }

    private static void CollectFiles(
        string currentDir,
        string outputFullPath,
        string outputDir,
        string baseName,
        string outputExt,
        bool exportByModule,
        HashSet<string> extSet,
        HashSet<string> nameSet,
        bool skipBinary,
        List<string> results,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var isSameAsOutDir = string.Equals(Path.GetFullPath(currentDir), outputDir, StringComparison.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(currentDir))
            {
                var fullPath = Path.GetFullPath(file);
                if (string.Equals(fullPath, outputFullPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = Path.GetFileName(file);

                if (exportByModule && isSameAsOutDir &&
                    fileName.StartsWith(baseName + "_", StringComparison.OrdinalIgnoreCase) &&
                    fileName.EndsWith(outputExt, StringComparison.OrdinalIgnoreCase))
                    continue;

                var ext = Path.GetExtension(file);
                if (nameSet.Contains(fileName) || (!string.IsNullOrEmpty(ext) && extSet.Contains(ext)))
                    continue;

                if (skipBinary && IsBinaryFile(file))
                    continue;

                results.Add(file);
            }

            foreach (var dir in Directory.EnumerateDirectories(currentDir))
            {
                if (nameSet.Contains(Path.GetFileName(dir)))
                    continue;

                CollectFiles(dir, outputFullPath, outputDir, baseName, outputExt, exportByModule, extSet, nameSet, skipBinary, results, ct);
            }
        }
        catch (UnauthorizedAccessException) { }
    }

    private static int GetSortPriority(string relPath)
    {
        var isRoot = !relPath.Contains('/');
        if (!isRoot) return 3;

        var lower = relPath.ToLowerInvariant();
        if (lower.EndsWith(".sln") || lower.EndsWith(".csproj") || Enumerable.Contains(PriorityRootFiles, lower))
            return 1;

        return 2;
    }

    private static bool IsBinaryFile(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[4096];
            var read = fs.Read(buffer, 0, buffer.Length);
            for (var i = 0; i < read; i++)
                if (buffer[i] == 0)
                    return true;
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static string NormalizeExt(string ext)
    {
        ext = ext.Trim();
        return ext.StartsWith('.') ? ext : "." + ext;
    }

    private static string BuildDirectoryTree(string rootName, IEnumerable<string> relPaths)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{rootName}/");
        foreach (var p in relPaths)
        {
            var depth = p.Count(c => c == '/');
            var indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}├── {p}");
        }

        return sb.ToString();
    }

    private static void WriteHeader(StringBuilder sb, OutputFormat format, string projectName, int count, string tree)
    {
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        switch (format)
        {
            case OutputFormat.Markdown:
                sb.AppendLine($"# Project: {projectName}");
                sb.AppendLine($"> Exported: {now} | Total files: {count}\n");
                sb.AppendLine("## Directory Structure");
                sb.AppendLine("```text");
                sb.Append(tree);
                sb.AppendLine("```\n");
                break;

            case OutputFormat.Xml:
                sb.AppendLine(
                    $"<project name=\"{SecurityElement.Escape(projectName)}\" exported=\"{now}\" files=\"{count}\">");
                sb.AppendLine("<directory_tree>");
                sb.Append(SecurityElement.Escape(tree));
                sb.AppendLine("</directory_tree>");
                break;

            case OutputFormat.PlainText:
                sb.AppendLine($"=== PROJECT: {projectName} | EXPORTED: {now} | FILES: {count} ===");
                sb.AppendLine(tree);
                break;
        }
    }

    private static void AppendFileBlock(StringBuilder sb, OutputFormat format, string relPath, string content)
    {
        switch (format)
        {
            case OutputFormat.Markdown:
                var lang = GetMarkdownLang(Path.GetExtension(relPath));
                sb.AppendLine($"## `{relPath}`");
                sb.AppendLine($"````{lang}");
                sb.AppendLine(content.TrimEnd());
                sb.AppendLine("````\n");
                break;

            case OutputFormat.Xml:
                sb.AppendLine($"<file path=\"{SecurityElement.Escape(relPath)}\"><![CDATA[");
                sb.AppendLine(content.Replace("]]>", "]]]]><![CDATA[>").TrimEnd());
                sb.AppendLine("]]></file>");
                break;

            case OutputFormat.PlainText:
                sb.AppendLine($"--- FILE: {relPath} ---");
                sb.AppendLine(content.TrimEnd());
                sb.AppendLine();
                break;
        }
    }

    private static string GetMarkdownLang(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".axaml" or ".xaml" or ".xml" or ".csproj" => "xml",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".py" => "python",
            ".json" => "json",
            ".html" => "html",
            ".css" => "css",
            ".sql" => "sql",
            ".sh" => "bash",
            ".ps1" => "powershell",
            ".md" => "markdown",
            _ => ""
        };
    }
}