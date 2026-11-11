using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiTaskClicker.Models;
using SharpHook;
using SharpHook.Data;

namespace MultiTaskClicker.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly EventSimulator _simulator = new();
    private SimpleGlobalHook? _globalHook;

    private volatile bool _isSimulating;

    private readonly ConcurrentDictionary<TaskItem, CancellationTokenSource> _taskCtsDict = new();

    private short _currentMouseX;
    private short _currentMouseY;

    private bool _isAltPressed;

    [ObservableProperty] private string? _numTasksInput = "1";
    [ObservableProperty] private bool _isGlobalRunning;

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    public MainWindowViewModel()
    {
        GenerateTasks();
        StartGlobalHook();
    }

    [RelayCommand]
    private void GenerateTasks()
    {
        if (!int.TryParse(NumTasksInput, out var num) || num <= 0) return;

        StopAllTasks(); // Dừng tất cả luồng cũ trước khi xóa danh sách

        Tasks.Clear();
        for (var i = 1; i <= num; i++) Tasks.Add(new TaskItem { TaskId = i });
        NumTasksInput = Tasks.Count.ToString();
    }

    [RelayCommand]
    private void AddTask()
    {
        var nextId = Tasks.Count + 1;
        Tasks.Add(new TaskItem { TaskId = nextId });
        NumTasksInput = Tasks.Count.ToString();
    }

    private void StartGlobalHook()
    {
        _globalHook = new SimpleGlobalHook();
        _globalHook.KeyPressed += (s, e) =>
        {
            if (e.Data.KeyCode == KeyCode.VcLeftAlt || e.Data.KeyCode == KeyCode.VcRightAlt) _isAltPressed = true;

            if (_isAltPressed && e.Data.KeyCode == KeyCode.VcS) Dispatcher.UIThread.Post(StartClicking);
            if (e.Data.KeyCode == KeyCode.VcQ) Dispatcher.UIThread.Post(StopAllTasks);
        };
        _globalHook.KeyReleased += (s, e) =>
        {
            if (e.Data.KeyCode == KeyCode.VcLeftAlt || e.Data.KeyCode == KeyCode.VcRightAlt) _isAltPressed = false;
        };
        _globalHook.MouseMoved += (s, e) =>
        {
            if (_isSimulating) return;
            _currentMouseX = e.Data.X;
            _currentMouseY = e.Data.Y;
        };
        Task.Run(() => _globalHook.Run());
    }

    [RelayCommand]
    private async Task GetPositionAsync(TaskItem task)
    {
        task.IsBtnGetPosEnabled = false;
        task.BtnGetPosText = "Đang chờ...";
        await Task.Delay(3000);

        task.X = _currentMouseX;
        task.Y = _currentMouseY;

        task.BtnGetPosText = "Lấy tọa độ (3s)";
        task.IsBtnGetPosEnabled = true;
    }

    [RelayCommand]
    private void StartClicking()
    {
        if (Tasks.Count == 0) return;

        IsGlobalRunning = true;

        foreach (var task in Tasks)
        {
            if (task.IsRunning) continue;

            var cts = new CancellationTokenSource();
            if (_taskCtsDict.TryAdd(task, cts)) Task.Run(() => ClickLoop(task, cts.Token));
        }
    }

    [RelayCommand]
    private void StopAllTasks()
    {
        foreach (var kvp in _taskCtsDict) kvp.Value.Cancel();
        _taskCtsDict.Clear();
        IsGlobalRunning = false;
    }


    [RelayCommand]
    private void StartTask(TaskItem task)
    {
        if (task.IsRunning) return;

        var cts = new CancellationTokenSource();

        if (_taskCtsDict.TryAdd(task, cts))
        {
            IsGlobalRunning = true;
            Task.Run(() => ClickLoop(task, cts.Token));
        }
    }

    [RelayCommand]
    private void StopTask(TaskItem task)
    {
        if (_taskCtsDict.TryRemove(task, out var cts)) cts.Cancel();
        task.IsRunning = false;
        CheckGlobalStatus();
    }

    [RelayCommand]
    private void DeleteTask(TaskItem task)
    {
        StopTask(task);
        Tasks.Remove(task);

        for (var i = 0; i < Tasks.Count; i++) Tasks[i].TaskId = i + 1;
        NumTasksInput = Tasks.Count.ToString();
    }

    [RelayCommand]
    private void ResetTask(TaskItem task)
    {
        StopTask(task);

        task.X = 0;
        task.Y = 0;
        task.Clicks = 1;
        task.Interval = 1.0;
        task.IsGhost = false;
    }

    private void CheckGlobalStatus()
    {
        foreach (var t in Tasks)
            if (t.IsRunning)
                return;
        IsGlobalRunning = false;
    }

    private async Task ClickLoop(TaskItem task, CancellationToken token)
    {
        // Đảm bảo đã cập nhật trạng thái UI là đang chạy
        task.IsRunning = true;
        var delayBetweenClicks = task.Interval * 1000 / task.Clicks;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var startTime = DateTime.Now;

                for (var i = 0; i < task.Clicks; i++)
                {
                    if (token.IsCancellationRequested) break;

                    // 1. XỬ LÝ CLICK (GHOST HOẶC NORMAL)
                    if (task.IsGhost)
                    {
                        // Lưu vị trí chuột thực tế hiện tại trước khi nhảy
                        var tempX = _currentMouseX;
                        var tempY = _currentMouseY;

                        _isSimulating = true; // KHÓA HOOK: Ngăn hệ thống tự ghi đè tọa độ

                        _simulator.SimulateMouseMovement((short)task.X, (short)task.Y);
                        _simulator.SimulateMousePress(MouseButton.Button1);
                        _simulator.SimulateMouseRelease(MouseButton.Button1);

                        _simulator.SimulateMouseMovement(tempX, tempY); // Trả chuột về ngay lập tức

                        // Chờ siêu ngắn (0.2ms) để OS hoàn tất việc đẩy sự kiện di chuyển vào hàng đợi
                        var snapSw = Stopwatch.StartNew();
                        while (snapSw.Elapsed.TotalMilliseconds < 0.2)
                        {
                        }

                        _isSimulating = false; // MỞ KHÓA HOOK
                    }
                    else
                    {
                        // Click bình thường, không giật chuột về
                        _simulator.SimulateMouseMovement((short)task.X, (short)task.Y);
                        _simulator.SimulateMousePress(MouseButton.Button1);
                        _simulator.SimulateMouseRelease(MouseButton.Button1);
                    }

                    // 2. XỬ LÝ DELAY SIÊU CHÍNH XÁC
                    if (delayBetweenClicks >= 16)
                    {
                        // Chờ bằng nhịp OS bình thường cho nhẹ CPU
                        await Task.Delay((int)delayBetweenClicks, token);
                    }
                    else if (delayBetweenClicks > 0)
                    {
                        // Vòng lặp bận (Busy Wait) cho tốc độ mili-giây
                        var sw = Stopwatch.StartNew();
                        while (sw.Elapsed.TotalMilliseconds < delayBetweenClicks)
                            if (token.IsCancellationRequested)
                                break;
                    }
                }

                // 3. BÙ TRỪ THỜI GIAN SAI SỐ CUỐI CHU KỲ
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                var remainingDelay = task.Interval * 1000 - elapsed;
                if (remainingDelay > 0) await Task.Delay((int)remainingDelay, token).ContinueWith(_ => { });
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // Khi bị dừng (qua nút Stop hoặc phím Q), trả UI về trạng thái tĩnh
            task.IsRunning = false;
            _taskCtsDict.TryRemove(task, out _);
            CheckGlobalStatus();
        }
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        // Lấy quyền truy cập Window hiện tại để mở hộp thoại
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow == null) return;

        var storage = desktop.MainWindow.StorageProvider;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Lưu cấu hình Task",
            DefaultExtension = "json",
            FileTypeChoices = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } }
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            var options = new JsonSerializerOptions { WriteIndented = true }; // Lưu JSON có format thụt lề cho dễ đọc
            await JsonSerializer.SerializeAsync(stream, Tasks, options);
        }
    }

    [RelayCommand]
    private async Task LoadConfigAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow == null) return;

        var storage = desktop.MainWindow.StorageProvider;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Mở cấu hình Task",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } }
        });

        if (files.Count >= 1)
        {
            StopAllTasks(); // Dừng sạch các luồng đang chạy trước khi nạp dữ liệu mới

            await using var stream = await files[0].OpenReadAsync();
            var loadedTasks = await JsonSerializer.DeserializeAsync<ObservableCollection<TaskItem>>(stream);

            if (loadedTasks != null)
            {
                Tasks.Clear();
                foreach (var task in loadedTasks)
                {
                    // Trả lại các trạng thái UI cơ bản (vì lúc nãy ta đã đặt [JsonIgnore])
                    task.BtnGetPosText = "Lấy tọa độ (3s)";
                    task.IsBtnGetPosEnabled = true;
                    task.IsRunning = false;

                    Tasks.Add(task);
                }

                // Cập nhật lại số lượng hiển thị trên TextBox
                NumTasksInput = Tasks.Count.ToString();
            }
        }
    }
}