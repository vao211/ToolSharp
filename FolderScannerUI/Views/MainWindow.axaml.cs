using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FolderScannerUI.Models;
using FolderScannerUI.ViewModels;

namespace FolderScannerUI.Views;

public partial class MainWindow : Window
{
   public MainWindow()
   {
      InitializeComponent();
   }

   private async void PickFolder_Click(object? sender, RoutedEventArgs e)
   {
      if (DataContext is not MainWindowViewModel vm) return;

      var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
      {
         Title = vm.Loc["DialogSelectFolderTitle"],
         AllowMultiple = false
      });

      var path = folders.FirstOrDefault()?.TryGetLocalPath();
      if (!string.IsNullOrWhiteSpace(path))
         vm.TargetFolder = path;
   }

   private async void ExportCsv_Click(object? sender, RoutedEventArgs e)
      => await ExportWithPickerAsync(ExportFormat.Csv, "CSV (*.csv)", "*.csv");

   private async void ExportTxt_Click(object? sender, RoutedEventArgs e)
      => await ExportWithPickerAsync(ExportFormat.Txt, "Text (*.txt)", "*.txt");

   private async void ExportJson_Click(object? sender, RoutedEventArgs e)
      => await ExportWithPickerAsync(ExportFormat.Json, "JSON (*.json)", "*.json");

   private async Task ExportWithPickerAsync(ExportFormat format, string filterName, string pattern)
   {
      if (DataContext is not MainWindowViewModel vm) return;

      var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
      {
         Title = vm.Loc["DialogSaveExportTitle"],
         SuggestedFileName = vm.GetDefaultExportFileName(format),
         FileTypeChoices =
         [
            new FilePickerFileType(filterName) { Patterns = [pattern] }
         ]
      });

      var path = file?.TryGetLocalPath();
      if (!string.IsNullOrWhiteSpace(path))
         vm.ExportResultsToFile(path, format);
   }

   private async void CopyRowPath_Click(object? sender, RoutedEventArgs e)
   {
      if (sender is not Control { DataContext: FileSystemItemInfo item }) return;
      if (DataContext is not MainWindowViewModel vm) return;

      var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
      if (clipboard is not null)
      {
         await clipboard.SetValueAsync(DataFormat.Text, item.FullPath);
         vm.NotifyCopiedPath(item.FullPath);
      }
   }

   private async void ResultsList_DoubleTapped(object? sender, TappedEventArgs e)
   {
      if (DataContext is not MainWindowViewModel vm) return;
      if (vm.SelectedItem is { IsDirectory: true } item)
         await vm.DrillIntoFolderCommand.ExecuteAsync(item);
   }
}
