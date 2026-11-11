using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization; // THÊM THƯ VIỆN NÀY

namespace MultiTaskClicker.Models;

public partial class TaskItem : ObservableObject
{
    [ObservableProperty] private int _taskId;
    
    [ObservableProperty] private int _x = 0;
    [ObservableProperty] private int _y = 0;
    [ObservableProperty] private int _clicks = 1;
    [ObservableProperty] private double _interval = 1.0;
    [ObservableProperty] private bool _isGhost;

    [ObservableProperty] 
    [property: JsonIgnore] 
    private string _btnGetPosText = "Lấy tọa độ (3s)";
    
    [ObservableProperty] 
    [property: JsonIgnore] 
    private bool _isBtnGetPosEnabled = true;
    
    [ObservableProperty] 
    [property: JsonIgnore] 
    private bool _isRunning;
}