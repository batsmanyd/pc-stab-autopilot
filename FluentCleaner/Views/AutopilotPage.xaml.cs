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
        StatusText.Text = enabled ? "Автопилот включён. Ежедневная проверка и безопасное обслуживание активны." : "Автопилот выключен. Можно выполнить вручную.";

        LastRunText.Text = AppSettings.Instance.LastAutopilotRun is { } lastRun
            ? lastRun.ToString("yyyy-MM-dd HH:mm:ss")
            : "Пока не выполнялась";

        TempText.Text = AppSettings.Instance.LastAutopilotTempBytes > 0
            ? AutopilotRunner.FormatBytes(AppSettings.Instance.LastAutopilotTempBytes)
            : "—";

        WarningsText.Text = AppSettings.Instance.LastAutopilotWarnings > 0
            ? $"Предупреждений: {AppSettings.Instance.LastAutopilotWarnings}"
            : "Критичных предупреждений нет";
    }

    private async void RunNow_Click(object sender, RoutedEventArgs e)
    {
        RunNowButton.IsEnabled = false;
        StatusText.Text = "Проверяю ПК и выполняю безопасное обслуживание...";

        try
        {
            var result = await AutopilotRunner.RunAsync(safeClean: true);
            ShowResult(result);
            StatusText.Text = $"Готово. Освобождено: {AutopilotRunner.FormatBytes(result.FreedBytes)}. Отчёт создан.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Ошибка: " + ex.Message;
        }
        finally
        {
            RunNowButton.IsEnabled = true;
        }
    }

    private void ShowResult(AutopilotRunResult result)
    {
        LastRunText.Text = result.RunAt.ToString("yyyy-MM-dd HH:mm:ss");
        DiskText.Text = $"Свободно {AutopilotRunner.FormatBytes(result.DriveCFreeBytes)} из {AutopilotRunner.FormatBytes(result.DriveCTotalBytes)}\nЗанято {result.DriveCUsedPercent}%";
        CpuText.Text = result.CpuName;
        RamText.Text = $"Занято {result.RamUsedPercent}%\nСвободно {result.RamFreeGb} GB из {result.RamTotalGb} GB";
        TempText.Text = $"Осталось: {AutopilotRunner.FormatBytes(result.TempBytes)}\nОсвобождено: {AutopilotRunner.FormatBytes(result.FreedBytes)}\nФайлов: {result.RemovedFiles}";
        SecurityText.Text = $"Defender: {result.DefenderStatus}\nFirewall: {result.FirewallStatus}";
        ErrorsText.Text = $"Автозагрузка: {result.StartupCount}\nОшибки System за 7 дней: {result.SystemErrors7Days}";
        WarningsText.Text = string.Join(Environment.NewLine, result.Warnings);
    }

    private async void Enable_Click(object sender, RoutedEventArgs e)
    {
        EnableButton.IsEnabled = false;
        try
        {
            var ok = await AutopilotScheduler.EnableDailyAsync(AppSettings.Instance.AutopilotHour);
            AppSettings.Instance.AutopilotEnabled = ok;
            AppSettings.Instance.Save();
            StatusText.Text = ok ? "Автопилот включён. Каждый день в 11:00 будет проверка и безопасное обслуживание." : "Не удалось включить автопилот.";
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
            StatusText.Text = ok ? "Автопилот выключен." : "Не удалось выключить автопилот.";
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
