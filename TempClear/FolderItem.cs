namespace TempClear;

public class FolderItem : ViewModelBase
{
    private bool _isSelected;
        
    public string Name { get; set; }
    public string FullPath { get; set; }
    public string CreatedDate { get; set; }
    
    public string Path { get; set; }
    
    public long SizeInBytes { get; set; }
    
    public string DisplaySize => FileUtils.FormatBytes(SizeInBytes);
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public FolderItem()
    {
        IsSelected = false; 
    }
}