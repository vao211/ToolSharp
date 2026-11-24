using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace MyWidget.Services;

public sealed class TrayIconService : IDisposable
{
   private const int WM_USER = 0x0400;
   private const int WM_TRAYCALLBACK = WM_USER + 205;

   private const int WM_LBUTTONUP = 0x0202;
   private const int WM_LBUTTONDBLCLK = 0x0203;
   private const int WM_RBUTTONUP = 0x0205;
   private const int WM_CONTEXTMENU = 0x007B;

   private const uint NIM_ADD = 0x00000000;
   private const uint NIM_MODIFY = 0x00000001;
   private const uint NIM_DELETE = 0x00000002;

   private const uint NIF_MESSAGE = 0x00000001;
   private const uint NIF_ICON = 0x00000002;
   private const uint NIF_TIP = 0x00000004;

   private static readonly IntPtr HWND_MESSAGE = new(-3);
   private static readonly IntPtr IDI_APPLICATION = new(32512);

   private HwndSource? _hwndSource;
   private IntPtr _hIcon;
   private bool _added;
   private bool _disposed;

   public ContextMenu? ContextMenu { get; set; }

   public event EventHandler? LeftClick;
   public event EventHandler? RightClickOpening;

   [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
   private struct NOTIFYICONDATAW
   {
      public uint cbSize;
      public IntPtr hWnd;
      public uint uID;
      public uint uFlags;
      public uint uCallbackMessage;
      public IntPtr hIcon;

      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
      public string szTip;

      public uint dwState;
      public uint dwStateMask;

      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
      public string szInfo;

      public uint uTimeoutOrVersion;

      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
      public string szInfoTitle;

      public uint dwInfoFlags;
      public Guid guidItem;
      public IntPtr hBalloonIcon;
   }

   [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
   private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

   [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
   private static extern IntPtr ExtractAssociatedIconW(IntPtr hInst, string lpIconPath, ref ushort lpiIcon);

   [DllImport("user32.dll")]
   private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

   [DllImport("user32.dll")]
   private static extern bool DestroyIcon(IntPtr hIcon);

   [DllImport("user32.dll")]
   private static extern bool SetForegroundWindow(IntPtr hWnd);

   public void Initialize(string tooltip = "MyWidget")
   {
      if (_added) return;

      var parameters = new HwndSourceParameters("MyWidget_TraySink")
      {
         Width = 0,
         Height = 0,
         ParentWindow = HWND_MESSAGE
      };

      _hwndSource = new HwndSource(parameters);
      _hwndSource.AddHook(WndProc);

      var exePath = Environment.ProcessPath;
      if (!string.IsNullOrWhiteSpace(exePath))
      {
         ushort iconIndex = 0;
         _hIcon = ExtractAssociatedIconW(IntPtr.Zero, exePath, ref iconIndex);
      }

      if (_hIcon == IntPtr.Zero)
         _hIcon = LoadIconW(IntPtr.Zero, IDI_APPLICATION);

      var data = CreateIconData(tooltip);
      _added = Shell_NotifyIconW(NIM_ADD, ref data);
   }

   public void UpdateTooltip(string tooltip)
   {
      if (!_added || _hwndSource is null) return;
      var data = CreateIconData(tooltip);
      Shell_NotifyIconW(NIM_MODIFY, ref data);
   }

   private NOTIFYICONDATAW CreateIconData(string tooltip)
   {
      if (tooltip.Length > 120)
         tooltip = tooltip[..120];

      return new NOTIFYICONDATAW
      {
         cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
         hWnd = _hwndSource!.Handle,
         uID = 1,
         uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
         uCallbackMessage = WM_TRAYCALLBACK,
         hIcon = _hIcon,
         szTip = tooltip,
         szInfo = string.Empty,
         szInfoTitle = string.Empty
      };
   }

   private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
   {
      if (msg == WM_TRAYCALLBACK)
      {
         var mouseMsg = (int)(lParam.ToInt64() & 0xFFFF);
         if (mouseMsg == WM_LBUTTONUP || mouseMsg == WM_LBUTTONDBLCLK)
         {
            handled = true;
            Application.Current?.Dispatcher.BeginInvoke(() => LeftClick?.Invoke(this, EventArgs.Empty));
         }
         else if (mouseMsg == WM_RBUTTONUP || mouseMsg == WM_CONTEXTMENU)
         {
            handled = true;
            Application.Current?.Dispatcher.BeginInvoke(ShowContextMenu);
         }
      }

      return IntPtr.Zero;
   }

   private void ShowContextMenu()
   {
      if (ContextMenu is null || _hwndSource is null) return;
      RightClickOpening?.Invoke(this, EventArgs.Empty);
      SetForegroundWindow(_hwndSource.Handle);
      ContextMenu.Placement = PlacementMode.MousePoint;
      ContextMenu.IsOpen = true;
   }

   public void Dispose()
   {
      if (_disposed) return;
      _disposed = true;

      if (_added && _hwndSource is not null)
      {
         var data = new NOTIFYICONDATAW
         {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwndSource.Handle,
            uID = 1
         };
         Shell_NotifyIconW(NIM_DELETE, ref data);
         _added = false;
      }

      if (_hIcon != IntPtr.Zero)
      {
         DestroyIcon(_hIcon);
         _hIcon = IntPtr.Zero;
      }

      _hwndSource?.Dispose();
      _hwndSource = null;
   }
}
