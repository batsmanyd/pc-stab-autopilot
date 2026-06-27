using FluentCleaner.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FluentCleaner;

public partial class App : Application
{
    public MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppSettings.Reload();

        var cmdArgs = Environment.GetCommandLineArgs();
        bool isAuto      = cmdArgs.Any(a => a.Equals("/AUTO",      StringComparison.OrdinalIgnoreCase));
        bool isAutopilot = cmdArgs.Any(a => a.Equals("/AUTOPILOT", StringComparison.OrdinalIgnoreCase));
        bool isShutdown  = cmdArgs.Any(a => a.Equals("/SHUTDOWN",  StringComparison.OrdinalIgnoreCase));

        if (isAutopilot)
        {
            _ = AutopilotRunner.RunAsync(exitAfterRun: true, safeClean: true);
            return;
        }

        if (isAuto)
        {
            _ = SilentRunner.RunAsync(isShutdown);
            return;
        }

        MainWindow = new MainWindow();
        SetupTitleBar();
        RestoreWindowSize();
        ApplyBackdrop(AppSettings.Instance.Backdrop);
        ApplyTheme(AppSettings.Instance.Theme);
        MainWindow.Activate();

        MainWindow.Closed += (_, _) =>
        {
            var size = MainWindow.AppWindow.Size;
            AppSettings.Instance.WindowWidth  = size.Width;
            AppSettings.Instance.WindowHeight = size.Height;
            AppSettings.Instance.Save();
        };
    }

    private void SetupTitleBar()
    {
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var bar = MainWindow!.AppWindow.TitleBar;
            bar.ButtonBackgroundColor         = Colors.Transparent;
            bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }

    private void RestoreWindowSize()
    {
        var w = AppSettings.Instance.WindowWidth;
        var h = AppSettings.Instance.WindowHeight;
        MainWindow!.AppWindow.Resize(new SizeInt32(w, h));

        if (DisplayArea.GetFromWindowId(MainWindow.AppWindow.Id, DisplayAreaFallback.Primary) is { } display)
        {
            var area = display.WorkArea;
            MainWindow.AppWindow.Move(new PointInt32(
                area.X + (area.Width  - w) / 2,
                area.Y + (area.Height - h) / 2));
        }
    }

    public void ApplyTheme(string? theme)
    {
        var elementTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark"  => ElementTheme.Dark,
            _       => ElementTheme.Default
        };

        if (MainWindow?.Content is FrameworkElement root)
            root.RequestedTheme = elementTheme;

        if (MainWindow is not { } win) return;

        win.AppWindow.TitleBar.PreferredTheme = elementTheme switch
        {
            ElementTheme.Light => TitleBarTheme.Light,
            ElementTheme.Dark  => TitleBarTheme.Dark,
            _                  => TitleBarTheme.UseDefaultAppMode
        };
    }

    public void ApplyBackdrop(string? backdrop)
    {
        if (MainWindow is null) return;

        MainWindow.SystemBackdrop = backdrop?.ToLowerInvariant() switch
        {
            "acrylic" => new DesktopAcrylicBackdrop(),
            _         => new MicaBackdrop()
        };
    }
}
