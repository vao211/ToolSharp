using System.Diagnostics;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutoKeySpammer.Services;
using Avalonia.Threading;
using ReactiveUI;

namespace AutoKeySpammer.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly IInputSimulator _inputSimulator;
    private CancellationTokenSource? _cts;
    
    private string _selectedKey = "space";
    private int _cps = 10;
    private int _totalClicks = 100;
    private bool _isInfinite = false;
    private bool _isRunning = false;
    private bool _isWaitingForKey = false;
    private string _selectButtonText = "Nhấn để chọn phím cần spam";

    public string SelectButtonText
    {
        get => _selectButtonText;
        set => this.RaiseAndSetIfChanged(ref _selectButtonText, value);
    }

    public int Cps
    {
        get => _cps;
        set => this.RaiseAndSetIfChanged(ref _cps, value < 1 ? 1 : value);
    }

    public int TotalClicks
    {
        get => _totalClicks;
        set => this.RaiseAndSetIfChanged(ref _totalClicks, value < 1 ? 1 : value);
    }

    public bool IsInfinite
    {
        get => _isInfinite;
        set => this.RaiseAndSetIfChanged(ref _isInfinite, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    public bool IsWaitingForKey
    {
        get => _isWaitingForKey;
        set => this.RaiseAndSetIfChanged(ref _isWaitingForKey, value);
    }

    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectKeyCommand { get; }

    public MainWindowViewModel()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _inputSimulator = new WindowsInputSimulator();
        }
        else
        {
            _inputSimulator = new LinuxX11InputSimulator();
        }

        StartCommand = ReactiveCommand.CreateFromTask(StartSpamAsync, this.WhenAnyValue(x => x.IsRunning, running => !running));
        StopCommand = ReactiveCommand.Create(StopSpam, this.WhenAnyValue(x => x.IsRunning));
        SelectKeyCommand = ReactiveCommand.Create(CaptureKey);
    }

    private void CaptureKey()
    {
        IsWaitingForKey = true;
        SelectButtonText = "Đang chờ bấm phím (Nhấn phím bất kỳ)...";
    }

    public void SetKey(string keyName)
    {
        _selectedKey = keyName;
        SelectButtonText = $"Phím đang chọn: {keyName.ToUpper()}";
        IsWaitingForKey = false;
    }

    private async Task StartSpamAsync()
    {
        Dispatcher.UIThread.Post(() => IsRunning = true);
    
        int currentCps = Cps;
        int currentTotalClicks = TotalClicks;
        bool currentIsInfinite = IsInfinite;
        string currentKey = _selectedKey;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                double intervalMs = 1000.0 / currentCps;
                long nextTick = sw.ElapsedMilliseconds + (long)intervalMs;
                int count = 0;

                while (!token.IsCancellationRequested)
                {
                    if (!currentIsInfinite && count >= currentTotalClicks)
                        break;

                    _inputSimulator.ClickKey(currentKey);
                    count++;

                    long now = sw.ElapsedMilliseconds;
                    long delay = nextTick - now;
                    if (delay > 0)
                    {
                        await Task.Delay((int)delay, token);
                    }
                    nextTick += (long)intervalMs;
                }
            }, token);
        }
        catch (TaskCanceledException) { }
        finally
        {
            Dispatcher.UIThread.Post(() => IsRunning = false);
        }
    }

    private void StopSpam()
    {
        _cts?.Cancel();
        Dispatcher.UIThread.Post(() => IsRunning = false);
    }
}