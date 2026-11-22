using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

// THÊM THƯ VIỆN NÀY

namespace MultiTaskClicker.Models;

public partial class TaskItem : ObservableObject
{
   [ObservableProperty] [property: JsonIgnore]
   private string _btnGetPosText = "Lấy tọa độ (3s)";

   [ObservableProperty] private int _clicks = 1;
   [ObservableProperty] private double _interval = 1.0;

   [ObservableProperty] [property: JsonIgnore]
   private bool _isBtnGetPosEnabled = true;

   [ObservableProperty] private bool _isGhost;

   [ObservableProperty] [property: JsonIgnore]
   private bool _isRunning;

   [ObservableProperty] private int _taskId;

   [ObservableProperty] private int _x;
   [ObservableProperty] private int _y;
}