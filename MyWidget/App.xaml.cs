using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MyWidget.Services;
using MyWidget.ViewModels;
using MyWidget.Views;

namespace MyWidget;

public partial class App : Application
{
   private const string SingleInstanceMutexName = @"Local\MyWidget_SingleInstance_Mutex_v1";
   private const string SingleInstanceWakeEventName = @"Local\MyWidget_SingleInstance_Wake_v1";

   private Mutex? _instanceMutex;
   private EventWaitHandle? _wakeEvent;
   private RegisteredWaitHandle? _registeredWakeWait;

   private WidgetManager? _widgetManager;
   private MainViewModel? _mainViewModel;
   private TrayIconService? _trayService;

   protected override void OnStartup(StartupEventArgs e)
   {
      base.OnStartup(e);

      _instanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
      if (!createdNew)
      {
         try
         {
            using var existingWake = EventWaitHandle.OpenExisting(SingleInstanceWakeEventName);
            existingWake.Set();
         }
         catch
         {
         }

         Shutdown();
         return;
      }

      _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstanceWakeEventName);
      _registeredWakeWait = ThreadPool.RegisterWaitForSingleObject(
         _wakeEvent,
         (_, _) => Dispatcher.BeginInvoke(() => _widgetManager?.ShowMainWindow()),
         null,
         -1,
         executeOnlyOnce: false);

      var settings = DataStorageService.Instance.LoadSettings();

      LocalizationService.Instance.ScanLanguages();
      LocalizationService.Instance.LoadLanguage(settings.LanguageFile);

      ThemeService.ApplyTheme(
         settings.IsDarkMode,
         settings.AccentColor,
         settings.CornerRadius,
         settings.ShowDescriptions);

      _widgetManager = new WidgetManager(settings);
      _widgetManager.InitializeNotes();

      _mainViewModel = new MainViewModel(_widgetManager);
      var mainWin = new MainWindow(_mainViewModel);
      _widgetManager.AttachMainWindow(mainWin);

      InitializeTrayIcon();

      var isWindowsStartup = e.Args.Any(a => string.Equals(a, "--startup", StringComparison.OrdinalIgnoreCase));
      if (!isWindowsStartup)
      {
         mainWin.Show();
      }
   }

   private void InitializeTrayIcon()
   {
      if (_widgetManager is null || _mainViewModel is null)
         return;

      _trayService = new TrayIconService();
      _trayService.LeftClick += (_, _) => _widgetManager.ShowMainWindow();
      _trayService.ContextMenu = BuildTrayContextMenu();
      _trayService.RightClickOpening += (_, _) =>
      {
         // Rebuild or refresh menu items with current theme/localization
         _trayService.ContextMenu = BuildTrayContextMenu();
      };
      _trayService.Initialize("MyWidget");
   }

   private ContextMenu BuildTrayContextMenu()
   {
      var loc = LocalizationService.Instance;
      var menu = new ContextMenu();

      var openItem = new MenuItem { Header = loc["MenuOpenManager"], FontWeight = FontWeights.SemiBold };
      openItem.Click += (_, _) => _widgetManager?.ShowMainWindow();
      menu.Items.Add(openItem);

      var newNoteItem = new MenuItem { Header = loc["BtnNewNote"] };
      newNoteItem.Click += (_, _) => _widgetManager?.CreateNewNote();
      menu.Items.Add(newNoteItem);

      menu.Items.Add(new Separator());

      var showAllItem = new MenuItem { Header = loc["BtnShowAll"] };
      showAllItem.Click += (_, _) => _widgetManager?.ShowAllNotes();
      menu.Items.Add(showAllItem);

      var hideAllItem = new MenuItem { Header = loc["BtnHideAll"] };
      hideAllItem.Click += (_, _) => _widgetManager?.HideAllNotes();
      menu.Items.Add(hideAllItem);

      menu.Items.Add(new Separator());

      if (_mainViewModel is not null)
      {
         var darkItem = new MenuItem
         {
            Header = loc["LabelDarkMode"],
            IsChecked = _mainViewModel.IsDarkMode
         };
         darkItem.Click += (_, _) => _mainViewModel.IsDarkMode = !_mainViewModel.IsDarkMode;
         menu.Items.Add(darkItem);

         var startupItem = new MenuItem
         {
            Header = loc["LabelStartWithWindows"],
            IsChecked = _mainViewModel.StartWithWindows
         };
         startupItem.Click += (_, _) => _mainViewModel.StartWithWindows = !_mainViewModel.StartWithWindows;
         menu.Items.Add(startupItem);
      }

      menu.Items.Add(new Separator());

      var quitItem = new MenuItem { Header = loc["BtnQuitApp"] };
      quitItem.Click += (_, _) => _widgetManager?.ExitApplication();
      menu.Items.Add(quitItem);

      return menu;
   }

   protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
   {
      _widgetManager?.SaveNotesImmediately();
      _widgetManager?.SaveSettings();
      base.OnSessionEnding(e);
   }

   protected override void OnExit(ExitEventArgs e)
   {
      _registeredWakeWait?.Unregister(null);
      _wakeEvent?.Dispose();
      _trayService?.Dispose();

      try
      {
         _instanceMutex?.ReleaseMutex();
      }
      catch
      {
      }
      _instanceMutex?.Dispose();

      base.OnExit(e);
   }
}
