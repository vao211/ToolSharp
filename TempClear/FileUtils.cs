using System;
using System.IO;
using System.Linq;

public static class FileUtils
{
    public static long GetDirectorySize(string folderPath)
    {
        try
        {
            DirectoryInfo di = new DirectoryInfo(folderPath);
            return di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblSByte = bytes;
        for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblSByte = bytes / 1024.0;
        }
        return string.Format("{0:0.##} {1}", dblSByte, suffix[i]);
    }
}