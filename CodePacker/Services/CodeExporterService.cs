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

public record ExportResult(int FileCount, string OutputFullPath);

public static class CodeExporterService
{
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
        CancellationToken ct = default)
    {
        var extSet = new HashSet<string>(
            excludedExtensions.Select(NormalizeExt),
            StringComparer.OrdinalIgnoreCase);

        var nameSet = new HashSet<string>(excludedNames, StringComparer.OrdinalIgnoreCase);
        var outDir = Path.GetDirectoryName(outputFullPath) ?? sourceDir;
        var outFile = OutputFormatExtensions.EnsureExtension(Path.GetFileName(outputFullPath), format);
        var normalizedOutput = Path.GetFullPath(Path.Combine(outDir, outFile));
        var files = new List<string>();
        CollectFiles(sourceDir, normalizedOutput, extSet, nameSet, skipBinary, files, ct);

        var sortedFiles = files
            .Select(full => new
            {
                FullPath = full,
                RelPath = Path.GetRelativePath(sourceDir, full).Replace('\\', '/')
            })
            .OrderBy(f => GetSortPriority(f.RelPath))
            .ThenBy(f => Path.GetDirectoryName(f.RelPath)?.Replace('\\', '/') ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => Path.GetFileName(f.RelPath), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tree = BuildDirectoryTree(Path.GetFileName(sourceDir.TrimEnd('\\', '/')), sortedFiles.Select(f => f.RelPath));

        Directory.CreateDirectory(Path.GetDirectoryName(normalizedOutput)!);
        var sb = new StringBuilder();

        if (format == OutputFormat.Json)
        {
            var items = new List<object>();
            foreach (var f in sortedFiles)
            {
                ct.ThrowIfCancellationRequested();
                var content = await File.ReadAllTextAsync(f.FullPath, ct);
                items.Add(new { path = f.RelPath, lines = content.Split('\n').Length, content });
            }

            var payload = new
            {
                project = Path.GetFileName(sourceDir),
                exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                totalFiles = sortedFiles.Count,
                tree,
                files = items
            };
            await File.WriteAllTextAsync(normalizedOutput, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8, ct);
            return new ExportResult(sortedFiles.Count, normalizedOutput);
        }

        WriteHeader(sb, format, Path.GetFileName(sourceDir), sortedFiles.Count, tree);

        foreach (var f in sortedFiles)
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(f.FullPath, ct);
            AppendFileBlock(sb, format, f.RelPath, content);
        }

        if (format == OutputFormat.Xml)
            sb.AppendLine("</project>");

        await File.WriteAllTextAsync(normalizedOutput, sb.ToString(), new UTF8Encoding(false), ct);
        return new ExportResult(sortedFiles.Count, normalizedOutput);
    }

    private static void CollectFiles(
        string currentDir,
        string outputFullPath,
        HashSet<string> extSet,
        HashSet<string> nameSet,
        bool skipBinary,
        List<string> results,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            foreach (var file in Directory.EnumerateFiles(currentDir))
            {
                if (string.Equals(Path.GetFullPath(file), outputFullPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = Path.GetFileName(file);
                var ext = Path.GetExtension(file);

                if (nameSet.Contains(fileName) || (!string.IsNullOrEmpty(ext) && extSet.Contains(ext)))
                    continue;

                if (skipBinary && IsBinaryFile(file))
                    continue;

                results.Add(file);
            }

            foreach (var dir in Directory.EnumerateDirectories(currentDir))
            {
                var dirName = Path.GetFileName(dir);
                if (nameSet.Contains(dirName))
                    continue;

                CollectFiles(dir, outputFullPath, extSet, nameSet, skipBinary, results, ct);
            }
        }
        catch (UnauthorizedAccessException) { }
    }

    private static int GetSortPriority(string relPath)
    {
        bool isRoot = !relPath.Contains('/');
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
            byte[] buffer = new byte[4096];
            int read = fs.Read(buffer, 0, buffer.Length);
            for (int i = 0; i < read; i++)
                if (buffer[i] == 0) return true;
            return false;
        }
        catch { return true; }
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
                sb.AppendLine($"<project name=\"{SecurityElement.Escape(projectName)}\" exported=\"{now}\" files=\"{count}\">");
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

    private static string GetMarkdownLang(string ext) => ext.ToLowerInvariant() switch
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