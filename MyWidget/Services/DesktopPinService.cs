using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MyWidget.Models;

namespace MyWidget.Services;

public static class DesktopPinService
{
   private const int GWL_EXSTYLE = -20;
   private const int GWLP_HWNDPARENT = -8;

   private const int WS_EX_TOOLWINDOW = 0x00000080;
   private const int WS_EX_APPWINDOW = 0x00040000;

   private const uint SWP_NOSIZE = 0x0001;
   private const uint SWP_NOMOVE = 0x0002;
   private const uint SWP_NOZORDER = 0x0004;
   private const uint SWP_NOACTIVATE = 0x0010;
   private const uint SWP_FRAMECHANGED = 0x0020;

   private static readonly IntPtr HWND_TOPMOST = new(-1);
   private static readonly IntPtr HWND_NOTOPMOST = new(-2);
   private static readonly IntPtr HWND_BOTTOM = new(1);

   public const int WM_NCLBUTTONDOWN = 0x00A1;
   public const int HTCAPTION = 2;
   public const int WM_SYSCOMMAND = 0x0112;
   public const int SC_MINIMIZE = 0xF020;

   [StructLayout(LayoutKind.Sequential)]
   public struct Win32Point
   {
      public int X;
      public int Y;
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct Win32Rect
   {
      public int Left;
      public int Top;
      public int Right;
      public int Bottom;
   }

   private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

   [DllImport("user32.dll", CharSet = CharSet.Unicode)]
   private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

   [DllImport("user32.dll", CharSet = CharSet.Unicode)]
   private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

   [DllImport("user32.dll")]
   private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

   [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
   private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

   [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
   private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

   [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
   private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

   [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
   private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

   [DllImport("user32.dll")]
   private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

   [DllImport("user32.dll")]
   private static extern bool ReleaseCapture();

   [DllImport("user32.dll")]
   private static extern IntPtr SendMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

   [DllImport("user32.dll")]
   private static extern bool GetCursorPos(out Win32Point lpPoint);

   [DllImport("user32.dll")]
   private static extern bool GetWindowRect(IntPtr hWnd, out Win32Rect lpRect);

   private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
      IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

   private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
      IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

   public static IntPtr FindDesktopShellHost()
   {
      var progman = FindWindow("Progman", null);
      if (progman != IntPtr.Zero)
      {
         var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
         if (defView != IntPtr.Zero)
            return progman;
      }

      var foundHost = IntPtr.Zero;
      EnumWindows((topHwnd, _) =>
      {
         var defView = FindWindowEx(topHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
         if (defView != IntPtr.Zero)
         {
            foundHost = topHwnd;
            return false;
         }
         return true;
      }, IntPtr.Zero);

      return foundHost != IntPtr.Zero ? foundHost : progman;
   }

   public static void ApplyPinMode(Window window, NotePinMode mode)
   {
      var hwnd = new WindowInteropHelper(window).Handle;
      if (hwnd == IntPtr.Zero) return;

      var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();

      switch (mode)
      {
         case NotePinMode.Desktop:
         {
            window.Topmost = false;
            window.ShowInTaskbar = false;

            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));

            var desktopHost = FindDesktopShellHost();
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, desktopHost);

            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
               SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            break;
         }

         case NotePinMode.AlwaysOnTop:
         {
            window.ShowInTaskbar = false;
            window.Topmost = true;

            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, IntPtr.Zero);

            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
               SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            break;
         }

         case NotePinMode.Normal:
         default:
         {
            window.Topmost = false;
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, IntPtr.Zero);

            exStyle &= ~WS_EX_TOOLWINDOW;
            exStyle |= WS_EX_APPWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));

            window.ShowInTaskbar = true;

            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
               SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            break;
         }
      }
   }

   public static void StartNativeDragMove(Window window)
   {
      var hwnd = new WindowInteropHelper(window).Handle;
      if (hwnd == IntPtr.Zero) return;

      ReleaseCapture();
      SendMessageW(hwnd, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
   }

   public static bool TryGetCursorPhysical(out Win32Point pt) => GetCursorPos(out pt);

   public static bool TryGetWindowBoundsPhysical(Window window, out Win32Rect rect)
   {
      var hwnd = new WindowInteropHelper(window).Handle;
      if (hwnd == IntPtr.Zero)
      {
         rect = default;
         return false;
      }

      return GetWindowRect(hwnd, out rect);
   }

   public static void SetWindowBoundsPhysical(Window window, int x, int y, int width, int height)
   {
      var hwnd = new WindowInteropHelper(window).Handle;
      if (hwnd == IntPtr.Zero) return;

      SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
   }
}
