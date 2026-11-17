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
        StatusText.Text = "Проверяю ПК без удаления файлов...";

        try
        {
            var result = await AutopilotRunner.RunAsync(safeClean: false);
            ShowResult(result);
            StatusText.Text = "Диагностика завершена. Файлы не удалялись. Отчёт создан.";
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
        ErrorsText.Text = result.SystemEventsReadSuccessfully
            ? $"Автозагрузка: {result.StartupCount}\\nСобытий System за 24 часа: {result.SystemErrors24Hours}\\nЗа 7 дней: {result.SystemErrors7Days}"
            : $"Автозагрузка: {result.StartupCount}\\nЖурнал System недоступен";
        EventDetailsText.Text = result.SystemEventsReadSuccessfully
            ? (result.SystemEventGroups.Count == 0
                ? "Нет ошибок и критических событий в доступной выборке."
                : string.Join("\\n\\n", result.SystemEventGroups.Take(6).Select(g =>
                    $"{g.Priority}: {g.Source}, ID {g.EventId} — за 24 ч.: {g.Count24Hours}, за 7 д.: {g.Count7Days}, последнее: {g.LastSeen:dd.MM HH:mm}")) +
                  (result.SystemEventsTruncated ? "\\nВыборка ограничена 2000 записями." : "") +
                  "\\nПолные тексты и даты — в отчёте.")
            : "Сведения недоступны. Проверь журнал событий Windows.";
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
            StatusText.Text = ok ? $"Автопилот включён. Ежедневная проверка запланирована на {Math.Clamp(AppSettings.Instance.AutopilotHour, 0, 23):00}:00." : "Не удалось включить автопилот. Проверьте права пользователя и Планировщик заданий Windows.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Ошибка включения автопилота: " + ex.Message;
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
