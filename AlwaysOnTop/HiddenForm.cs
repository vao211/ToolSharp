using System.Runtime.InteropServices;

namespace AlwaysOnTop;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
public class HiddenForm : Form
{
   private const int HOTKEY_ID = 1;
   const uint MOD_ALT = 0x0001;
   private const uint VK_T = 0x54;
   
   const int GWL_EXSTYLE = -20;
   const uint WS_EX_TOPMOST = 0x00000008;
   const uint SWP_NOMOVE = 0x0002;
   const uint SWP_NOSIZE = 0x0001;
   const int DWMWA_BORDER_COLOR = 34;
   
   private const string RegistryAppName = "AlwaysOnTopTool";
   private const string RegestryRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
   
   static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
   static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
   
   [DllImport("user32.dll")]
   static extern IntPtr GetForegroundWindow();
   
   [DllImport("user32.dll")]
   static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

   [DllImport("user32.dll")]
   static extern int GetWindowLong(IntPtr hWnd, int nIndex);

   [DllImport("user32.dll")]
   static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

   [DllImport("user32.dll")]
   static extern bool UnregisterHotKey(IntPtr hWnd, int id);

   [DllImport("dwmapi.dll")]
   static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

   private NotifyIcon trayIcon;
   private ContextMenuStrip trayMenu;

   public HiddenForm()
   {
      trayMenu = new ContextMenuStrip();
      var startupMenuItem = new ToolStripMenuItem("Start With Window");
      startupMenuItem.Checked = IsStartupEnabled();
      trayMenu.Items.AddRange(new ToolStripItem[] { startupMenuItem });

      trayIcon = new NotifyIcon()
      {
         Text = "AlwaysOnTop (Alt + T) ",
         Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
         ContextMenuStrip = trayMenu,
         Visible = true
      };

      if (!RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, VK_T))
      {
         MessageBox.Show("Failed to register hotkey Alt + T, maybe other application already used it");
         Application.Exit();
      }
   }

   protected override void SetVisibleCore(bool value)
   {
      base.SetVisibleCore(false);
   }

   protected override void WndProc(ref Message m)
   {
      if (m.Msg == 0x312 && m.WParam.ToInt32() == HOTKEY_ID)
      {
         ToggleTopMost();
      }
      base.WndProc(ref m);
   }

   private void ToggleTopMost()
   {
      IntPtr hwnd = GetForegroundWindow();
      if (hwnd == IntPtr.Zero || hwnd == this.Handle) return;
      int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
      bool isTopMost = (exStyle & WS_EX_TOPMOST) == WS_EX_TOPMOST;

      if (isTopMost)
      {
         SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
         int colorDefault = unchecked((int)0xFFFFFFFF); 
         DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorDefault, sizeof(int));
      }
      else
      {
         SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
         int colorRed = 0x000000FF; 
         DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorRed, sizeof(int));
      }
   }
   private bool IsStartupEnabled()
   {
      using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegestryRunPath, false);
      return key?.GetValue("AlwaysOnTop") != null;
   }

   private void OnToggleStartUp(object sender, EventArgs e)
   {
      var menuItem = (ToolStripMenuItem)sender;
      using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegestryRunPath, false))
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
      UnregisterHotKey(this.Handle, HOTKEY_ID);
      Application.Exit();
   }
}