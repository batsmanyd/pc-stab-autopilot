using Microsoft.UI.Xaml;
using System.Text;
using System.Text.Json;

namespace FluentCleaner.Services;

public sealed class AutopilotRunResult
{
    public DateTime RunAt { get; set; } = DateTime.Now;
    public string Windows { get; set; } = Environment.OSVersion.VersionString;
    public string ComputerName { get; set; } = Environment.MachineName;
    public string UserName { get; set; } = Environment.UserName;
    public long DriveCTotalBytes { get; set; }
    public long DriveCFreeBytes { get; set; }
    public double DriveCUsedPercent { get; set; }
    public long TempBytes { get; set; }
    public int SystemErrors7Days { get; set; }
    public List<string> Warnings { get; set; } = [];
    public string TxtReportPath { get; set; } = "";
    public string JsonReportPath { get; set; } = "";
}

public static class AutopilotRunner
{
    public static string BaseDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FluentCleaner", "Autopilot");

    public static string ReportsDirectory => Path.Combine(BaseDirectory, "Reports");

    public static async Task<AutopilotRunResult> RunAsync(bool exitAfterRun = false)
    {
        Directory.CreateDirectory(ReportsDirectory);
        var result = new AutopilotRunResult();

        await Task.Run(() =>
        {
            ReadDriveC(result);
            result.TempBytes = GetFolderSizeSafe(Path.GetTempPath()) +
                               GetFolderSizeSafe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));

            // MVP: event log parsing is disabled to avoid extra runtime packages.
            // We will add System log reading after the Autopilot screen is stable.
            result.SystemErrors7Days = 0;

            BuildWarnings(result);
            WriteReports(result);

            AppSettings.Instance.LastAutopilotRun = result.RunAt;
            AppSettings.Instance.LastAutopilotTempBytes = result.TempBytes;
            AppSettings.Instance.LastAutopilotWarnings = result.Warnings.Count(w => !w.Equals("Критичных предупреждений нет", StringComparison.OrdinalIgnoreCase));
            AppSettings.Instance.Save();
        });

        if (exitAfterRun)
            Application.Current.Exit();

        return result;
    }

    private static void ReadDriveC(AutopilotRunResult result)
    {
        try
        {
            var drive = new DriveInfo("C");
            if (!drive.IsReady) return;
            result.DriveCTotalBytes = drive.TotalSize;
            result.DriveCFreeBytes = drive.AvailableFreeSpace;
            var used = drive.TotalSize - drive.AvailableFreeSpace;
            result.DriveCUsedPercent = drive.TotalSize > 0 ? Math.Round(used * 100.0 / drive.TotalSize, 1) : 0;
        }
        catch { }
    }

    private static long GetFolderSizeSafe(string path)
    {
        long total = 0;
        try
        {
            if (!Directory.Exists(path)) return 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; }
                catch { }
            }
        }
        catch { }
        return total;
    }

    private static void BuildWarnings(AutopilotRunResult result)
    {
        if (result.DriveCFreeBytes > 0 && result.DriveCFreeBytes < 25L * 1024 * 1024 * 1024)
            result.Warnings.Add($"Мало свободного места на C: {FormatBytes(result.DriveCFreeBytes)}");

        if (result.DriveCUsedPercent >= 85)
            result.Warnings.Add($"Диск C заполнен на {result.DriveCUsedPercent}%");

        if (result.SystemErrors7Days > 20)
            result.Warnings.Add($"Много ошибок System за 7 дней: {result.SystemErrors7Days}");

        if (result.Warnings.Count == 0)
            result.Warnings.Add("Критичных предупреждений нет");
    }

    private static void WriteReports(AutopilotRunResult result)
    {
        var stamp = result.RunAt.ToString("yyyy-MM-dd_HH-mm-ss");
        result.TxtReportPath = Path.Combine(ReportsDirectory, $"pc-stab-autopilot-{stamp}.txt");
        result.JsonReportPath = Path.Combine(ReportsDirectory, $"pc-stab-autopilot-{stamp}.json");

        var warnings = string.Join(Environment.NewLine, result.Warnings.Select(w => "- " + w));
        var text = $$"""
        PC Штаб Автопилот — отчёт
        Дата: {{result.RunAt:yyyy-MM-dd HH:mm:ss}}

        Режим MVP: наблюдение и отчёты.
        На этом этапе программа ничего не удаляет и не меняет.

        ПК:
        Windows: {{result.Windows}}
        Компьютер: {{result.ComputerName}}
        Пользователь: {{result.UserName}}

        Диск C:
        Свободно: {{FormatBytes(result.DriveCFreeBytes)}}
        Всего: {{FormatBytes(result.DriveCTotalBytes)}}
        Занято: {{result.DriveCUsedPercent}}%

        Временные файлы, расчёт: {{FormatBytes(result.TempBytes)}}
        Ошибки System за 7 дней: будет добавлено следующим шагом

        Предупреждения:
        {{warnings}}
        """;

        File.WriteAllText(result.TxtReportPath, text, Encoding.UTF8);
        File.WriteAllText(result.JsonReportPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:N1} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:N1} MB";
        double gb = mb / 1024.0;
        return $"{gb:N1} GB";
    }
}
