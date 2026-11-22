using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using CodePacker.Models;
using CodePacker.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodePacker.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    public LocalizationService Loc { get; } = new();

    [ObservableProperty] private string _sourceFolder = Environment.CurrentDirectory;
    [ObservableProperty] private string _outputFolder = Environment.CurrentDirectory;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(FinalOutputFileName))]
    [NotifyPropertyChangedFor(nameof(SuffixExtension))]
    private string _outputFileName = "project_code";
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FinalOutputFileName))]
    [NotifyPropertyChangedFor(nameof(SuffixExtension))] 
    private OutputFormat _selectedFormat;
    
    [ObservableProperty] private bool _skipBinaryFiles;

    [ObservableProperty] private string _newExtension = "";
    [ObservableProperty] private string? _selectedExtension;
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string? _selectedName;

    [ObservableProperty] private bool _isDarkMode;
    [ObservableProperty] private string _defaultFileName = "project_code";
    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;
    
    [ObservableProperty] private bool _syncOutputFolderWithSource;

    public ObservableCollection<string> ExcludedExtensions { get; }
    public ObservableCollection<string> ExcludedNames { get; }
    public ObservableCollection<LanguageOption> Languages { get; } = [];
    public OutputFormat[] AvailableFormats { get; } = Enum.GetValues<OutputFormat>();
    
    public string FinalOutputFileName =>
        OutputFormatExtensions.EnsureExtension(OutputFileName, SelectedFormat);
    
    public string SuffixExtension
    {
        get
        {
            var ext = SelectedFormat.GetExtension();
            var clean = (OutputFileName ?? string.Empty).Trim();
            return clean.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : ext;
        }
    }

    public MainWindowViewModel()
    {
        _settings = AppSettings.Load();

        ExcludedExtensions = new ObservableCollection<string>(_settings.ExcludedExtensions);
        ExcludedNames = new ObservableCollection<string>(_settings.ExcludedNames);

        DefaultFileName = _settings.DefaultOutputFileName;
        OutputFileName = _settings.DefaultOutputFileName;
        SelectedFormat = _settings.DefaultFormat;
        SkipBinaryFiles = _settings.SkipBinaryFiles;
        IsDarkMode = _settings.IsDarkMode;
        
        _syncOutputFolderWithSource = _settings.SyncOutputFolderWithSource;

        RefreshLanguageList(_settings.LanguageFile);
        ApplyTheme(IsDarkMode);
        StatusMessage = Loc["StatusReady"];
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        ApplyTheme(value);
        SaveSettings();
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
        foreach (var l in Loc.AvailableLanguages)
            Languages.Add(l);

        SelectedLanguage = Languages.FirstOrDefault(l => l.FileName == preferredFile)
                           ?? Languages.FirstOrDefault();
    }

    [RelayCommand]
    private void ReloadLanguages() => RefreshLanguageList(_settings.LanguageFile);

    [RelayCommand]
    private void AddExtension()
    {
        var ext = NewExtension.Trim();
        if (string.IsNullOrEmpty(ext)) return;
        if (!ext.StartsWith('.')) ext = "." + ext;

        if (!ExcludedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            ExcludedExtensions.Add(ext);
            SaveSettings();
        }
        NewExtension = "";
    }

    [RelayCommand]
    private void RemoveExtension()
    {
        if (SelectedExtension is null) return;
        ExcludedExtensions.Remove(SelectedExtension);
        SaveSettings();
    }

    [RelayCommand]
    private void AddExcludedName()
    {
        var name = NewName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        if (!ExcludedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            ExcludedNames.Add(name);
            SaveSettings();
        }
        NewName = "";
    }

    [RelayCommand]
    private void RemoveExcludedName()
    {
        if (SelectedName is null) return;
        ExcludedNames.Remove(SelectedName);
        SaveSettings();
    }

    [RelayCommand]
    private void RegisterPath()
    {
        var res = PathManagerService.RegisterOrUpdatePath(_settings);
        StatusMessage = res switch
        {
            PathUpdateResult.Added => Loc["PathAdded"],
            PathUpdateResult.Updated => Loc["PathUpdated"],
            _ => Loc["PathExists"]
        };
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.DefaultOutputFileName = string.IsNullOrWhiteSpace(DefaultFileName) ? "project_code" : DefaultFileName.Trim();
        _settings.DefaultFormat = SelectedFormat;
        _settings.SkipBinaryFiles = SkipBinaryFiles;
        _settings.IsDarkMode = IsDarkMode;
        _settings.ExcludedExtensions = [.. ExcludedExtensions];
        _settings.ExcludedNames = [.. ExcludedNames];
        _settings.SyncOutputFolderWithSource = SyncOutputFolderWithSource;
        _settings.Save();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsBusy || !Directory.Exists(SourceFolder)) return;

        try
        {
            IsBusy = true;
            var outPath = Path.Combine(OutputFolder, FinalOutputFileName);
            var result = await Task.Run(() => CodeExporterService.ExportAsync(
                SourceFolder,
                outPath,
                SelectedFormat,
                ExcludedExtensions,
                ExcludedNames,
                SkipBinaryFiles));

            StatusMessage = string.Format(Loc["StatusDone"], result.FileCount, result.OutputFullPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    partial void OnSourceFolderChanged(string value)
    {
        if (SyncOutputFolderWithSource && !string.IsNullOrWhiteSpace(value))
            OutputFolder = value;
    }
    
    partial void OnSyncOutputFolderWithSourceChanged(bool value)
    {
        if (value && !string.IsNullOrWhiteSpace(SourceFolder))
            OutputFolder = SourceFolder;
        _settings.DefaultOutputFileName = string.IsNullOrWhiteSpace(DefaultFileName)
            ? "project_code"
            : DefaultFileName.Trim();
        SaveSettings();
    }
    
    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder) || !Directory.Exists(OutputFolder))
        {
            StatusMessage = string.Format(Loc["FolderNotFound"], OutputFolder);
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = OutputFolder,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                var psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                psi.ArgumentList.Add(OutputFolder);
                Process.Start(psi);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null) return;
        Loc.LoadLanguage(value.FileName);
        _settings.LanguageFile = value.FileName;
        _settings.Save();
        StatusMessage = Loc["StatusReady"];
    }
    
}