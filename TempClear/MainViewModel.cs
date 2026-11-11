using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace TempClear
{
    public class MainViewModel : ViewModelBase
    {
        public ObservableCollection<FolderItem> Folders { get; set; }
        
        private double _progressValue;
        private string _statusMessage;
        private bool _isBusy;

        public double ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); } 
        }
        
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ReloadCommand { get; }

        public MainViewModel()
        {
            Folders = new ObservableCollection<FolderItem>();
            
            SelectAllCommand = new RelayCommand(_ => SetSelection(true));
            DeselectAllCommand = new RelayCommand(_ => SetSelection(false));
            
            DeleteCommand = new RelayCommand(async _ => await DeleteSelectedFolders(), _ => !IsBusy);
            
            ReloadCommand = new RelayCommand(async _ => await LoadTempFolders(), _ => !IsBusy);
            
            LoadTempFolders(); 
        }
        
        private async Task LoadTempFolders()
        {
            IsBusy = true;
            Folders.Clear();
            string tempPath = Path.GetTempPath();
            StatusMessage = "Đang quét và tính dung lượng...";

            await Task.Run(() =>
            {
                try
                {
                    var directories = Directory.GetDirectories(tempPath);
                    var tempList = new List<FolderItem>();

                    foreach (var dir in directories)
                    {
                        DirectoryInfo info = new DirectoryInfo(dir);
                        
                        long size = FileUtils.GetDirectorySize(info.FullName);

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
            double step = 100.0 / itemsToDelete.Count;
            
            int deletedCount = 0;
            int skippedCount = 0;
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
            string sizeFreedString = FileUtils.FormatBytes(totalBytesFreed);
            string finalMsg = $"Hoàn tất!\nXóa: {deletedCount}\nBỏ qua: {skippedCount}\nTổng dung lượng giải phóng: {sizeFreedString}";
            
            StatusMessage = $"Đã dọn dẹp {sizeFreedString}";
            IsBusy = false;
            ProgressValue = 100;

            MessageBox.Show(finalMsg, "Kết quả dọn dẹp", MessageBoxButton.OK, MessageBoxImage.Information);
            
            await LoadTempFolders();
        }
    }
}