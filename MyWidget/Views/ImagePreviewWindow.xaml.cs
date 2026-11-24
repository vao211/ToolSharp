using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using MyWidget.Services;

namespace MyWidget.Views;

public partial class ImagePreviewWindow : Window
{
   private readonly string _fullPath;
   private double _zoom = 1.0;

   public ImagePreviewWindow(string fullPath)
   {
      _fullPath = fullPath;
      InitializeComponent();

      SourceInitialized += (_, _) => ThemeService.ApplyTitleBarTheme(this, ThemeService.IsDarkMode);

      FileNameText.Text = Path.GetFileName(fullPath);
      PreviewImg.Source = DataStorageService.Instance.LoadBitmapSafe(fullPath);
   }

   private void ApplyZoom(double value)
   {
      _zoom = Math.Clamp(value, 0.2, 5.0);
      ImgScale.ScaleX = _zoom;
      ImgScale.ScaleY = _zoom;
      ZoomLabel.Text = $"{(int)Math.Round(_zoom * 100)}%";
   }

   private void ZoomOut_OnClick(object sender, RoutedEventArgs e) => ApplyZoom(_zoom - 0.2);

   private void ZoomIn_OnClick(object sender, RoutedEventArgs e) => ApplyZoom(_zoom + 0.2);

   private void ResetZoom_OnClick(object sender, RoutedEventArgs e) => ApplyZoom(1.0);

   private void ScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
   {
      if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
      {
         e.Handled = true;
         ApplyZoom(_zoom + (e.Delta > 0 ? 0.15 : -0.15));
      }
   }

   private void OpenExternal_OnClick(object sender, RoutedEventArgs e)
   {
      if (!File.Exists(_fullPath)) return;
      try
      {
         Process.Start(new ProcessStartInfo
         {
            FileName = _fullPath,
            UseShellExecute = true
         });
      }
      catch
      {
      }
   }

   private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
   {
      if (e.Key == Key.Escape)
         Close();
   }
}
