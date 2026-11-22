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
   private const string RegestryRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

   private static readonly IntPtr HWND_TOPMOST = new(-1);
   private static readonly IntPtr HWND_NOTOPMOST = new(-2);

   private readonly NotifyIcon trayIcon;
   private readonly ContextMenuStrip trayMenu;

   public HiddenForm()
   {
      trayMenu = new ContextMenuStrip();
      var startupMenuItem = new ToolStripMenuItem("Start With Window");
      startupMenuItem.Checked = IsStartupEnabled();
      trayMenu.Items.AddRange(startupMenuItem);

      trayIcon = new NotifyIcon
      {
         Text = "AlwaysOnTop (Alt + T) ",
         Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
         ContextMenuStrip = trayMenu,
         Visible = true
      };

      if (!RegisterHotKey(Handle, HOTKEY_ID, MOD_ALT, VK_T))
      {
         MessageBox.Show("Failed to register hotkey Alt + T, maybe other application already used it");
         Application.Exit();
      }
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

   private bool IsStartupEnabled()
   {
      using var key = Registry.CurrentUser.OpenSubKey(RegestryRunPath, false);
      return key?.GetValue("AlwaysOnTop") != null;
   }

   private void OnToggleStartUp(object sender, EventArgs e)
   {
      var menuItem = (ToolStripMenuItem)sender;
      using (var key = Registry.CurrentUser.OpenSubKey(RegestryRunPath, false))
      {
         if (menuItem.Checked)
         {
            key?.DeleteValue(RegistryAppName, false);
            menuItem.Checked = false;
         }
         else
         {
            key?.SetValue(RegistryAppName, Application.ExecutablePath);
            menuItem.Checked = true;
         }
      }
   }

   private void OnExit(object sender, EventArgs e)
   {
      trayIcon.Visible = false;
      UnregisterHotKey(Handle, HOTKEY_ID);
      Application.Exit();
   }
}