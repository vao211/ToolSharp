using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderScannerUI.Models;
using FolderScannerUI.Services;

namespace FolderScannerUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
   private readonly AppSettings _settings;
   private List<FileSystemItemInfo> _allItems = [];
   private CancellationTokenSource? _scanCts;
   private string _statusKey = "StatusReady";
   private object[] _statusArgs = [];

   [ObservableProperty] private bool _deepScanIncludeFolders;
   [ObservableProperty] private string _extensionFilter = ".mp4, .mkv";
   [ObservableProperty] private int _fileCount;
   [ObservableProperty] private int _folderCount;
   [ObservableProperty] private bool _includeFilesInFolderScan = true;

   [ObservableProperty]
   [NotifyPropertyChangedFor(nameof(ThemeLabel))]
   private bool _isDarkMode = true;

   [ObservableProperty]
   [NotifyPropertyChangedFor(nameof(ScanActionLabel))]
   [NotifyPropertyChangedFor(nameof(ShowInitialEmptyState))]
   [NotifyPropertyChangedFor(nameof(ShowFilterEmptyState))]
   private bool _isScanning;

   [ObservableProperty] private string? _lastExportedPath;
   [ObservableProperty] private bool _hasLastExportedPath;
   [ObservableProperty] private string _largestItemName = "-";
   [ObservableProperty] private string _lastDurationText = "-";
   [ObservableProperty] private string _scanningCurrentFolder = string.Empty;
   [ObservableProperty] private int _scanningFoundCount;
   [ObservableProperty] private string _searchQuery = string.Empty;
   [ObservableProperty] private FileSystemItemInfo? _selectedItem;
   [ObservableProperty] private LanguageOption? _selectedLanguage;

   [ObservableProperty]
   [NotifyPropertyChangedFor(nameof(IsFolderSizeMode))]
   [NotifyPropertyChangedFor(nameof(IsDeepScanMode))]
   [NotifyPropertyChangedFor(nameof(IsExtensionFilterMode))]
   private ScanMode _selectedScanMode = ScanMode.FolderSize;

   [ObservableProperty] private int _skippedCount;

   [ObservableProperty]
   [NotifyPropertyChangedFor(nameof(SortSizeIndicator))]
   [NotifyPropertyChangedFor(nameof(SortNameIndicator))]
   [NotifyPropertyChangedFor(nameof(SortDateIndicator))]
   [NotifyPropertyChangedFor(nameof(SortTypeIndicator))]
   private bool _sortDescending = true;

   [ObservableProperty]
   [NotifyPropertyChangedFor(nameof(SortSizeIndicator))]
   [NotifyPropertyChangedFor(nameof(SortNameIndicator))]
   [NotifyPropertyChangedFor(nameof(SortDateIndicator))]
   [NotifyPropertyChangedFor(nameof(SortTypeIndicator))]
   private SortField _sortField = SortField.Size;

   [ObservableProperty] private string _statusMessage = string.Empty;
   [ObservableProperty] private string _targetFolder = string.Empty;
   [ObservableProperty] private long _totalBytes;
   [ObservableProperty] private int _totalItemsCount;

   [ObservableProperty]
   [NotifyPropertyChangedFor(nameof(IsFilterAll))]
   [NotifyPropertyChangedFor(nameof(IsFilterFolders))]
   [NotifyPropertyChangedFor(nameof(IsFilterFiles))]
   private ItemTypeFilter _typeFilter = ItemTypeFilter.All;

   public MainWindowViewModel()
   {
      _settings = AppSettings.Load();

      _isDarkMode = _settings.IsDarkMode;
      _targetFolder = Directory.Exists(_settings.LastFolderPath)
         ? _settings.LastFolderPath
         : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      _selectedScanMode = _settings.LastScanMode;
      _includeFilesInFolderScan = _settings.IncludeFilesInFolderScan;
      _deepScanIncludeFolders = _settings.DeepScanIncludeFolders;
      _extensionFilter = _settings.LastExtensionFilter;

      IsAdministrator = CheckIsAdministrator();
      IsWindowsPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

      RefreshLanguageList(_settings.LanguageFile);
      ApplyTheme(_isDarkMode);
      SetStatus("StatusReady");
   }

   public LocalizationService Loc { get; } = new();
   public ObservableCollection<LanguageOption> Languages { get; } = [];
   public ObservableCollection<FileSystemItemInfo> FilteredItems { get; } = [];

   public bool IsWindowsPlatform { get; }
   public bool IsAdministrator { get; }
   public bool CanElevateAdmin => IsWindowsPlatform && !IsAdministrator;

   public bool IsFolderSizeMode
   {
      get => SelectedScanMode == ScanMode.FolderSize;
      set
      {
         if (value) SelectedScanMode = ScanMode.FolderSize;
      }
   }

   public bool IsDeepScanMode
   {
      get => SelectedScanMode == ScanMode.DeepScan;
      set
      {
         if (value) SelectedScanMode = ScanMode.DeepScan;
      }
   }

   public bool IsExtensionFilterMode
   {
      get => SelectedScanMode == ScanMode.ExtensionFilter;
      set
      {
         if (value) SelectedScanMode = ScanMode.ExtensionFilter;
      }
   }

   public bool IsFilterAll
   {
      get => TypeFilter == ItemTypeFilter.All;
      set
      {
         if (value) TypeFilter = ItemTypeFilter.All;
      }
   }

   public bool IsFilterFolders
   {
      get => TypeFilter == ItemTypeFilter.Folders;
      set
      {
         if (value) TypeFilter = ItemTypeFilter.Folders;
      }
   }

   public bool IsFilterFiles
   {
      get => TypeFilter == ItemTypeFilter.Files;
      set
      {
         if (value) TypeFilter = ItemTypeFilter.Files;
      }
   }

   public bool HasResults => _allItems.Count > 0;
   public bool HasFilteredResults => FilteredItems.Count > 0;
   public bool ShowInitialEmptyState => !IsScanning && !HasResults;
   public bool ShowFilterEmptyState => !IsScanning && HasResults && !HasFilteredResults;

   public bool CanGoUpFolder => !string.IsNullOrWhiteSpace(TargetFolder) &&
                                Directory.GetParent(TargetFolder.TrimEnd(Path.DirectorySeparatorChar,
                                   Path.AltDirectorySeparatorChar)) is not null;

   public string TotalSizeDisplay => SizeFormatter.Format(TotalBytes);
   public string ThemeLabel => IsDarkMode ? Loc["ThemeDark"] : Loc["ThemeLight"];
   public string ScanActionLabel => IsScanning ? Loc["BtnStopScan"] : Loc["BtnStartScan"];
   public string ScanningStatusText => string.Format(Loc["ScanningStatus"], ScanningCurrentFolder);
   public string ScanningItemsText => string.Format(Loc["ScanningItemsFound"], ScanningFoundCount);

   public string KpiShowingSubtext => string.Format(Loc["KpiSubShowing"], FilteredItems.Count, TotalItemsCount);
   public string KpiDurationSubtext => string.Format(Loc["KpiSubDuration"], LastDurationText);
   public string KpiLargestSubtext => string.Format(Loc["KpiSubLargest"], LargestItemName);

   public string SortSizeIndicator => SortField == SortField.Size ? (SortDescending ? "↓" : "↑") : string.Empty;
   public string SortNameIndicator => SortField == SortField.Name ? (SortDescending ? "↓" : "↑") : string.Empty;
   public string SortDateIndicator => SortField == SortField.Date ? (SortDescending ? "↓" : "↑") : string.Empty;
   public string SortTypeIndicator => SortField == SortField.Type ? (SortDescending ? "↓" : "↑") : string.Empty;

   partial void OnIsDarkModeChanged(bool value)
   {
      ApplyTheme(value);
      _settings.IsDarkMode = value;
      _settings.Save();
   }

   partial void OnTargetFolderChanged(string value)
   {
      OnPropertyChanged(nameof(CanGoUpFolder));
      _settings.LastFolderPath = value;
      _settings.Save();
   }

   partial void OnSelectedScanModeChanged(ScanMode value)
   {
      _settings.LastScanMode = value;
      _settings.Save();
   }

   partial void OnIncludeFilesInFolderScanChanged(bool value)
   {
      _settings.IncludeFilesInFolderScan = value;
      _settings.Save();
   }

   partial void OnDeepScanIncludeFoldersChanged(bool value)
   {
      _settings.DeepScanIncludeFolders = value;
      _settings.Save();
   }

   partial void OnExtensionFilterChanged(string value)
   {
      _settings.LastExtensionFilter = value;
      _settings.Save();
   }

   partial void OnSearchQueryChanged(string value) => ApplyFilterAndSort();
   partial void OnTypeFilterChanged(ItemTypeFilter value) => ApplyFilterAndSort();
   partial void OnSortFieldChanged(SortField value) => ApplyFilterAndSort();
   partial void OnSortDescendingChanged(bool value) => ApplyFilterAndSort();

   partial void OnScanningCurrentFolderChanged(string value) => OnPropertyChanged(nameof(ScanningStatusText));
   partial void OnScanningFoundCountChanged(int value) => OnPropertyChanged(nameof(ScanningItemsText));

   partial void OnSelectedLanguageChanged(LanguageOption? value)
   {
      if (value is null) return;
      Loc.LoadLanguage(value.FileName);
      _settings.LanguageFile = value.FileName;
      _settings.Save();
      RefreshLocalizedStrings();
   }

   private void RefreshLocalizedStrings()
   {
      OnPropertyChanged(nameof(ThemeLabel));
      OnPropertyChanged(nameof(ScanActionLabel));
      OnPropertyChanged(nameof(ScanningStatusText));
      OnPropertyChanged(nameof(ScanningItemsText));
      OnPropertyChanged(nameof(KpiShowingSubtext));
      OnPropertyChanged(nameof(KpiDurationSubtext));
      OnPropertyChanged(nameof(KpiLargestSubtext));
      SetStatus(_statusKey, _statusArgs);
   }

   private void SetStatus(string key, params object[] args)
   {
      _statusKey = key;
      _statusArgs = args;
      var template = Loc[key];
      StatusMessage = args.Length > 0 ? string.Format(template, args) : template;
   }

   private static void ApplyTheme(bool dark)
   {
      if (Application.Current is not null)
         Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
   }

   private void RefreshLanguageList(string preferredFile)
   {
      Loc.ScanLanguages();
      Languages.Clear();
      foreach (var lang in Loc.AvailableLanguages)
         Languages.Add(lang);

      SelectedLanguage = Languages.FirstOrDefault(l => l.FileName.Equals(preferredFile, StringComparison.OrdinalIgnoreCase))
                         ?? Languages.FirstOrDefault();
   }

   [RelayCommand]
   private void ToggleTheme() => IsDarkMode = !IsDarkMode;

   [RelayCommand]
   private void ReloadLanguages()
   {
      var currentFile = SelectedLanguage?.FileName ?? _settings.LanguageFile;
      RefreshLanguageList(currentFile);
      SetStatus("StatusLangReloaded", Languages.Count);
   }

   [RelayCommand]
   private void SelectQuickFolder(string? kind)
   {
      var path = kind switch
      {
         "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
         "Downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
         "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
         "Home" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
         _ => TargetFolder
      };

      if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
         TargetFolder = path;
   }

   [RelayCommand]
   private void ApplyExtensionPreset(string? ext)
   {
      if (string.IsNullOrWhiteSpace(ext)) return;
      ExtensionFilter = ext;
      SelectedScanMode = ScanMode.ExtensionFilter;
   }

   [RelayCommand]
   private async Task GoUpFolderAsync()
   {
      if (IsScanning || string.IsNullOrWhiteSpace(TargetFolder)) return;
      var trimmed = TargetFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      var parent = Directory.GetParent(trimmed);
      if (parent is null) return;

      TargetFolder = parent.FullName;
      if (HasResults)
         await RunScanAsync();
   }

   [RelayCommand]
   private async Task DrillIntoFolderAsync(FileSystemItemInfo? item)
   {
      if (IsScanning || item is null || !item.IsDirectory || !Directory.Exists(item.FullPath))
         return;

      TargetFolder = item.FullPath;
      SelectedScanMode = ScanMode.FolderSize;
      await RunScanAsync();
   }

   [RelayCommand]
   private async Task StartOrStopScanAsync()
   {
      if (IsScanning)
      {
         _scanCts?.Cancel();
         return;
      }

      await RunScanAsync();
   }

   public async Task RunScanAsync()
   {
      if (IsScanning) return;

      var path = TargetFolder?.Trim() ?? string.Empty;
      if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
      {
         SetStatus("StatusFolderNotFound", path);
         return;
      }

      if (SelectedScanMode == ScanMode.ExtensionFilter && string.IsNullOrWhiteSpace(ExtensionFilter))
      {
         SetStatus("StatusExtensionRequired");
         return;
      }

      _scanCts?.Dispose();
      _scanCts = new CancellationTokenSource();

      IsScanning = true;
      ScanningCurrentFolder = Path.GetFileName(path) is { Length: > 0 } name ? name : path;
      ScanningFoundCount = 0;
      SetStatus("StatusScanning", path);

      var progress = new Progress<ScanProgressReport>(report =>
      {
         ScanningCurrentFolder = report.CurrentFolder;
         ScanningFoundCount = report.ItemsFound;
      });

      try
      {
         var result = await FolderScannerService.ScanAsync(
            path,
            SelectedScanMode,
            IncludeFilesInFolderScan,
            DeepScanIncludeFolders,
            ExtensionFilter,
            progress,
            _scanCts.Token);

         _allItems = result.Items;
         TotalBytes = result.TotalBytes;
         TotalItemsCount = result.Items.Count;
         FolderCount = result.FolderCount;
         FileCount = result.FileCount;
         SkippedCount = result.SkippedCount;
         LastDurationText = $"{result.Duration.TotalSeconds:F2}s";

         var largest = _allItems.OrderByDescending(x => x.SizeInBytes).FirstOrDefault();
         LargestItemName = largest is null ? "-" : $"{largest.Name} ({largest.FormattedSize})";

         ApplyFilterAndSort();
         NotifySummaryProperties();

         if (SkippedCount > 0)
            SetStatus("StatusDoneWithSkipped", result.Duration.TotalSeconds, TotalItemsCount, TotalSizeDisplay,
               SkippedCount);
         else
            SetStatus("StatusDone", result.Duration.TotalSeconds, TotalItemsCount, TotalSizeDisplay);
      }
      catch (OperationCanceledException)
      {
         SetStatus("StatusCancelled", _allItems.Count);
      }
      catch (Exception ex)
      {
         StatusMessage = $"Error: {ex.Message}";
      }
      finally
      {
         IsScanning = false;
         _scanCts?.Dispose();
         _scanCts = null;
      }
   }

   [RelayCommand]
   private void SortBy(string? fieldName)
   {
      if (!Enum.TryParse<SortField>(fieldName, true, out var field))
         return;

      if (SortField == field)
      {
         SortDescending = !SortDescending;
      }
      else
      {
         SortField = field;
         SortDescending = field is SortField.Size or SortField.Date;
      }
   }

   private void ApplyFilterAndSort()
   {
      IEnumerable<FileSystemItemInfo> query = _allItems;

      query = TypeFilter switch
      {
         ItemTypeFilter.Folders => query.Where(x => x.IsDirectory),
         ItemTypeFilter.Files => query.Where(x => !x.IsDirectory),
         _ => query
      };

      if (!string.IsNullOrWhiteSpace(SearchQuery))
      {
         var term = SearchQuery.Trim();
         query = query.Where(x =>
            x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            x.FullPath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            x.Extension.Contains(term, StringComparison.OrdinalIgnoreCase));
      }

      query = (SortField, SortDescending) switch
      {
         (SortField.Size, true) => query.OrderByDescending(x => x.SizeInBytes).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
         (SortField.Size, false) => query.OrderBy(x => x.SizeInBytes).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
         (SortField.Name, true) => query.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase),
         (SortField.Name, false) => query.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
         (SortField.Date, true) => query.OrderByDescending(x => x.CreatedDate),
         (SortField.Date, false) => query.OrderBy(x => x.CreatedDate),
         (SortField.Type, true) => query.OrderByDescending(x => x.IsDirectory).ThenByDescending(x => x.SizeInBytes),
         (SortField.Type, false) => query.OrderBy(x => x.IsDirectory).ThenByDescending(x => x.SizeInBytes),
         _ => query.OrderByDescending(x => x.SizeInBytes)
      };

      FilteredItems.Clear();
      foreach (var item in query)
         FilteredItems.Add(item);

      NotifySummaryProperties();
   }

   private void NotifySummaryProperties()
   {
      OnPropertyChanged(nameof(TotalSizeDisplay));
      OnPropertyChanged(nameof(HasResults));
      OnPropertyChanged(nameof(HasFilteredResults));
      OnPropertyChanged(nameof(ShowInitialEmptyState));
      OnPropertyChanged(nameof(ShowFilterEmptyState));
      OnPropertyChanged(nameof(KpiShowingSubtext));
      OnPropertyChanged(nameof(KpiDurationSubtext));
      OnPropertyChanged(nameof(KpiLargestSubtext));
   }

   public string GetDefaultExportFileName(ExportFormat format)
   {
      var ext = format switch
      {
         ExportFormat.Csv => ".csv",
         ExportFormat.Txt => ".txt",
         ExportFormat.Json => ".json",
         _ => ".csv"
      };
      return $"FolderScan_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
   }

   public void ExportResultsToFile(string filePath, ExportFormat format)
   {
      if (_allItems.Count == 0)
      {
         SetStatus("StatusNoDataToExport");
         return;
      }

      var itemsToExport = FilteredItems.Count > 0 ? (IReadOnlyList<FileSystemItemInfo>)FilteredItems : _allItems;
      var modeLabel = SelectedScanMode switch
      {
         ScanMode.FolderSize => Loc["ModeFolderSize"],
         ScanMode.DeepScan => Loc["ModeDeepScan"],
         ScanMode.ExtensionFilter => $"{Loc["ModeExtFilter"]} ({ExtensionFilter})",
         _ => SelectedScanMode.ToString()
      };

      ExportService.Export(filePath, format, itemsToExport, TargetFolder, modeLabel, Loc);
      LastExportedPath = filePath;
      HasLastExportedPath = true;
      SetStatus("StatusExported", itemsToExport.Count, filePath);
   }

   [RelayCommand]
   private void OpenInExplorer(string? path)
   {
      if (string.IsNullOrWhiteSpace(path)) return;

      try
      {
         if (OperatingSystem.IsWindows())
         {
            if (File.Exists(path))
            {
               Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
               {
                  UseShellExecute = true
               });
            }
            else if (Directory.Exists(path))
            {
               Process.Start(new ProcessStartInfo
               {
                  FileName = path,
                  UseShellExecute = true
               });
            }
         }
         else if (OperatingSystem.IsLinux())
         {
            var dir = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            if (!string.IsNullOrEmpty(dir))
            {
               var psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
               psi.ArgumentList.Add(dir);
               Process.Start(psi);
            }
         }
         else if (OperatingSystem.IsMacOS())
         {
            var psi = new ProcessStartInfo("open") { UseShellExecute = false };
            if (File.Exists(path))
            {
               psi.ArgumentList.Add("-R");
               psi.ArgumentList.Add(path);
            }
            else
            {
               psi.ArgumentList.Add(path);
            }

            Process.Start(psi);
         }
      }
      catch (Exception ex)
      {
         StatusMessage = $"Error: {ex.Message}";
      }
   }

   [RelayCommand]
   private void OpenLastExportedLocation()
   {
      if (!string.IsNullOrWhiteSpace(LastExportedPath))
         OpenInExplorer(LastExportedPath);
   }

   [RelayCommand]
   private void ElevateAdmin()
   {
      if (!CanElevateAdmin) return;

      try
      {
         var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
         if (string.IsNullOrEmpty(exePath)) return;

         var psi = new ProcessStartInfo
         {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas"
         };
         Process.Start(psi);
         Environment.Exit(0);
      }
      catch (Win32Exception ex)
      {
         SetStatus("StatusAdminFailed", ex.Message);
      }
      catch (Exception ex)
      {
         SetStatus("StatusAdminFailed", ex.Message);
      }
   }

   public void NotifyCopiedPath(string path) => SetStatus("StatusCopiedPath", path);

   private static bool CheckIsAdministrator()
   {
      if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

      try
      {
         using var identity = WindowsIdentity.GetCurrent();
         var principal = new WindowsPrincipal(identity);
         return principal.IsInRole(WindowsBuiltInRole.Administrator);
      }
      catch
      {
         return false;
      }
   }
}
