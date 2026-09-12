using Velopack;

namespace RovOverlay.Desktop;

// The entry point, ahead of WPF.
//
// Velopack has to run first. When the installer, an update or an uninstall starts this
// exe, it passes a switch that VelopackApp handles and then exits; if a window opened
// first, the operator would see it flash past during every update. WPF generates its
// own Main from App.xaml, so <StartupObject> in the csproj points here instead.
//
// Nothing else belongs here. In particular, nothing that shows a window: Main runs
// before Application.Run() pumps messages, and a window opened this early is never
// created at all - see the licence in App.xaml.cs, which learned that the hard way.
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
