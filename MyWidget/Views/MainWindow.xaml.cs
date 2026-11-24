using System.Windows;
using System.Windows.Input;
using MyWidget.Services;
using MyWidget.ViewModels;

namespace MyWidget.Views;

public partial class MainWindow : Window
{
   private bool _allowForceClose;
   private bool _isClosing;
   private bool _isClosed;

   public MainWindow(MainViewModel viewModel)
   {
      ViewModel = viewModel;
      DataContext = viewModel;

      InitializeComponent();

      SourceInitialized += (_, _) => ThemeService.ApplyTitleBarTheme(this, ThemeService.IsDarkMode);
      Closed += (_, _) => _isClosed = true;
   }

   public MainViewModel ViewModel { get; }

   public void ForceClose()
   {
      _allowForceClose = true;
      if (!_isClosing && !_isClosed)
         Close();
   }

   protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
   {
      _isClosing = true;
      try
      {
         if (!_allowForceClose && !ViewModel.Manager.IsShuttingDown)
         {
            if (ViewModel.Settings.HideToTrayOnClose)
            {
               e.Cancel = true;
               Hide();
               return;
            }

            ViewModel.Manager.ExitApplication();
         }

         base.OnClosing(e);
      }
      finally
      {
         if (e.Cancel)
            _isClosing = false;
      }
   }

   private Point? _cardDragStart;

   private void NoteCard_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
   {
      if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: NoteViewModel noteVm })
      {
         _cardDragStart = null;
         ViewModel.Manager.ShowNoteWindow(noteVm, activate: true);
         e.Handled = true;
         return;
      }

      _cardDragStart = e.GetPosition(this);
   }

   private void NoteCard_OnPreviewMouseMove(object sender, MouseEventArgs e)
   {
      if (_cardDragStart is null || e.LeftButton != MouseButtonState.Pressed)
         return;

      if (e.OriginalSource is DependencyObject dep && IsCardButtonChild(dep))
         return;

      var pos = e.GetPosition(this);
      if (Math.Abs(pos.X - _cardDragStart.Value.X) > 8 || Math.Abs(pos.Y - _cardDragStart.Value.Y) > 8)
      {
         _cardDragStart = null;
         if (sender is FrameworkElement { DataContext: NoteViewModel noteVm })
         {
            var data = new DataObject(typeof(NoteViewModel), noteVm);
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
         }
      }
   }

   private void NoteCard_OnDragOver(object sender, DragEventArgs e)
   {
      e.Effects = e.Data.GetDataPresent(typeof(NoteViewModel))
         ? DragDropEffects.Move
         : DragDropEffects.None;
      e.Handled = true;
   }

   private void NoteCard_OnDrop(object sender, DragEventArgs e)
   {
      if (e.Data.GetData(typeof(NoteViewModel)) is NoteViewModel sourceNote &&
          sender is FrameworkElement { DataContext: NoteViewModel targetNote })
      {
         ViewModel.Manager.MoveNoteOrder(sourceNote, targetNote);
         e.Handled = true;
      }
   }

   private void NoteCardContextMenu_OnOpened(object sender, RoutedEventArgs e)
   {
      if (sender is System.Windows.Controls.ContextMenu { PlacementTarget: FrameworkElement { DataContext: NoteViewModel noteVm } } cm)
      {
         cm.DataContext = noteVm;
      }
   }

   private static bool IsCardButtonChild(DependencyObject source)
   {
      var current = source;
      while (current is not null)
      {
         if (current is System.Windows.Controls.Button)
            return true;
         current = System.Windows.Media.VisualTreeHelper.GetParent(current);
      }
      return false;
   }
}
