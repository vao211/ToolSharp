using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MyWidget.Models;
using MyWidget.Services;

namespace MyWidget.ViewModels;

public class NoteViewModel : ViewModelBase
{
   private NoteVisualPalette _palette;
   private NoteBlockViewModel? _activeBlock;
   private string _customColorHex;

   public NoteViewModel(NoteModel model, WidgetManager manager)
   {
      Model = model;
      Manager = manager;
      _customColorHex = string.IsNullOrWhiteSpace(model.ColorHex) ? ThemeService.CurrentAccentHex : model.ColorHex;

      _palette = ThemeService.ComputeNotePalette(model.ColorHex, ThemeService.IsDarkMode, ThemeService.CurrentAccentHex);

      Blocks = new ObservableCollection<NoteBlockViewModel>(
         model.Blocks.Select(b => new NoteBlockViewModel(b, this)));

      if (Blocks.Count == 0)
      {
         var initial = new NoteBlockModel { Type = NoteBlockType.Text };
         model.Blocks.Add(initial);
         Blocks.Add(new NoteBlockViewModel(initial, this));
      }

      ColorSwatches = new ObservableCollection<ColorSwatchItem>(
         ThemeService.NoteColorPresets.Select(hex => new ColorSwatchItem(hex, string.IsNullOrEmpty(hex))));
      UpdateSwatchSelection();

      AddTextBlockCommand = new RelayCommand(() => AddBlock(NoteBlockType.Text, after: ActiveBlock, focus: true));
      AddCheckBoxBlockCommand = new RelayCommand(() => AddBlock(NoteBlockType.CheckBox, after: ActiveBlock, focus: true));
      AddQuoteBlockCommand = new RelayCommand(() => AddBlock(NoteBlockType.Quote, after: ActiveBlock, focus: true));
      AddDividerBlockCommand = new RelayCommand(() => AddBlock(NoteBlockType.Divider, after: ActiveBlock, focus: false));
      AddImageDialogCommand = new RelayCommand(PromptAndAddImages);

      SelectNoteColorCommand = new RelayCommand(p =>
      {
         if (p is ColorSwatchItem swatch)
            ColorHex = swatch.ColorHex;
      });
      ApplyCustomNoteColorCommand = new RelayCommand(() =>
      {
         if (ThemeService.TryParseHexColor(CustomColorHex, out _))
            ColorHex = ThemeService.NormalizeHex(CustomColorHex);
      });

      CyclePinModeCommand = new RelayCommand(CyclePinMode);
      SetPinDesktopCommand = new RelayCommand(() => PinMode = NotePinMode.Desktop);
      SetPinTopmostCommand = new RelayCommand(() => PinMode = NotePinMode.AlwaysOnTop);
      SetPinNormalCommand = new RelayCommand(() => PinMode = NotePinMode.Normal);

      ToggleLockCommand = new RelayCommand(() => IsLocked = !IsLocked);
      ToggleCollapseCommand = new RelayCommand(() => IsCollapsed = !IsCollapsed);

      ToggleBoldActiveCommand = new RelayCommand(() => EnsureTargetBlock()?.ToggleBoldCommand.Execute(null));
      ToggleItalicActiveCommand = new RelayCommand(() => EnsureTargetBlock()?.ToggleItalicCommand.Execute(null));
      ToggleUnderlineActiveCommand = new RelayCommand(() => EnsureTargetBlock()?.ToggleUnderlineCommand.Execute(null));
      ToggleStrikeActiveCommand = new RelayCommand(() => EnsureTargetBlock()?.ToggleStrikethroughCommand.Execute(null));
      CycleFontSizeActiveCommand = new RelayCommand(() => EnsureTargetBlock()?.CycleFontSizeCommand.Execute(null));

      ShowNoteCommand = new RelayCommand(() => Manager.ShowNoteWindow(this, activate: true));
      HideNoteCommand = new RelayCommand(() => Manager.HideNoteWindow(this));
      ToggleOpenCommand = new RelayCommand(() =>
      {
         if (IsOpen)
            Manager.HideNoteWindow(this);
         else
            Manager.ShowNoteWindow(this, activate: true);
      });

      NewSiblingNoteCommand = new RelayCommand(() => Manager.CreateNewNote(nearNote: this));
      DuplicateNoteCommand = new RelayCommand(() => Manager.DuplicateNote(this));
      DeleteNoteCommand = new RelayCommand(() => Manager.DeleteNoteWithConfirm(this));
      CenterOnScreenCommand = new RelayCommand(() => Manager.CenterNoteOnScreen(this));
      OpenManagerCommand = new RelayCommand(() => Manager.ShowMainWindow());
   }

   public NoteModel Model { get; }
   public WidgetManager Manager { get; }

   public ObservableCollection<NoteBlockViewModel> Blocks { get; }
   public ObservableCollection<ColorSwatchItem> ColorSwatches { get; }

   public event EventHandler? PinModeChanged;
   public event EventHandler? CollapseStateChanged;

   public string Id => Model.Id;

   public NoteVisualPalette Palette
   {
      get => _palette;
      private set => SetProperty(ref _palette, value);
   }

   public NoteBlockViewModel? ActiveBlock
   {
      get => _activeBlock;
      set => SetProperty(ref _activeBlock, value);
   }

   public string Title
   {
      get => Model.Title;
      set
      {
         if (Model.Title == value) return;
         Model.Title = value;
         OnPropertyChanged();
         OnPropertyChanged(nameof(DisplayTitle));
         NotifyContentModified();
      }
   }

   public string DisplayTitle
   {
      get
      {
         if (!string.IsNullOrWhiteSpace(Model.Title))
            return Model.Title.Trim();

         var firstText = Blocks.FirstOrDefault(b =>
            (b.IsTextType || b.IsCheckBoxType || b.IsQuoteType) && !string.IsNullOrWhiteSpace(b.Text))?.Text;

         if (!string.IsNullOrWhiteSpace(firstText))
         {
            var firstLine = firstText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(firstLine))
               return firstLine.Length > 42 ? firstLine[..42] + "…" : firstLine;
         }

         return LocalizationService.Instance["UntitledNote"];
      }
   }

   public string PreviewSummary
   {
      get
      {
         var lines = new List<string>();
         foreach (var b in Blocks)
         {
            if (b.IsTextType || b.IsQuoteType)
            {
               if (!string.IsNullOrWhiteSpace(b.Text))
                  lines.Add(b.Text.Trim());
            }
            else if (b.IsCheckBoxType)
            {
               var mark = b.IsChecked ? "☑" : "☐";
               if (!string.IsNullOrWhiteSpace(b.Text))
                  lines.Add($"{mark} {b.Text.Trim()}");
            }
            if (lines.Count >= 3) break;
         }

         if (lines.Count == 0)
         {
            return ImageCount > 0
               ? LocalizationService.Instance.Format("NoteImagesOnlySummary", ImageCount)
               : LocalizationService.Instance["EmptyNoteHint"];
         }

         return string.Join(Environment.NewLine, lines);
      }
   }

   public int TotalCheckCount => Blocks.Count(b => b.IsCheckBoxType);
   public int CompletedCheckCount => Blocks.Count(b => b.IsCheckBoxType && b.IsChecked);
   public bool HasChecks => TotalCheckCount > 0;
   public string CheckStatsText => HasChecks ? $"☑ {CompletedCheckCount}/{TotalCheckCount}" : string.Empty;

   public int ImageCount => Blocks.Count(b => b.IsImageType);
   public bool HasImages => ImageCount > 0;
   public string ImageStatsText => HasImages ? $"🖼 {ImageCount}" : string.Empty;

   public string UpdatedTimeText => Model.UpdatedAt.ToString("dd/MM HH:mm");

   public string ColorHex
   {
      get => Model.ColorHex;
      set
      {
         var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : ThemeService.NormalizeHex(value);
         if (Model.ColorHex == normalized) return;
         Model.ColorHex = normalized;
         if (!string.IsNullOrEmpty(normalized))
            CustomColorHex = normalized;

         RefreshPalette();
         UpdateSwatchSelection();
         OnPropertyChanged();
         Manager.RequestSave();
      }
   }

   public string CustomColorHex
   {
      get => _customColorHex;
      set => SetProperty(ref _customColorHex, value);
   }

   public NotePinMode PinMode
   {
      get => Model.PinMode;
      set
      {
         if (Model.PinMode == value) return;
         Model.PinMode = value;
         RefreshPinProperties();
         PinModeChanged?.Invoke(this, EventArgs.Empty);
         Manager.RequestSave();
      }
   }

   public bool IsDesktopPinned => PinMode == NotePinMode.Desktop;
   public bool IsAlwaysOnTop => PinMode == NotePinMode.AlwaysOnTop;
   public bool IsNormalWindow => PinMode == NotePinMode.Normal;

   public string PinModeIcon => PinMode switch
   {
      NotePinMode.Desktop => "📌",
      NotePinMode.AlwaysOnTop => "⬆",
      _ => "🗔"
   };

   public string PinModeLabel => PinMode switch
   {
      NotePinMode.Desktop => LocalizationService.Instance["PinModeDesktop"],
      NotePinMode.AlwaysOnTop => LocalizationService.Instance["PinModeTopmost"],
      _ => LocalizationService.Instance["PinModeNormal"]
   };

   public bool IsOpen
   {
      get => Model.IsOpen;
      set
      {
         if (Model.IsOpen == value) return;
         Model.IsOpen = value;
         OnPropertyChanged();
         OnPropertyChanged(nameof(OpenStatusBadge));
         Manager.NotifyOpenCountsChanged();
         Manager.RequestSave();
      }
   }

   public string OpenStatusBadge => IsOpen
      ? LocalizationService.Instance["StatusPinnedOpen"]
      : LocalizationService.Instance["StatusHidden"];

   public bool IsLocked
   {
      get => Model.IsLocked;
      set
      {
         if (Model.IsLocked == value) return;
         Model.IsLocked = value;
         OnPropertyChanged();
         OnPropertyChanged(nameof(IsEditable));
         OnPropertyChanged(nameof(LockIcon));
         Manager.RequestSave();
      }
   }

   public bool IsEditable => !IsLocked;
   public string LockIcon => IsLocked ? "🔒" : "🔓";

   public bool IsCollapsed
   {
      get => Model.IsCollapsed;
      set
      {
         if (Model.IsCollapsed == value) return;
         Model.IsCollapsed = value;
         OnPropertyChanged();
         OnPropertyChanged(nameof(CollapseIcon));
         CollapseStateChanged?.Invoke(this, EventArgs.Empty);
         Manager.RequestSave();
      }
   }

   public string CollapseIcon => IsCollapsed ? "▾" : "▴";

   public double BackgroundOpacity
   {
      get => Math.Clamp(Model.BackgroundOpacity, 0.0, 1.0);
      set
      {
         var clamped = Math.Round(Math.Clamp(value, 0.0, 1.0), 2);
         if (Math.Abs(Model.BackgroundOpacity - clamped) < 0.005) return;
         Model.BackgroundOpacity = clamped;
         OnPropertyChanged();
         OnPropertyChanged(nameof(BackgroundOpacityPercentText));
         Manager.RequestSave();
      }
   }

   public string BackgroundOpacityPercentText => $"{(int)Math.Round(BackgroundOpacity * 100)}%";

   public double ContentOpacity
   {
      get => Math.Clamp(Model.ContentOpacity, 0.20, 1.0);
      set
      {
         var clamped = Math.Round(Math.Clamp(value, 0.20, 1.0), 2);
         if (Math.Abs(Model.ContentOpacity - clamped) < 0.005) return;
         Model.ContentOpacity = clamped;
         OnPropertyChanged();
         OnPropertyChanged(nameof(ContentOpacityPercentText));
         Manager.RequestSave();
      }
   }

   public string ContentOpacityPercentText => $"{(int)Math.Round(ContentOpacity * 100)}%";

   public double Left
   {
      get => Model.Left;
      set
      {
         if (Math.Abs(Model.Left - value) < 0.5) return;
         Model.Left = value;
         OnPropertyChanged();
         Manager.RequestSave();
      }
   }

   public double Top
   {
      get => Model.Top;
      set
      {
         if (Math.Abs(Model.Top - value) < 0.5) return;
         Model.Top = value;
         OnPropertyChanged();
         Manager.RequestSave();
      }
   }

   public double Width
   {
      get => Model.Width < 220 ? 320 : Model.Width;
      set
      {
         var v = Math.Max(220, value);
         if (Math.Abs(Model.Width - v) < 0.5) return;
         Model.Width = v;
         OnPropertyChanged();
         Manager.RequestSave();
      }
   }

   public double Height
   {
      get => Model.Height < 52 ? 360 : Model.Height;
      set
      {
         var v = Math.Max(52, value);
         if (Math.Abs(Model.Height - v) < 0.5) return;
         Model.Height = v;
         if (!IsCollapsed && v >= 140)
            Model.ExpandedHeight = v;
         OnPropertyChanged();
         Manager.RequestSave();
      }
   }

   public double ExpandedHeight
   {
      get => Model.ExpandedHeight < 160 ? 360 : Model.ExpandedHeight;
      set => Model.ExpandedHeight = Math.Max(160, value);
   }

   public ICommand AddTextBlockCommand { get; }
   public ICommand AddCheckBoxBlockCommand { get; }
   public ICommand AddQuoteBlockCommand { get; }
   public ICommand AddDividerBlockCommand { get; }
   public ICommand AddImageDialogCommand { get; }

   public ICommand SelectNoteColorCommand { get; }
   public ICommand ApplyCustomNoteColorCommand { get; }

   public ICommand CyclePinModeCommand { get; }
   public ICommand SetPinDesktopCommand { get; }
   public ICommand SetPinTopmostCommand { get; }
   public ICommand SetPinNormalCommand { get; }

   public ICommand ToggleLockCommand { get; }
   public ICommand ToggleCollapseCommand { get; }

   public ICommand ToggleBoldActiveCommand { get; }
   public ICommand ToggleItalicActiveCommand { get; }
   public ICommand ToggleUnderlineActiveCommand { get; }
   public ICommand ToggleStrikeActiveCommand { get; }
   public ICommand CycleFontSizeActiveCommand { get; }

   public ICommand ShowNoteCommand { get; }
   public ICommand HideNoteCommand { get; }
   public ICommand ToggleOpenCommand { get; }
   public ICommand NewSiblingNoteCommand { get; }
   public ICommand DuplicateNoteCommand { get; }
   public ICommand DeleteNoteCommand { get; }
   public ICommand CenterOnScreenCommand { get; }
   public ICommand OpenManagerCommand { get; }

   public void RefreshPalette()
   {
      Palette = ThemeService.ComputeNotePalette(Model.ColorHex, ThemeService.IsDarkMode, ThemeService.CurrentAccentHex);
      foreach (var sw in ColorSwatches)
         sw.RefreshBrush();
   }

   public void RefreshLocalizedTexts()
   {
      OnPropertyChanged(nameof(DisplayTitle));
      OnPropertyChanged(nameof(PreviewSummary));
      OnPropertyChanged(nameof(PinModeLabel));
      OnPropertyChanged(nameof(OpenStatusBadge));
   }

   private void RefreshPinProperties()
   {
      OnPropertyChanged(nameof(PinMode));
      OnPropertyChanged(nameof(IsDesktopPinned));
      OnPropertyChanged(nameof(IsAlwaysOnTop));
      OnPropertyChanged(nameof(IsNormalWindow));
      OnPropertyChanged(nameof(PinModeIcon));
      OnPropertyChanged(nameof(PinModeLabel));
   }

   private void UpdateSwatchSelection()
   {
      foreach (var sw in ColorSwatches)
      {
         sw.IsSelected = string.Equals(sw.ColorHex, Model.ColorHex, StringComparison.OrdinalIgnoreCase);
      }
   }

   private void CyclePinMode()
   {
      PinMode = PinMode switch
      {
         NotePinMode.Desktop => NotePinMode.AlwaysOnTop,
         NotePinMode.AlwaysOnTop => NotePinMode.Normal,
         _ => NotePinMode.Desktop
      };
   }

   private NoteBlockViewModel? EnsureTargetBlock()
   {
      if (IsLocked) return null;
      if (ActiveBlock is not null && (ActiveBlock.IsTextType || ActiveBlock.IsCheckBoxType || ActiveBlock.IsQuoteType))
         return ActiveBlock;

      return Blocks.FirstOrDefault(b => b.IsTextType || b.IsCheckBoxType || b.IsQuoteType);
   }

   public NoteBlockViewModel? AddBlock(NoteBlockType type, NoteBlockViewModel? after = null, bool focus = true)
   {
      if (IsLocked) return null;

      var blockModel = new NoteBlockModel { Type = type };
      var vm = new NoteBlockViewModel(blockModel, this)
      {
         ShouldFocusOnLoad = focus
      };

      var insertIndex = Blocks.Count;
      if (after is not null)
      {
         var idx = Blocks.IndexOf(after);
         if (idx >= 0)
            insertIndex = idx + 1;
      }

      Model.Blocks.Insert(insertIndex, blockModel);
      Blocks.Insert(insertIndex, vm);
      ActiveBlock = vm;
      NotifyContentModified();
      return vm;
   }

   public void RemoveBlock(NoteBlockViewModel block)
   {
      if (IsLocked) return;
      var idx = Blocks.IndexOf(block);
      if (idx < 0) return;

      Model.Blocks.RemoveAt(idx);
      Blocks.RemoveAt(idx);

      if (Blocks.Count == 0)
      {
         var emptyText = new NoteBlockModel { Type = NoteBlockType.Text };
         Model.Blocks.Add(emptyText);
         var vm = new NoteBlockViewModel(emptyText, this) { ShouldFocusOnLoad = true };
         Blocks.Add(vm);
         ActiveBlock = vm;
      }
      else
      {
         var targetIdx = Math.Clamp(idx - 1, 0, Blocks.Count - 1);
         ActiveBlock = Blocks[targetIdx];
         ActiveBlock.ShouldFocusOnLoad = true;
      }

      NotifyContentModified();
   }

   public void MoveBlock(NoteBlockViewModel block, int delta)
   {
      if (IsLocked) return;
      var oldIdx = Blocks.IndexOf(block);
      if (oldIdx < 0) return;

      var newIdx = oldIdx + delta;
      if (newIdx < 0 || newIdx >= Blocks.Count) return;

      var model = Model.Blocks[oldIdx];
      Model.Blocks.RemoveAt(oldIdx);
      Model.Blocks.Insert(newIdx, model);

      Blocks.Move(oldIdx, newIdx);
      NotifyContentModified();
   }

   public void PromptAndAddImages()
   {
      if (IsLocked) return;

      var dlg = new OpenFileDialog
      {
         Title = LocalizationService.Instance["DialogSelectImageTitle"],
         Filter = "Image Files (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|All Files (*.*)|*.*",
         Multiselect = true
      };

      if (dlg.ShowDialog() == true)
      {
         foreach (var file in dlg.FileNames)
         {
            AddImageFromFile(file);
         }
      }
   }

   public void AddImageFromFile(string filePath)
   {
      if (IsLocked || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
         return;

      var ext = Path.GetExtension(filePath).ToLowerInvariant();
      if (ext is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".tiff" or ".ico"))
         return;

      var copySetting = Manager.Settings.CopyImagesToAppData;
      var (storedPath, isAppRes) = DataStorageService.Instance.ImportImageFile(filePath, copySetting);

      var blockModel = new NoteBlockModel
      {
         Type = NoteBlockType.Image,
         ImagePath = storedPath,
         IsAppResource = isAppRes,
         ImageMaxHeight = 240
      };

      var insertIndex = ActiveBlock is not null && Blocks.IndexOf(ActiveBlock) >= 0
         ? Blocks.IndexOf(ActiveBlock) + 1
         : Blocks.Count;

      Model.Blocks.Insert(insertIndex, blockModel);
      var vm = new NoteBlockViewModel(blockModel, this);
      Blocks.Insert(insertIndex, vm);
      ActiveBlock = vm;
      NotifyContentModified();
   }

   public void AddImageFromClipboardBitmap(BitmapSource bitmap)
   {
      if (IsLocked) return;

      var (storedPath, isAppRes) = DataStorageService.Instance.SaveClipboardBitmap(bitmap);
      var blockModel = new NoteBlockModel
      {
         Type = NoteBlockType.Image,
         ImagePath = storedPath,
         IsAppResource = isAppRes,
         ImageMaxHeight = 240
      };

      var insertIndex = ActiveBlock is not null && Blocks.IndexOf(ActiveBlock) >= 0
         ? Blocks.IndexOf(ActiveBlock) + 1
         : Blocks.Count;

      Model.Blocks.Insert(insertIndex, blockModel);
      var vm = new NoteBlockViewModel(blockModel, this);
      Blocks.Insert(insertIndex, vm);
      ActiveBlock = vm;
      NotifyContentModified();
   }

   public void ReloadAllImages()
   {
      foreach (var b in Blocks)
      {
         if (b.IsImageType)
            b.ReloadImage();
      }
   }

   public bool MatchesSearch(string? query)
   {
      if (string.IsNullOrWhiteSpace(query))
         return true;

      var q = query.Trim();
      if (Title.Contains(q, StringComparison.CurrentCultureIgnoreCase))
         return true;

      return Blocks.Any(b => !string.IsNullOrWhiteSpace(b.Text) &&
                             b.Text.Contains(q, StringComparison.CurrentCultureIgnoreCase));
   }

   public void NotifyContentModified()
   {
      Model.UpdatedAt = DateTime.Now;
      OnPropertyChanged(nameof(DisplayTitle));
      OnPropertyChanged(nameof(PreviewSummary));
      OnPropertyChanged(nameof(TotalCheckCount));
      OnPropertyChanged(nameof(CompletedCheckCount));
      OnPropertyChanged(nameof(HasChecks));
      OnPropertyChanged(nameof(CheckStatsText));
      OnPropertyChanged(nameof(ImageCount));
      OnPropertyChanged(nameof(HasImages));
      OnPropertyChanged(nameof(ImageStatsText));
      OnPropertyChanged(nameof(UpdatedTimeText));
      Manager.NotifyNoteChanged(this);
      Manager.RequestSave();
   }
}
