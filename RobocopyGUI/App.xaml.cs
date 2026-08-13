using Microsoft.UI.Xaml;

namespace RobocopyGUI;

public partial class App : Application
{
    private Window? _window;
    public App()
    {
        this.InitializedComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
