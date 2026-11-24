using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace TempClear;

public class MainViewModel : ViewModelBase
{
   private readonly AppSettings _settings;
   private bool _isBusy;
   private bool _isDarkMode;

   private double _progressValue;
   private string _statusMessage;

   public MainViewModel()
   {
      _settings = AppSettings.Load();
      _isDarkMode = _settings.IsDarkMode;

      Folders = new ObservableCollection<FolderItem>();

      SelectAllCommand = new RelayCommand(_ => SetSelection(true));
      DeselectAllCommand = new RelayCommand(_ => SetSelection(false));

      DeleteCommand = new RelayCommand(async _ => await DeleteSelectedFolders(), _ => !IsBusy);

      ReloadCommand = new RelayCommand(async _ => await LoadTempFolders(), _ => !IsBusy);

      LoadTempFolders();
   }

   public ObservableCollection<FolderItem> Folders { get; set; }

   public bool IsDarkMode
   {
      get => _isDarkMode;
      set
      {
         if (_isDarkMode == value) return;
         _isDarkMode = value;
         OnPropertyChanged();
         ThemeManager.ApplyTheme(value);
         _settings.IsDarkMode = value;
         _settings.Save();
      }
   }

   public double ProgressValue
   {
      get => _progressValue;
      set
      {
         _progressValue = value;
         OnPropertyChanged();
      }
   }

   public string StatusMessage
   {
      get => _statusMessage;
      set
      {
         _statusMessage = value;
         OnPropertyChanged();
      }
   }

   public bool IsBusy
   {
      get => _isBusy;
      set
      {
         _isBusy = value;
         OnPropertyChanged();
      }
   }

   public ICommand SelectAllCommand { get; }
   public ICommand DeselectAllCommand { get; }
   public ICommand DeleteCommand { get; }
   public ICommand ReloadCommand { get; }

   private async Task LoadTempFolders()
   {
      IsBusy = true;
      Folders.Clear();
      var tempPath = Path.GetTempPath();
      StatusMessage = "Đang quét và tính dung lượng...";

      await Task.Run(() =>
      {
         try
         {
            var directories = Directory.GetDirectories(tempPath);
            var tempList = new List<FolderItem>();

            foreach (var dir in directories)
            {
               var info = new DirectoryInfo(dir);

               var size = FileUtils.GetDirectorySize(info.FullName);

               tempList.Add(new FolderItem
               {
                  Name = info.Name,
                  FullPath = info.FullName,
                  CreatedDate = info.CreationTime.ToString("dd/MM/yyyy HH:mm"),
                  SizeInBytes = size
               });
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
               foreach (var item in tempList) Folders.Add(item);
               StatusMessage = $"Đã tìm thấy {Folders.Count} thư mục.";
            });
         }
         catch (Exception ex)
         {
            StatusMessage = "Lỗi khi quét: " + ex.Message;
         }
      });

      IsBusy = false;
   }

   private void SetSelection(bool isSelected)
   {
      foreach (var item in Folders) item.IsSelected = isSelected;
   }

   private async Task DeleteSelectedFolders()
   {
      var itemsToDelete = Folders.Where(x => x.IsSelected).ToList();
      if (itemsToDelete.Count == 0)
      {
         MessageBox.Show("Vui lòng chọn ít nhất 1 thư mục để xóa.");
         return;
      }

      IsBusy = true;
      ProgressValue = 0;
      var step = 100.0 / itemsToDelete.Count;

      var deletedCount = 0;
      var skippedCount = 0;
      long totalBytesFreed = 0;

      await Task.Run(() =>
      {
         foreach (var item in itemsToDelete)
         {
            try
            {
               StatusMessage = $"Đang xóa: {item.Name}";

               Directory.Delete(item.FullPath, true);

               totalBytesFreed += item.SizeInBytes;

               Application.Current.Dispatcher.Invoke(() => Folders.Remove(item));
               deletedCount++;
            }
            catch (Exception)
            {
               skippedCount++;
            }

            ProgressValue += step;
         }
      });

      // Format thông báo cuối cùng
      var sizeFreedString = FileUtils.FormatBytes(totalBytesFreed);
      var finalMsg =
         $"Hoàn tất!\nXóa: {deletedCount}\nBỏ qua: {skippedCount}\nTổng dung lượng giải phóng: {sizeFreedString}";

      StatusMessage = $"Đã dọn dẹp {sizeFreedString}";
      IsBusy = false;
      ProgressValue = 100;

      MessageBox.Show(finalMsg, "Kết quả dọn dẹp", MessageBoxButton.OK, MessageBoxImage.Information);

      await LoadTempFolders();
   }
}