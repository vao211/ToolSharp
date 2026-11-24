using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using MyWidget.Models;
using MyWidget.ViewModels;
using MyWidget.Views;

namespace MyWidget.Services;

public class WidgetManager
{
   private readonly Dictionary<string, NoteWindow> _openWindows = new();
   private readonly DispatcherTimer _saveTimer;
   private MainWindow? _mainWindow;
   private bool _isShuttingDown;

   public WidgetManager(AppSettings settings)
   {
      Settings = settings;

      _saveTimer = new DispatcherTimer
      {
         Interval = TimeSpan.FromMilliseconds(400)
      };
      _saveTimer.Tick += (_, _) =>
      {
         _saveTimer.Stop();
         SaveNotesImmediately();
      };

      ThemeService.ThemeChanged += (_, _) =>
      {
         foreach (var n in Notes)
            n.RefreshPalette();
      };

      LocalizationService.Instance.LanguageChanged += (_, _) =>
      {
         foreach (var n in Notes)
            n.RefreshLocalizedTexts();
         NotesChanged?.Invoke(this, EventArgs.Empty);
      };
   }

   public AppSettings Settings { get; }
   public ObservableCollection<NoteViewModel> Notes { get; } = [];
   public bool IsShuttingDown => _isShuttingDown;

   public event EventHandler? NotesChanged;

   public void InitializeNotes()
   {
      var loaded = DataStorageService.Instance.LoadNotes();

      Notes.Clear();
      foreach (var model in loaded)
      {
         Notes.Add(new NoteViewModel(model, this));
      }

      foreach (var note in Notes.Where(n => n.IsOpen))
      {
         ShowNoteWindow(note, activate: false);
      }

      NotesChanged?.Invoke(this, EventArgs.Empty);
   }

   public void AttachMainWindow(MainWindow window)
   {
      _mainWindow = window;
   }

   public void ShowMainWindow()
   {
      if (_mainWindow is null) return;

      if (!_mainWindow.IsVisible)
         _mainWindow.Show();

      if (_mainWindow.WindowState == WindowState.Minimized)
         _mainWindow.WindowState = WindowState.Normal;

      _mainWindow.Activate();
   }

   public NoteViewModel CreateNewNote(NoteViewModel? nearNote = null)
   {
      var workArea = SystemParameters.WorkArea;
      double left;
      double top;

      if (nearNote is not null && !double.IsNaN(nearNote.Left) && !double.IsNaN(nearNote.Top))
      {
         left = nearNote.Left + 34;
         top = nearNote.Top + 34;
         if (left + 320 > workArea.Right)
            left = Math.Max(workArea.Left + 24, nearNote.Left - 340);
         if (top + 360 > workArea.Bottom)
            top = Math.Max(workArea.Top + 24, workArea.Top + 48);
      }
      else
      {
         var offset = (Notes.Count % 6) * 28;
         left = Math.Clamp(workArea.Right - 390 - offset, workArea.Left + 24, Math.Max(workArea.Left + 24, workArea.Right - 340));
         top = Math.Clamp(workArea.Top + 56 + offset, workArea.Top + 24, Math.Max(workArea.Top + 24, workArea.Bottom - 380));
      }

      var model = new NoteModel
      {
         ColorHex = nearNote?.ColorHex ?? Settings.DefaultNoteColor,
         PinMode = nearNote?.PinMode ?? Settings.DefaultPinMode,
         IsOpen = true,
         Left = left,
         Top = top,
         Width = 320,
         Height = 350,
         ExpandedHeight = 350,
         Blocks =
         [
            new NoteBlockModel { Type = NoteBlockType.Text }
         ]
      };

      var vm = new NoteViewModel(model, this);
      vm.Blocks[0].ShouldFocusOnLoad = true;
      Notes.Insert(0, vm);

      ShowNoteWindow(vm, activate: true);
      NotesChanged?.Invoke(this, EventArgs.Empty);
      RequestSave();
      return vm;
   }

   public NoteViewModel DuplicateNote(NoteViewModel source)
   {
      var workArea = SystemParameters.WorkArea;
      var left = double.IsNaN(source.Left) ? workArea.Left + 120 : Math.Min(source.Left + 32, workArea.Right - source.Width - 16);
      var top = double.IsNaN(source.Top) ? workArea.Top + 80 : Math.Min(source.Top + 32, workArea.Bottom - source.ExpandedHeight - 16);

      var cloneModel = new NoteModel
      {
         Title = string.IsNullOrWhiteSpace(source.Title)
            ? string.Empty
            : $"{source.Title} ({LocalizationService.Instance["CopySuffix"]})",
         ColorHex = source.ColorHex,
         PinMode = source.PinMode,
         IsOpen = true,
         IsLocked = false,
         IsCollapsed = false,
         BackgroundOpacity = source.BackgroundOpacity,
         ContentOpacity = source.ContentOpacity,
         Left = left,
         Top = top,
         Width = source.Width,
         Height = source.ExpandedHeight,
         ExpandedHeight = source.ExpandedHeight,
         Blocks = [.. source.Model.Blocks.Select(b => b.Clone())]
      };

      var vm = new NoteViewModel(cloneModel, this);
      Notes.Insert(0, vm);
      ShowNoteWindow(vm, activate: true);
      NotesChanged?.Invoke(this, EventArgs.Empty);
      RequestSave();
      return vm;
   }

   public void DeleteNoteWithConfirm(NoteViewModel note)
   {
      var loc = LocalizationService.Instance;
      var msg = loc.Format("ConfirmDeleteNoteMessage", note.DisplayTitle);

      Window? ownerWin = _mainWindow is { IsVisible: true, IsActive: true }
         ? _mainWindow
         : (_openWindows.TryGetValue(note.Id, out var nw) && nw.IsVisible ? nw : _mainWindow);

      var confirmed = ConfirmDialogWindow.ShowConfirm(
         loc["ConfirmDeleteNoteTitle"],
         msg,
         loc["BtnConfirmDelete"],
         isDanger: true,
         owner: ownerWin);

      if (!confirmed)
         return;

      if (_openWindows.TryGetValue(note.Id, out var win))
      {
         _openWindows.Remove(note.Id);
         win.ForceClose();
      }

      Notes.Remove(note);
      NotesChanged?.Invoke(this, EventArgs.Empty);
      SaveNotesImmediately();
   }

   public void ShowNoteWindow(NoteViewModel note, bool activate)
   {
      note.IsOpen = true;

      if (_openWindows.TryGetValue(note.Id, out var existing))
      {
         if (!existing.IsVisible)
            existing.Show();
         if (activate)
            existing.Activate();
         return;
      }

      var win = new NoteWindow(note);
      _openWindows[note.Id] = win;
      win.Closed += (_, _) =>
      {
         _openWindows.Remove(note.Id);
      };

      win.Show();
      if (activate)
         win.Activate();
   }

   public void HideNoteWindow(NoteViewModel note)
   {
      note.IsOpen = false;
      if (_openWindows.TryGetValue(note.Id, out var win))
      {
         _openWindows.Remove(note.Id);
         win.ForceClose();
      }
   }

   public void ShowAllNotes()
   {
      foreach (var note in Notes)
         ShowNoteWindow(note, activate: false);

      NotesChanged?.Invoke(this, EventArgs.Empty);
      RequestSave();
   }

   public void HideAllNotes()
   {
      foreach (var note in Notes)
         HideNoteWindow(note);

      NotesChanged?.Invoke(this, EventArgs.Empty);
      RequestSave();
   }

   public void CenterNoteOnScreen(NoteViewModel note)
   {
      var workArea = SystemParameters.WorkArea;
      var w = note.Width + 16;
      var h = (note.IsCollapsed ? 54 : note.ExpandedHeight + 16);
      var targetLeft = Math.Max(workArea.Left + 16, workArea.Left + (workArea.Width - w) / 2);
      var targetTop = Math.Max(workArea.Top + 16, workArea.Top + (workArea.Height - h) / 2);

      note.Left = targetLeft;
      note.Top = targetTop;

      ShowNoteWindow(note, activate: true);
      if (_openWindows.TryGetValue(note.Id, out var win))
      {
         win.ApplyExternalPosition(targetLeft, targetTop);
      }

      RequestSave();
   }

   public void MoveNoteOrder(NoteViewModel source, NoteViewModel target)
   {
      if (ReferenceEquals(source, target)) return;
      var oldIdx = Notes.IndexOf(source);
      var newIdx = Notes.IndexOf(target);
      if (oldIdx < 0 || newIdx < 0 || oldIdx == newIdx) return;

      Notes.Move(oldIdx, newIdx);
      NotesChanged?.Invoke(this, EventArgs.Empty);
      RequestSave();
   }

   public void NotifyNoteChanged(NoteViewModel note)
   {
      NotesChanged?.Invoke(this, EventArgs.Empty);
   }

   public void NotifyOpenCountsChanged()
   {
      NotesChanged?.Invoke(this, EventArgs.Empty);
   }

   public void RequestSave()
   {
      if (_isShuttingDown) return;
      _saveTimer.Stop();
      _saveTimer.Start();
   }

   public void SaveNotesImmediately()
   {
      _saveTimer.Stop();
      DataStorageService.Instance.SaveNotes(Notes.Select(n => n.Model));
   }

   public void SaveSettings()
   {
      DataStorageService.Instance.SaveSettings(Settings);
   }

   public void ExitApplication()
   {
      if (_isShuttingDown) return;
      _isShuttingDown = true;

      SaveNotesImmediately();
      SaveSettings();

      foreach (var win in _openWindows.Values.ToList())
      {
         win.ForceClose();
      }
      _openWindows.Clear();

      _mainWindow?.ForceClose();
      Application.Current.Shutdown();
   }
}
