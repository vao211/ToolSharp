using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CodePacker.ViewModels;

namespace CodePacker.Views;

public partial class MainWindow : Window
{
   public MainWindow() => InitializeComponent();

   private async void PickSourceFolder_Click(object? sender, RoutedEventArgs e)
   {
      var path = await PickFolderAsync();
      if (path is not null && DataContext is MainWindowViewModel vm)
         vm.SourceFolder = path;
   }

   private async void PickOutputFolder_Click(object? sender, RoutedEventArgs e)
   {
      var path = await PickFolderAsync();
      if (path is not null && DataContext is MainWindowViewModel vm)
         vm.OutputFolder = path;
   }

   private async Task<string?> PickFolderAsync()
   {
      var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
      {
         AllowMultiple = false
      });
      return folders.FirstOrDefault()?.TryGetLocalPath();
   }
   
}