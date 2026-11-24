namespace FolderScannerUI.Models;

public enum ScanMode
{
   FolderSize,
   DeepScan,
   ExtensionFilter
}

public enum ItemTypeFilter
{
   All,
   Folders,
   Files
}

public enum SortField
{
   Size,
   Name,
   Date,
   Type
}

public enum ExportFormat
{
   Csv,
   Txt,
   Json
}
