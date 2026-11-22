namespace TempClear;

public class FolderItem : ViewModelBase
{
   private bool _isSelected;

   public FolderItem()
   {
      IsSelected = false;
   }

   public string Name { get; set; }
   public string FullPath { get; set; }
   public string CreatedDate { get; set; }

   public string Path { get; set; }

   public long SizeInBytes { get; set; }

   public string DisplaySize => FileUtils.FormatBytes(SizeInBytes);

   public bool IsSelected
   {
      get => _isSelected;
      set
      {
         _isSelected = value;
         OnPropertyChanged();
      }
   }
}