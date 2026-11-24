using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using MyWidget.Models;
using MyWidget.Services;
using MyWidget.ViewModels;

namespace MyWidget.Views;

public partial class NoteWindow : Window
{
   private const double OuterShadowMargin = 8.0;
   private const double CollapsedWindowHeight = 54.0; // 38 header + 16 shadow margin

   private bool _allowForceClose;
   private bool _isClosing;
   private bool _isClosed;
   private bool _isApplyingLayout;

   public NoteWindow(NoteViewModel viewModel)
   {
      ViewModel = viewModel;
      DataContext = viewModel;

      InitializeComponent();

      ApplyInitialBounds();

      SourceInitialized += OnSourceInitialized;
      LocationChanged += OnLocationChanged;
      SizeChanged += OnSizeChanged;
      Closed += (_, _) => _isClosed = true;

      ViewModel.PinModeChanged += OnPinModeChanged;
      ViewModel.CollapseStateChanged += OnCollapseStateChanged;
   }

   public NoteViewModel ViewModel { get; }

   private void ApplyInitialBounds()
   {
      _isApplyingLayout = true;

      Width = ViewModel.Width + OuterShadowMargin * 2;
      Height = ViewModel.IsCollapsed
         ? CollapsedWindowHeight
         : ViewModel.ExpandedHeight + OuterShadowMargin * 2;

      var workArea = SystemParameters.WorkArea;
      if (!double.IsNaN(ViewModel.Left) && !double.IsNaN(ViewModel.Top))
      {
         Left = Math.Clamp(ViewModel.Left, SystemParameters.VirtualScreenLeft - Width + 80,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80);
         Top = Math.Clamp(ViewModel.Top, SystemParameters.VirtualScreenTop - 8,
            SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 50);
      }
      else
      {
         Left = Math.Max(workArea.Left + 40, workArea.Right - Width - 40);
         Top = Math.Max(workArea.Top + 40, workArea.Top + 60);
         ViewModel.Left = Left;
         ViewModel.Top = Top;
      }

      _isApplyingLayout = false;
   }

   private void OnSourceInitialized(object? sender, EventArgs e)
   {
      var hwnd = new WindowInteropHelper(this).Handle;
      var source = HwndSource.FromHwnd(hwnd);
      source?.AddHook(WndProc);

      DesktopPinService.ApplyPinMode(this, ViewModel.PinMode);
   }

   private void OnPinModeChanged(object? sender, EventArgs e)
   {
      DesktopPinService.ApplyPinMode(this, ViewModel.PinMode);
   }

   private void OnCollapseStateChanged(object? sender, EventArgs e)
   {
      _isApplyingLayout = true;
      if (ViewModel.IsCollapsed)
      {
         if (ActualHeight > CollapsedWindowHeight + 20)
            ViewModel.ExpandedHeight = ActualHeight - OuterShadowMargin * 2;
         Height = CollapsedWindowHeight;
      }
      else
      {
         Height = ViewModel.ExpandedHeight + OuterShadowMargin * 2;
      }
      _isApplyingLayout = false;
   }

   private void OnLocationChanged(object? sender, EventArgs e)
   {
      if (_isApplyingLayout || !IsLoaded || WindowState != WindowState.Normal)
         return;

      ViewModel.Left = Left;
      ViewModel.Top = Top;
   }

   private void OnSizeChanged(object sender, SizeChangedEventArgs e)
   {
      if (_isApplyingLayout || !IsLoaded || WindowState != WindowState.Normal)
         return;

      ViewModel.Width = Math.Max(220, ActualWidth - OuterShadowMargin * 2);
      if (!ViewModel.IsCollapsed)
      {
         var contentHeight = Math.Max(140, ActualHeight - OuterShadowMargin * 2);
         ViewModel.Height = contentHeight;
         ViewModel.ExpandedHeight = contentHeight;
      }
   }

   private bool _isMovingWindow;
   private DesktopPinService.Win32Point _dragStartCursor;
   private DesktopPinService.Win32Rect _dragStartRect;

   private string? _activeResizeDir;
   private DesktopPinService.Win32Point _resizeStartCursor;
   private DesktopPinService.Win32Rect _resizeStartRect;

   private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
   {
      if (msg == DesktopPinService.WM_SYSCOMMAND)
      {
         var cmd = wParam.ToInt32() & 0xFFF0;
         if (cmd == DesktopPinService.SC_MINIMIZE && ViewModel.PinMode == NotePinMode.Desktop)
         {
            // Prevent Win+M / Minimize All from minimizing Desktop-pinned widgets
            handled = true;
            return IntPtr.Zero;
         }
      }

      return IntPtr.Zero;
   }

   public void ApplyExternalPosition(double left, double top)
   {
      _isApplyingLayout = true;
      Left = left;
      Top = top;
      _isApplyingLayout = false;
   }

   protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
   {
      _isClosing = true;
      try
      {
         if (!_allowForceClose && !ViewModel.Manager.IsShuttingDown)
         {
            // Pressing Alt+F4 closes this widget window and marks the note hidden in Manager
            ViewModel.IsOpen = false;
            ViewModel.Manager.NotifyOpenCountsChanged();
            ViewModel.Manager.RequestSave();
         }

         ViewModel.PinModeChanged -= OnPinModeChanged;
         ViewModel.CollapseStateChanged -= OnCollapseStateChanged;
         base.OnClosing(e);
      }
      finally
      {
         if (e.Cancel)
            _isClosing = false;
      }
   }

   public void ForceClose()
   {
      _allowForceClose = true;
      if (!_isClosing && !_isClosed)
         Close();
   }

   // =========================================================
   // WINDOW DRAG-MOVE (Header Bar & Bottom Toolbar)
   // =========================================================

   private void HeaderDrag_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
   {
      if (e.OriginalSource is DependencyObject dep && IsInteractiveChild(dep))
         return;

      if (TitleEditorHost.Visibility == Visibility.Visible)
         EndTitleEdit();

      if (e.ClickCount == 2 && ReferenceEquals(sender, HeaderBorder))
      {
         ViewModel.IsCollapsed = !ViewModel.IsCollapsed;
         e.Handled = true;
         return;
      }

      if (ViewModel.IsLocked || sender is not UIElement dragElement)
         return;

      if (DesktopPinService.TryGetCursorPhysical(out _dragStartCursor) &&
          DesktopPinService.TryGetWindowBoundsPhysical(this, out _dragStartRect))
      {
         _isMovingWindow = true;
         dragElement.CaptureMouse();
         e.Handled = true;
      }
   }

   private void HeaderDrag_OnMouseMove(object sender, MouseEventArgs e)
   {
      if (!_isMovingWindow || ViewModel.IsLocked || sender is not UIElement { IsMouseCaptured: true } dragElement)
         return;

      if (e.LeftButton != MouseButtonState.Pressed)
      {
         _isMovingWindow = false;
         dragElement.ReleaseMouseCapture();
         return;
      }

      if (DesktopPinService.TryGetCursorPhysical(out var cur))
      {
         var w = _dragStartRect.Right - _dragStartRect.Left;
         var h = _dragStartRect.Bottom - _dragStartRect.Top;
         var newX = _dragStartRect.Left + (cur.X - _dragStartCursor.X);
         var newY = _dragStartRect.Top + (cur.Y - _dragStartCursor.Y);
         DesktopPinService.SetWindowBoundsPhysical(this, newX, newY, w, h);
      }
   }

   private void HeaderDrag_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
   {
      if (!_isMovingWindow) return;
      _isMovingWindow = false;
      if (sender is UIElement { IsMouseCaptured: true } dragElement)
         dragElement.ReleaseMouseCapture();
      e.Handled = true;
   }

   private void HeaderDrag_OnLostMouseCapture(object sender, MouseEventArgs e)
   {
      _isMovingWindow = false;
   }

   private static bool IsInteractiveChild(DependencyObject source)
   {
      var current = source;
      while (current is not null)
      {
         if (current is Button or System.Windows.Controls.Primitives.ToggleButton or TextBox)
            return true;
         current = System.Windows.Media.VisualTreeHelper.GetParent(current);
      }
      return false;
   }

   // =========================================================
   // 8-DIRECTION EDGE & CORNER RESIZE
   // =========================================================

   private void ResizeHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
   {
      if (ViewModel.IsLocked || sender is not Border { Tag: string dir } handle)
         return;

      if (DesktopPinService.TryGetCursorPhysical(out _resizeStartCursor) &&
          DesktopPinService.TryGetWindowBoundsPhysical(this, out _resizeStartRect))
      {
         _activeResizeDir = dir;
         handle.CaptureMouse();
         e.Handled = true;
      }
   }

   private void ResizeHandle_OnMouseMove(object sender, MouseEventArgs e)
   {
      if (_activeResizeDir is null || ViewModel.IsLocked || sender is not Border { IsMouseCaptured: true } handle)
         return;

      if (e.LeftButton != MouseButtonState.Pressed)
      {
         _activeResizeDir = null;
         handle.ReleaseMouseCapture();
         return;
      }

      if (!DesktopPinService.TryGetCursorPhysical(out var cur))
         return;

      var dx = cur.X - _resizeStartCursor.X;
      var dy = cur.Y - _resizeStartCursor.Y;

      var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
      var minW = (int)Math.Round(MinWidth * dpi.DpiScaleX);
      var minH = (int)Math.Round((ViewModel.IsCollapsed ? CollapsedWindowHeight : 160.0) * dpi.DpiScaleY);

      var left = _resizeStartRect.Left;
      var top = _resizeStartRect.Top;
      var right = _resizeStartRect.Right;
      var bottom = _resizeStartRect.Bottom;

      if (_activeResizeDir.Contains('L'))
         left = Math.Min(right - minW, _resizeStartRect.Left + dx);
      else if (_activeResizeDir.Contains('R'))
         right = Math.Max(left + minW, _resizeStartRect.Right + dx);

      if (!ViewModel.IsCollapsed)
      {
         if (_activeResizeDir.Contains('T'))
            top = Math.Min(bottom - minH, _resizeStartRect.Top + dy);
         else if (_activeResizeDir.Contains('B'))
            bottom = Math.Max(top + minH, _resizeStartRect.Bottom + dy);
      }

      DesktopPinService.SetWindowBoundsPhysical(this, left, top, right - left, bottom - top);
   }

   private void ResizeHandle_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
   {
      if (_activeResizeDir is null) return;
      _activeResizeDir = null;
      if (sender is Border { IsMouseCaptured: true } handle)
         handle.ReleaseMouseCapture();
      e.Handled = true;
   }

   private void ResizeHandle_OnLostMouseCapture(object sender, MouseEventArgs e)
   {
      _activeResizeDir = null;
   }

   // =========================================================
   // INLINE TITLE RENAME
   // =========================================================

   private void EditTitleBtn_OnClick(object sender, RoutedEventArgs e)
   {
      if (ViewModel.IsLocked) return;
      TitleDisplayPanel.Visibility = Visibility.Collapsed;
      TitleEditorHost.Visibility = Visibility.Visible;
      Dispatcher.BeginInvoke(() =>
      {
         TitleTextBox.Focus();
         TitleTextBox.SelectAll();
      });
   }

   private void TitleTextBox_OnKeyDown(object sender, KeyEventArgs e)
   {
      if (e.Key is Key.Enter or Key.Escape)
      {
         EndTitleEdit();
         e.Handled = true;
      }
   }

   private void TitleTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
   {
      EndTitleEdit();
   }

   private void EndTitleEdit()
   {
      TitleEditorHost.Visibility = Visibility.Collapsed;
      TitleDisplayPanel.Visibility = Visibility.Visible;
   }

   private void MoreBtn_OnClick(object sender, RoutedEventArgs e)
   {
      if (MoreBtn.ContextMenu is not null)
      {
         MoreBtn.ContextMenu.PlacementTarget = MoreBtn;
         MoreBtn.ContextMenu.DataContext = ViewModel;
         MoreBtn.ContextMenu.IsOpen = true;
      }
   }

   private void NoteContextMenu_OnOpened(object sender, RoutedEventArgs e)
   {
      if (sender is ContextMenu cm)
         cm.DataContext = ViewModel;
   }

   private void BlockTextBox_OnLoaded(object sender, RoutedEventArgs e)
   {
      if (sender is TextBox tb && tb.DataContext is NoteBlockViewModel blockVm && blockVm.ShouldFocusOnLoad)
      {
         blockVm.ShouldFocusOnLoad = false;
         Dispatcher.BeginInvoke(() =>
         {
            tb.Focus();
            tb.CaretIndex = tb.Text.Length;
         });
      }
   }

   private void BlockTextBox_OnGotFocus(object sender, RoutedEventArgs e)
   {
      if (sender is TextBox { DataContext: NoteBlockViewModel blockVm })
      {
         ViewModel.ActiveBlock = blockVm;
      }
   }

   private void BlockTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
   {
      if (ViewModel.IsLocked) return;
      if (sender is not TextBox tb || tb.DataContext is not NoteBlockViewModel blockVm)
         return;

      var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
      var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

      if (ctrl && e.Key == Key.V)
      {
         if (TryPasteClipboardImageOrFiles())
         {
            e.Handled = true;
            return;
         }
      }
      else if (ctrl && e.Key == Key.B)
      {
         blockVm.IsBold = !blockVm.IsBold;
         e.Handled = true;
         return;
      }
      else if (ctrl && e.Key == Key.I)
      {
         blockVm.IsItalic = !blockVm.IsItalic;
         e.Handled = true;
         return;
      }
      else if (ctrl && e.Key == Key.U)
      {
         blockVm.IsUnderline = !blockVm.IsUnderline;
         e.Handled = true;
         return;
      }

      if (blockVm.IsCheckBoxType)
      {
         if (e.Key == Key.Enter && !shift)
         {
            ViewModel.AddBlock(NoteBlockType.CheckBox, after: blockVm, focus: true);
            e.Handled = true;
         }
         else if (e.Key == Key.Back && string.IsNullOrEmpty(tb.Text) && ViewModel.Blocks.Count > 1)
         {
            ViewModel.RemoveBlock(blockVm);
            e.Handled = true;
         }
      }
   }

   private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
   {
      if (ViewModel.IsLocked) return;

      if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
      {
         if (Keyboard.FocusedElement is not TextBox)
         {
            if (TryPasteClipboardImageOrFiles())
               e.Handled = true;
         }
      }
      else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.N)
      {
         ViewModel.Manager.CreateNewNote(nearNote: ViewModel);
         e.Handled = true;
      }
   }

   private bool TryPasteClipboardImageOrFiles()
   {
      try
      {
         if (Clipboard.ContainsImage())
         {
            var bmp = Clipboard.GetImage();
            if (bmp is not null)
            {
               ViewModel.AddImageFromClipboardBitmap(bmp);
               return true;
            }
         }

         if (Clipboard.ContainsFileDropList())
         {
            var files = Clipboard.GetFileDropList();
            var addedAny = false;
            foreach (var file in files)
            {
               if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) continue;
               var ext = Path.GetExtension(file).ToLowerInvariant();
               if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".tiff")
               {
                  ViewModel.AddImageFromFile(file);
                  addedAny = true;
               }
            }
            return addedAny;
         }
      }
      catch
      {
      }

      return false;
   }

   private void NoteArea_OnDragOver(object sender, DragEventArgs e)
   {
      if (!ViewModel.IsLocked && e.Data.GetDataPresent(DataFormats.FileDrop))
      {
         e.Effects = DragDropEffects.Copy;
         e.Handled = true;
      }
      else
      {
         e.Effects = DragDropEffects.None;
         e.Handled = true;
      }
   }

   private void NoteArea_OnDrop(object sender, DragEventArgs e)
   {
      if (ViewModel.IsLocked || !e.Data.GetDataPresent(DataFormats.FileDrop))
         return;

      if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
      {
         foreach (var file in files)
            ViewModel.AddImageFromFile(file);
      }
   }

   private void NoteImage_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
   {
      if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: NoteBlockViewModel blockVm })
      {
         OpenImagePreview(blockVm);
      }
   }

   private void ZoomImageBtn_OnClick(object sender, RoutedEventArgs e)
   {
      if (sender is FrameworkElement { DataContext: NoteBlockViewModel blockVm })
      {
         OpenImagePreview(blockVm);
      }
   }

   private void OpenImagePreview(NoteBlockViewModel blockVm)
   {
      var full = blockVm.FullImagePath;
      if (string.IsNullOrWhiteSpace(full) || !File.Exists(full)) return;
      var preview = new ImagePreviewWindow(full);
      preview.Show();
   }
}
