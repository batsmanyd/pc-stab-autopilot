using FluentCleaner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace FluentCleaner.Views;

public sealed partial class AutopilotPage : Page
{
    public AutopilotPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        var enabled = await AutopilotScheduler.IsEnabledAsync();
        StatusText.Text = enabled ? "Daily autopilot is enabled." : "Daily autopilot is disabled.";

        if (AppSettings.Instance.LastAutopilotRun is { } lastRun)
            LastRunText.Text = lastRun.ToString("yyyy-MM-dd HH:mm:ss");
        else
            LastRunText.Text = "Not run yet";

        TempText.Text = AppSettings.Instance.LastAutopilotTempBytes > 0
            ? AutopilotRunner.FormatBytes(AppSettings.Instance.LastAutopilotTempBytes)
            : "—";

        WarningsText.Text = AppSettings.Instance.LastAutopilotWarnings > 0
            ? AppSettings.Instance.LastAutopilotWarnings.ToString()
            : "No critical warnings";
    }

    private async void RunNow_Click(object sender, RoutedEventArgs e)
    {
        RunNowButton.IsEnabled = false;
        StatusText.Text = "Checking PC...";

        try
        {
            var result = await AutopilotRunner.RunAsync();
            LastRunText.Text = result.RunAt.ToString("yyyy-MM-dd HH:mm:ss");
            TempText.Text = AutopilotRunner.FormatBytes(result.TempBytes);
            WarningsText.Text = string.Join(Environment.NewLine, result.Warnings);
            StatusText.Text = "Check completed. Report created.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
        }
        finally
        {
            RunNowButton.IsEnabled = true;
        }
    }

    private async void Enable_Click(object sender, RoutedEventArgs e)
    {
        EnableButton.IsEnabled = false;
        try
        {
            var ok = await AutopilotScheduler.EnableDailyAsync(AppSettings.Instance.AutopilotHour);
            AppSettings.Instance.AutopilotEnabled = ok;
            AppSettings.Instance.Save();
            StatusText.Text = ok ? "Daily autopilot enabled." : "Could not enable daily autopilot.";
        }
        finally
        {
            EnableButton.IsEnabled = true;
            await RefreshStatusAsync();
        }
    }

    private async void Disable_Click(object sender, RoutedEventArgs e)
    {
        DisableButton.IsEnabled = false;
        try
        {
            var ok = await AutopilotScheduler.DisableAsync();
            AppSettings.Instance.AutopilotEnabled = false;
            AppSettings.Instance.Save();
            StatusText.Text = ok ? "Daily autopilot disabled." : "Could not disable daily autopilot.";
        }
        finally
        {
            DisableButton.IsEnabled = true;
            await RefreshStatusAsync();
        }
    }

    private void OpenReports_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AutopilotRunner.ReportsDirectory);
        Process.Start(new ProcessStartInfo(AutopilotRunner.ReportsDirectory) { UseShellExecute = true });
    }
}
