using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AlwaysOnTop;

public class HiddenForm : Form
{
   private const int HOTKEY_ID = 1;
   private const uint MOD_ALT = 0x0001;
   private const uint VK_T = 0x54;

   private const int GWL_EXSTYLE = -20;
   private const uint WS_EX_TOPMOST = 0x00000008;
   private const uint SWP_NOMOVE = 0x0002;
   private const uint SWP_NOSIZE = 0x0001;
   private const int DWMWA_BORDER_COLOR = 34;

   private const string RegistryAppName = "AlwaysOnTopTool";
   private const string LegacyRegistryAppName = "AlwaysOnTop";
   private const string RegistryRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
   private const string StartupApprovedRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

   private static readonly IntPtr HWND_TOPMOST = new(-1);
   private static readonly IntPtr HWND_NOTOPMOST = new(-2);

   private readonly NotifyIcon? trayIcon;
   private readonly ContextMenuStrip? trayMenu;
   private readonly Icon? extractedIcon;
   private readonly RegisteredWaitHandle? shutdownWaitHandle;
   private bool hotkeyRegistered;

   public bool IsInitialized { get; }

   public HiddenForm(EventWaitHandle? shutdownEvent = null)
   {
      ShowInTaskbar = false;
      WindowState = FormWindowState.Minimized;

      if (!RegisterHotKey(Handle, HOTKEY_ID, MOD_ALT, VK_T))
      {
         MessageBox.Show(
            "Failed to register hotkey Alt + T, maybe another application is already using it.",
            "AlwaysOnTop",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
         return;
      }

      hotkeyRegistered = true;

      trayMenu = new ContextMenuStrip();

      var startupMenuItem = new ToolStripMenuItem("Start with Windows");
      startupMenuItem.Checked = IsStartupEnabled();
      startupMenuItem.Click += OnToggleStartUp;

      var exitMenuItem = new ToolStripMenuItem("Exit");
      exitMenuItem.Click += OnExit;

      trayMenu.Items.Add(startupMenuItem);
      trayMenu.Items.Add(new ToolStripSeparator());
      trayMenu.Items.Add(exitMenuItem);
      trayMenu.Opening += (_, _) => startupMenuItem.Checked = IsStartupEnabled();

      var exePath = GetExecutablePath();
      if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
      {
         extractedIcon = Icon.ExtractAssociatedIcon(exePath);
      }

      trayIcon = new NotifyIcon
      {
         Text = "AlwaysOnTop (Alt + T)",
         Icon = extractedIcon ?? SystemIcons.Application,
         ContextMenuStrip = trayMenu,
         Visible = true
      };

      if (shutdownEvent != null)
      {
         shutdownWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            shutdownEvent,
            (_, _) => RequestExit(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: true);
      }

      IsInitialized = true;
   }

   [DllImport("user32.dll")]
   private static extern IntPtr GetForegroundWindow();

   [DllImport("user32.dll")]
   private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy,
      uint uFlags);

   [DllImport("user32.dll")]
   private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

   [DllImport("user32.dll")]
   private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

   [DllImport("user32.dll")]
   private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

   [DllImport("dwmapi.dll")]
   private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

   protected override void SetVisibleCore(bool value)
   {
      base.SetVisibleCore(false);
   }

   protected override void WndProc(ref Message m)
   {
      if (m.Msg == 0x312 && m.WParam.ToInt32() == HOTKEY_ID) ToggleTopMost();
      base.WndProc(ref m);
   }

   private void ToggleTopMost()
   {
      var hwnd = GetForegroundWindow();
      if (hwnd == IntPtr.Zero || hwnd == Handle) return;
      var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
      var isTopMost = (exStyle & WS_EX_TOPMOST) == WS_EX_TOPMOST;

      if (isTopMost)
      {
         SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
         var colorDefault = unchecked((int)0xFFFFFFFF);
         DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorDefault, sizeof(int));
      }
      else
      {
         SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
         var colorRed = 0x000000FF;
         DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorRed, sizeof(int));
      }
   }

   private static string GetExecutablePath()
   {
      return Environment.ProcessPath ?? Application.ExecutablePath;
   }

   private static bool IsStartupApproved(string valueName)
   {
      try
      {
         using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRunPath, false);
         if (approvedKey?.GetValue(valueName) is byte[] data && data.Length > 0)
         {
            // Even first byte (e.g. 0x02, 0x06) means enabled; odd (e.g. 0x03) means disabled in Task Manager.
            return (data[0] & 1) == 0;
         }
      }
      catch
      {
         // Ignore if StartupApproved key cannot be read.
      }

      return true;
   }

   private static void ClearStartupApprovedOverrides()
   {
      try
      {
         using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRunPath, writable: true);
         approvedKey?.DeleteValue(RegistryAppName, false);
         approvedKey?.DeleteValue(LegacyRegistryAppName, false);
      }
      catch
      {
         // Non-critical if the override key does not exist or cannot be modified.
      }
   }

   private bool IsStartupEnabled()
   {
      using var key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, false);
      if (key == null) return false;

      var hasCurrent = key.GetValue(RegistryAppName) != null && IsStartupApproved(RegistryAppName);
      var hasLegacy = key.GetValue(LegacyRegistryAppName) != null && IsStartupApproved(LegacyRegistryAppName);
      return hasCurrent || hasLegacy;
   }

   private void OnToggleStartUp(object? sender, EventArgs e)
   {
      if (sender is not ToolStripMenuItem menuItem) return;

      try
      {
         if (IsStartupEnabled())
         {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, writable: true);
            key?.DeleteValue(RegistryAppName, false);
            key?.DeleteValue(LegacyRegistryAppName, false);
            ClearStartupApprovedOverrides();
         }
         else
         {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryRunPath);
            key.DeleteValue(LegacyRegistryAppName, false);
            key.SetValue(RegistryAppName, $"\"{GetExecutablePath()}\"");
            ClearStartupApprovedOverrides();
         }
      }
      catch (Exception ex)
      {
         MessageBox.Show(
            $"Failed to update Windows startup setting: {ex.Message}",
            "AlwaysOnTop",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
      }

      menuItem.Checked = IsStartupEnabled();
   }

   private void RequestExit()
   {
      if (IsDisposed) return;

      try
      {
         if (InvokeRequired && IsHandleCreated)
         {
            BeginInvoke(ExitApplication);
         }
         else
         {
            ExitApplication();
         }
      }
      catch (ObjectDisposedException)
      {
      }
      catch (InvalidOperationException)
      {
      }
   }

   private void OnExit(object? sender, EventArgs e)
   {
      ExitApplication();
   }

   private void ExitApplication()
   {
      CleanupResources();
      Application.Exit();
   }

   private void CleanupResources()
   {
      if (trayIcon != null)
      {
         trayIcon.Visible = false;
      }

      if (hotkeyRegistered && IsHandleCreated)
      {
         UnregisterHotKey(Handle, HOTKEY_ID);
         hotkeyRegistered = false;
      }
   }

   protected override void Dispose(bool disposing)
   {
      if (disposing)
      {
         shutdownWaitHandle?.Unregister(null);
         CleanupResources();
         trayIcon?.Dispose();
         trayMenu?.Dispose();
         extractedIcon?.Dispose();
      }

      base.Dispose(disposing);
   }
}