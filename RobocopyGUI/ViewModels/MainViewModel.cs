using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using RobocopyGUI.Models;
using RobocopyGUI.Services;

namespace RobocopyGUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IRobocopyService _robocopyService;
    private readonly DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource? _cts;

    public MainViewModel(DispatcherQueue dispatcherQueue, IRobocopyService? robocopyService = null)
    {
        _dispatcherQueue = dispatcherQueue;
        _robocopyService = robocopyService ?? new RobocopyService();
    }

    [ObservableProperty] private string sourcePath = string.Empty;
    [ObservableProperty] private string destinationPath = string.Empty;
    [ObservableProperty] private string fileFilter = "*.*";

    [ObservableProperty] private bool mirror;
    [ObservableProperty] private bool copySubdirectories = true;
    [ObservableProperty] private bool purge;
    [ObservableProperty] private bool restartableModel;

    [ObservableProperty] private bool multiThreaded = true;
    [ObservableProperty] private double threadCount = 8;
    [ObservableProperty] private double retryCount = 3;
    [ObservableProperty] private double waitSeconds = 5;

    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private string statusText = "Ready.";
    [ObservableProperty] private string summaryText = string.Empty;

    public ObservableCollection<string> LogLines { get; } = new();

    private bool CanStart() => !IsRunning;
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => !IsRunning;
    private void OnIsRunningChanged(bool value)
    {
        StartCommand.NotifyCanExecutedChanged();
        CancelCommand.NotifyCanExecutedChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]

    private async Task StartTask()
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
        {
            StatusText = "Please choose both a source and destination folder.";
            return;
        }

        LogLines.Clear();
        summaryText = string.Empty;
        IsRunning = true;
        StatusText = "Running...";

        _cts = new CancellationTokenSource();

        var options = new RobocopyOptions
        {
            SourcePath = SourcePath,
            DestinationPath = DestinationPath,
            FileFilter = FileFilter,
            Mirror = Mirror,
            CopySubDirectories = CopySubdirectories,
            Purge = Purge,
            RestartableMode = RestartableModel,
            MultiThreaded = MultiThreaded,
            ThreadCount = (int)ThreadCount,
            RetryCount = (int)RetryCount,
            WaitSecond = (int)WaitSeconds,
        };

        try
        {
            var result = await _robocopyService.RunAsync(
                options,
                onOutputLine: line => _dispatcherQueue.TryEnqueue(() =>
                {
                    LogLines.Add(line);
                    if (LogLines.Count > 5000) LogLines.RemoveAt(0);
                }),
                cancellationToken: _cts.Token);

            StatusText = result.Succeeded ? "Completed successfully" : "Completed with errors.";
            summaryText =
                $"Dirs : {result.DirsCopied}/{result.DirsTotal} " +
                $"Files : {result.FilesCopied}/{result.FilesTotal} " +
                $"Bytes : {result.BytesCopied}/{result.BytesTotal} " +
                $"Elapsed : {result.Elapsed:hh\\:mm\\:ss} " +
                $"Exit code : {result.ExitCode} ({result.ExitCodeDescription})";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error : {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }
}