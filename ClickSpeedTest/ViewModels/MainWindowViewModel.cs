using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickSpeedTest.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private DispatcherTimer? _timer;
    private readonly Stopwatch _stopwatch = new();
    
    [ObservableProperty] private int _clickCount = 0;
    [ObservableProperty] private double _timeLeft;
    [ObservableProperty] private double _cps = 0.0;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsReady))]
    private bool _isTesting = false;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsReady))]
    private bool _isFinished = false;
    
    public bool IsReady => !IsTesting && !IsFinished;
    
    public List<int> DurationOptions { get; } = new() { 5, 10, 15, 30, 60, 100, 120 };
    [ObservableProperty] private int _selectedDuration = 5;

    public MainWindowViewModel()
    {
        TimeLeft = SelectedDuration;
        
        _timer = new DispatcherTimer
        {

            Interval = TimeSpan.FromMilliseconds(30) 
        };
        _timer.Tick += Timer_Tick;
    }

    partial void OnSelectedDurationChanged(int value)
    {
        ResetTest();
    }

    [RelayCommand]
    private void RegisterClick()
    {
        if (IsFinished) return;

        if (!IsTesting)
        {
            IsTesting = true;
            _stopwatch.Restart();
            _timer?.Start();
        }

        ClickCount++;
        CalculateCps();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds; 
        
        TimeLeft = Math.Max(0, SelectedDuration - elapsedSeconds);

        if (TimeLeft <= 0)
        {
            _timer?.Stop();
            _stopwatch.Stop();
            IsTesting = false;
            IsFinished = true;
            
            CalculateCps(); 
        }
        else
        {
            CalculateCps();
        }
    }

    private void CalculateCps()
    {
        double timeElapsed = _stopwatch.Elapsed.TotalSeconds;
        if (timeElapsed > 0)
        {
            Cps = Math.Round(ClickCount / timeElapsed, 1);
        }
    }

    [RelayCommand]
    private void ResetTest()
    {
        _timer?.Stop();
        _stopwatch.Reset();
        ClickCount = 0;
        TimeLeft = SelectedDuration;
        Cps = 0.0;
        IsTesting = false;
        IsFinished = false;
    }
}