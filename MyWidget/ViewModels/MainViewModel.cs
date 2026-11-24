using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using MyWidget.Models;
using MyWidget.Services;

namespace MyWidget.ViewModels;

public class MainViewModel : ViewModelBase
{
   private bool _isSettingsTab;
   private string _searchQuery = string.Empty;
   private int _filterMode; // 0: All, 1: Open, 2: Hidden
   private string _customAppAccentHex;
   private LanguageOption? _selectedLanguage;
   private string _statusMessage = string.Empty;

   public MainViewModel(WidgetManager manager)
   {
      Manager = manager;
      _customAppAccentHex = Settings.AccentColor;

      // Sync actual registry state on startup
      var regEnabled = StartupService.IsStartupEnabled();
      if (Settings.StartWithWindows != regEnabled)
      {
         if (Settings.StartWithWindows)
            StartupService.SetStartup(true);
         else
            Settings.StartWithWindows = regEnabled;
      }

      AppAccentSwatches = new ObservableCollection<ColorSwatchItem>(
         ThemeService.AppAccentPresets.Select(hex => new ColorSwatchItem(hex)));
      UpdateAppAccentSwatchSelection();

      DefaultNoteSwatches = new ObservableCollection<ColorSwatchItem>(
         ThemeService.NoteColorPresets.Select(hex => new ColorSwatchItem(hex, string.IsNullOrEmpty(hex))));
      UpdateDefaultNoteSwatchSelection();

      AvailableLanguages = new ObservableCollection<LanguageOption>(LocalizationService.Instance.AvailableLanguages);
      _selectedLanguage = AvailableLanguages.FirstOrDefault(l =>
                             string.Equals(l.FileName, Settings.LanguageFile, StringComparison.OrdinalIgnoreCase))
                          ?? AvailableLanguages.FirstOrDefault();

      SwitchToNotesCommand = new RelayCommand(() => IsSettingsTab = false);
      SwitchToSettingsCommand = new RelayCommand(() => IsSettingsTab = true);

      CreateNoteCommand = new RelayCommand(() => Manager.CreateNewNote());
      ShowAllNotesCommand = new RelayCommand(() => Manager.ShowAllNotes());
      HideAllNotesCommand = new RelayCommand(() => Manager.HideAllNotes());
      ClearSearchCommand = new RelayCommand(() => SearchQuery = string.Empty);

      SetFilterAllCommand = new RelayCommand(() => FilterMode = 0);
      SetFilterOpenCommand = new RelayCommand(() => FilterMode = 1);
      SetFilterHiddenCommand = new RelayCommand(() => FilterMode = 2);

      ToggleThemeCommand = new RelayCommand(() => IsDarkMode = !IsDarkMode);
      SetLightModeCommand = new RelayCommand(() => IsDarkMode = false);
      SetDarkModeCommand = new RelayCommand(() => IsDarkMode = true);
      SetCornerPresetCommand = new RelayCommand(p =>
      {
         if (p is string s && double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var r))
            CornerRadius = r;
      });

      SelectAppAccentCommand = new RelayCommand(p =>
      {
         if (p is ColorSwatchItem sw)
            ApplyAppAccent(sw.ColorHex);
      });
      ApplyCustomAppAccentCommand = new RelayCommand(() =>
      {
         if (ThemeService.TryParseHexColor(CustomAppAccentHex, out _))
            ApplyAppAccent(ThemeService.NormalizeHex(CustomAppAccentHex));
      });

      SelectDefaultNoteColorCommand = new RelayCommand(p =>
      {
         if (p is ColorSwatchItem sw)
         {
            Settings.DefaultNoteColor = sw.ColorHex;
            UpdateDefaultNoteSwatchSelection();
            Manager.SaveSettings();
         }
      });

      ReloadLanguagesCommand = new RelayCommand(ReloadLanguages);
      OpenLangFolderCommand = new RelayCommand(() => OpenFolderInExplorer(LocalizationService.Instance.LangFolder));
      OpenDataFolderCommand = new RelayCommand(() => OpenFolderInExplorer(DataStorageService.Instance.DataFolder));

      MigrateExternalImagesCommand = new RelayCommand(MigrateExternalImages);
      CleanUnusedMediaCommand = new RelayCommand(CleanUnusedMedia);
      QuitAppCommand = new RelayCommand(() => Manager.ExitApplication());

      Manager.NotesChanged += (_, _) => RefreshFilteredNotes();
      RefreshFilteredNotes();
   }

   public WidgetManager Manager { get; }
   public AppSettings Settings => Manager.Settings;

   public ObservableCollection<NoteViewModel> FilteredNotes { get; } = [];
   public ObservableCollection<ColorSwatchItem> AppAccentSwatches { get; }
   public ObservableCollection<ColorSwatchItem> DefaultNoteSwatches { get; }
   public ObservableCollection<LanguageOption> AvailableLanguages { get; }

   public bool IsSettingsTab
   {
      get => _isSettingsTab;
      set
      {
         if (SetProperty(ref _isSettingsTab, value))
            OnPropertyChanged(nameof(IsNotesTab));
      }
   }

   public bool IsNotesTab => !_isSettingsTab;

   public string SearchQuery
   {
      get => _searchQuery;
      set
      {
         if (SetProperty(ref _searchQuery, value))
         {
            OnPropertyChanged(nameof(HasSearchQuery));
            RefreshFilteredNotes();
         }
      }
   }

   public bool HasSearchQuery => !string.IsNullOrWhiteSpace(_searchQuery);

   public int FilterMode
   {
      get => _filterMode;
      set
      {
         if (SetProperty(ref _filterMode, value))
         {
            OnPropertyChanged(nameof(IsFilterAll));
            OnPropertyChanged(nameof(IsFilterOpen));
            OnPropertyChanged(nameof(IsFilterHidden));
            RefreshFilteredNotes();
         }
      }
   }

   public bool IsFilterAll => FilterMode == 0;
   public bool IsFilterOpen => FilterMode == 1;
   public bool IsFilterHidden => FilterMode == 2;

   public int TotalNotesCount => Manager.Notes.Count;
   public int OpenNotesCount => Manager.Notes.Count(n => n.IsOpen);
   public int HiddenNotesCount => Manager.Notes.Count(n => !n.IsOpen);
   public bool HasNoFilteredNotes => FilteredNotes.Count == 0;

   public bool IsDarkMode
   {
      get => Settings.IsDarkMode;
      set
      {
         if (Settings.IsDarkMode == value) return;
         Settings.IsDarkMode = value;
         ThemeService.ApplyTheme(value, Settings.AccentColor);
         OnPropertyChanged();
         OnPropertyChanged(nameof(IsLightMode));
         Manager.SaveSettings();
      }
   }

   public bool IsLightMode => !IsDarkMode;

   public double CornerRadius
   {
      get => Math.Clamp(Settings.CornerRadius, 0, 24);
      set
      {
         var clamped = Math.Round(Math.Clamp(value, 0, 24));
         if (Math.Abs(Settings.CornerRadius - clamped) < 0.1) return;
         Settings.CornerRadius = clamped;
         ThemeService.ApplyCornerRadius(clamped);
         OnPropertyChanged();
         OnPropertyChanged(nameof(CornerRadiusText));
         Manager.SaveSettings();
      }
   }

   public string CornerRadiusText => $"{(int)Math.Round(CornerRadius)} px";

   public bool ShowDescriptions
   {
      get => Settings.ShowDescriptions;
      set
      {
         if (Settings.ShowDescriptions == value) return;
         Settings.ShowDescriptions = value;
         ThemeService.ApplyHintsVisibility(value);
         OnPropertyChanged();
         Manager.SaveSettings();
      }
   }

   public string CustomAppAccentHex
   {
      get => _customAppAccentHex;
      set => SetProperty(ref _customAppAccentHex, value);
   }

   public bool StartWithWindows
   {
      get => Settings.StartWithWindows;
      set
      {
         if (Settings.StartWithWindows == value) return;
         if (StartupService.SetStartup(value))
         {
            Settings.StartWithWindows = value;
            OnPropertyChanged();
            Manager.SaveSettings();
         }
      }
   }

   public bool CopyImagesToAppData
   {
      get => Settings.CopyImagesToAppData;
      set
      {
         if (Settings.CopyImagesToAppData == value) return;
         Settings.CopyImagesToAppData = value;
         OnPropertyChanged();
         Manager.SaveSettings();
      }
   }

   public bool HideToTrayOnClose
   {
      get => Settings.HideToTrayOnClose;
      set
      {
         if (Settings.HideToTrayOnClose == value) return;
         Settings.HideToTrayOnClose = value;
         OnPropertyChanged();
         Manager.SaveSettings();
      }
   }

   public int DefaultPinModeIndex
   {
      get => (int)Settings.DefaultPinMode;
      set
      {
         var mode = (NotePinMode)Math.Clamp(value, 0, 2);
         if (Settings.DefaultPinMode == mode) return;
         Settings.DefaultPinMode = mode;
         OnPropertyChanged();
         Manager.SaveSettings();
      }
   }

   public LanguageOption? SelectedLanguage
   {
      get => _selectedLanguage;
      set
      {
         if (!SetProperty(ref _selectedLanguage, value) || value is null)
            return;

         Settings.LanguageFile = value.FileName;
         LocalizationService.Instance.LoadLanguage(value.FileName);
         Manager.SaveSettings();
      }
   }

   public string DataFolderPath => DataStorageService.Instance.DataFolder;
   public string LangFolderPath => LocalizationService.Instance.LangFolder;

   public string StatusMessage
   {
      get => _statusMessage;
      set
      {
         if (SetProperty(ref _statusMessage, value))
            OnPropertyChanged(nameof(HasStatusMessage));
      }
   }

   public bool HasStatusMessage => !string.IsNullOrWhiteSpace(_statusMessage);

   public ICommand SwitchToNotesCommand { get; }
   public ICommand SwitchToSettingsCommand { get; }

   public ICommand CreateNoteCommand { get; }
   public ICommand ShowAllNotesCommand { get; }
   public ICommand HideAllNotesCommand { get; }
   public ICommand ClearSearchCommand { get; }

   public ICommand SetFilterAllCommand { get; }
   public ICommand SetFilterOpenCommand { get; }
   public ICommand SetFilterHiddenCommand { get; }

   public ICommand ToggleThemeCommand { get; }
   public ICommand SetLightModeCommand { get; }
   public ICommand SetDarkModeCommand { get; }
   public ICommand SetCornerPresetCommand { get; }

   public ICommand SelectAppAccentCommand { get; }
   public ICommand ApplyCustomAppAccentCommand { get; }
   public ICommand SelectDefaultNoteColorCommand { get; }

   public ICommand ReloadLanguagesCommand { get; }
   public ICommand OpenLangFolderCommand { get; }
   public ICommand OpenDataFolderCommand { get; }
   public ICommand MigrateExternalImagesCommand { get; }
   public ICommand CleanUnusedMediaCommand { get; }
   public ICommand QuitAppCommand { get; }

   private void ApplyAppAccent(string hex)
   {
      var normalized = ThemeService.NormalizeHex(hex);
      Settings.AccentColor = normalized;
      CustomAppAccentHex = normalized;
      ThemeService.ApplyTheme(Settings.IsDarkMode, normalized);

      UpdateAppAccentSwatchSelection();
      foreach (var sw in DefaultNoteSwatches)
         sw.RefreshBrush();

      Manager.SaveSettings();
   }

   private void UpdateAppAccentSwatchSelection()
   {
      foreach (var sw in AppAccentSwatches)
         sw.IsSelected = string.Equals(sw.ColorHex, Settings.AccentColor, StringComparison.OrdinalIgnoreCase);
   }

   private void UpdateDefaultNoteSwatchSelection()
   {
      foreach (var sw in DefaultNoteSwatches)
         sw.IsSelected = string.Equals(sw.ColorHex, Settings.DefaultNoteColor, StringComparison.OrdinalIgnoreCase);
   }

   public void RefreshFilteredNotes()
   {
      var query = SearchQuery;
      var matched = Manager.Notes.Where(n =>
      {
         if (FilterMode == 1 && !n.IsOpen) return false;
         if (FilterMode == 2 && n.IsOpen) return false;
         return n.MatchesSearch(query);
      }).ToList();

      FilteredNotes.Clear();
      foreach (var item in matched)
         FilteredNotes.Add(item);

      OnPropertyChanged(nameof(TotalNotesCount));
      OnPropertyChanged(nameof(OpenNotesCount));
      OnPropertyChanged(nameof(HiddenNotesCount));
      OnPropertyChanged(nameof(HasNoFilteredNotes));
   }

   private void ReloadLanguages()
   {
      var currentFile = SelectedLanguage?.FileName ?? Settings.LanguageFile;
      LocalizationService.Instance.ScanLanguages();

      AvailableLanguages.Clear();
      foreach (var lang in LocalizationService.Instance.AvailableLanguages)
         AvailableLanguages.Add(lang);

      _selectedLanguage = AvailableLanguages.FirstOrDefault(l =>
                             string.Equals(l.FileName, currentFile, StringComparison.OrdinalIgnoreCase))
                          ?? AvailableLanguages.FirstOrDefault();
      OnPropertyChanged(nameof(SelectedLanguage));

      if (_selectedLanguage is not null)
         LocalizationService.Instance.LoadLanguage(_selectedLanguage.FileName);

      StatusMessage = LocalizationService.Instance.Format("MsgLanguagesReloaded", AvailableLanguages.Count);
   }

   private void MigrateExternalImages()
   {
      var migrated = DataStorageService.Instance.MigrateExternalImagesToAppData(Manager.Notes.Select(n => n.Model));
      foreach (var note in Manager.Notes)
         note.ReloadAllImages();

      Manager.SaveNotesImmediately();
      StatusMessage = LocalizationService.Instance.Format("MsgImagesMigrated", migrated);
   }

   private void CleanUnusedMedia()
   {
      var cleaned = DataStorageService.Instance.CleanUnusedMedia(Manager.Notes.Select(n => n.Model));
      StatusMessage = LocalizationService.Instance.Format("MsgUnusedMediaCleaned", cleaned);
   }

   private static void OpenFolderInExplorer(string folderPath)
   {
      try
      {
         System.IO.Directory.CreateDirectory(folderPath);
         Process.Start(new ProcessStartInfo
         {
            FileName = folderPath,
            UseShellExecute = true
         });
      }
      catch
      {
      }
   }
}
