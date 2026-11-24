using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MyWidget.Services;

namespace MyWidget.Views;

public partial class ConfirmDialogWindow : Window
{
   public ConfirmDialogWindow(
      string title,
      string message,
      string? confirmText = null,
      bool isDanger = true)
   {
      TitleText = title;
      DataContext = this;

      InitializeComponent();

      DialogTitleText.Text = title;
      DialogMessageText.Text = message;
      ConfirmButton.Content = confirmText ?? LocalizationService.Instance["BtnConfirmDelete"];

      if (isDanger)
      {
         IconGlyph.Text = "🗑";
         ConfirmButton.SetResourceReference(BackgroundProperty, "DangerBrush");
         ConfirmButton.Foreground = Brushes.White;
      }
      else
      {
         IconGlyph.Text = "❓";
         IconBadge.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "AccentSoftBrush");
         ConfirmButton.SetResourceReference(BackgroundProperty, "AccentBrush");
         ConfirmButton.SetResourceReference(ForegroundProperty, "AccentForegroundBrush");
      }

      Loaded += (_, _) => ConfirmButton.Focus();
   }

   public string TitleText { get; }

   public static bool ShowConfirm(
      string title,
      string message,
      string? confirmText = null,
      bool isDanger = true,
      Window? owner = null)
   {
      var dlg = new ConfirmDialogWindow(title, message, confirmText, isDanger);
      if (owner is MainWindow { IsVisible: true, WindowState: not WindowState.Minimized })
      {
         dlg.Owner = owner;
         dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
      }
      else if (owner is { IsVisible: true } && !double.IsNaN(owner.Left) && !double.IsNaN(owner.Top))
      {
         dlg.WindowStartupLocation = WindowStartupLocation.Manual;
         var work = SystemParameters.WorkArea;
         var targetLeft = owner.Left + (owner.ActualWidth - dlg.Width) / 2;
         var targetTop = owner.Top + Math.Min(80, owner.ActualHeight * 0.25);
         dlg.Left = Math.Clamp(targetLeft, work.Left + 12, Math.Max(work.Left + 12, work.Right - dlg.Width - 12));
         dlg.Top = Math.Clamp(targetTop, work.Top + 12, Math.Max(work.Top + 12, work.Bottom - 220));
      }

      return dlg.ShowDialog() == true;
   }

   private void RootBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
   {
      if (e.ButtonState == MouseButtonState.Pressed)
      {
         try
         {
            DragMove();
         }
         catch
         {
         }
      }
   }

   private void ConfirmBtn_OnClick(object sender, RoutedEventArgs e)
   {
      DialogResult = true;
      Close();
   }

   private void CancelBtn_OnClick(object sender, RoutedEventArgs e)
   {
      DialogResult = false;
      Close();
   }

   private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
   {
      if (e.Key == Key.Escape)
      {
         DialogResult = false;
         Close();
         e.Handled = true;
      }
      else if (e.Key == Key.Enter)
      {
         DialogResult = true;
         Close();
         e.Handled = true;
      }
   }
}
