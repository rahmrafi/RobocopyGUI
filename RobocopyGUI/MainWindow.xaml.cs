using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RobocopyGUI.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace RobocopyGUI;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        this.InitializeComponent();
        ViewModel = new MainViewModel(DispatcherQueue);
    }

    private async void BrowserSource_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickerFolderAsync();
        if (folder != null)
            ViewModel.SourcePath = folder.Path;
    }

    private async void BrowserDestination_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickerFolderAsync();
        if (folder != null)
            ViewModel.DestinationPath = folder.Path;
    }

    private async Task<StorageFolder?> PickerFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
        };
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        return await picker.PickSingleFolderAsync();
    }
}
