using Microsoft.UI.Xaml;
using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FluentCleaner.Services;

public sealed class AutopilotRunResult
{
    public DateTime RunAt { get; set; } = DateTime.Now;
    public string Windows { get; set; } = Environment.OSVersion.VersionString;
    public string ComputerName { get; set; } = Environment.MachineName;
    public string UserName { get; set; } = Environment.UserName;
    public string CpuName { get; set; } = "Не прочитано";
    public double RamTotalGb { get; set; }
    public double RamFreeGb { get; set; }
    public double RamUsedPercent { get; set; }
    public long DriveCTotalBytes { get; set; }
    public long DriveCFreeBytes { get; set; }
    public double DriveCUsedPercent { get; set; }
    public long TempBytes { get; set; }
    public long FreedBytes { get; set; }
    public int RemovedFiles { get; set; }
    public int RemoveErrors { get; set; }
    public string DefenderStatus { get; set; } = "Не прочитано";
    public string FirewallStatus { get; set; } = "Не прочитано";
    public int StartupCount { get; set; }
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

    public static async Task<AutopilotRunResult> RunAsync(bool exitAfterRun = false, bool safeClean = false)
    {
        Directory.CreateDirectory(ReportsDirectory);
        var result = new AutopilotRunResult();

        await Task.Run(() =>
        {
            ReadDriveC(result);
            ReadSystemInfo(result);

            var userTemp = Path.GetTempPath();
            var windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");

            result.TempBytes = GetFolderSizeSafe(userTemp) + GetFolderSizeSafe(windowsTemp);

            if (safeClean)
            {
                CleanOldTempFiles(userTemp, AppSettings.Instance.SafeCleanOlderThanDays, result);
                CleanOldTempFiles(windowsTemp, AppSettings.Instance.SafeCleanOlderThanDays, result);
                result.TempBytes = GetFolderSizeSafe(userTemp) + GetFolderSizeSafe(windowsTemp);
            }

            result.StartupCount = CountStartupItems();
            result.DefenderStatus = MapDefenderStatus(ReadPowerShell("try { if((Get-MpComputerStatus).RealTimeProtectionEnabled){'ON'}else{'OFF'} } catch {'UNKNOWN'}"));
            result.FirewallStatus = MapFirewallStatus(ReadPowerShell("try { $off=(Get-NetFirewallProfile | Where-Object { -not $_.Enabled }).Count; if($off -eq 0){'ON'}else{'PARTIAL'} } catch {'UNKNOWN'}"));
            result.SystemErrors7Days = ParseInt(ReadPowerShell("try { (Get-WinEvent -FilterHashtable @{LogName='System';Level=1,2;StartTime=(Get-Date).AddDays(-7)} -ErrorAction SilentlyContinue | Measure-Object).Count } catch { 0 }"));

            BuildWarnings(result);
            WriteReports(result, safeClean);

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

    private static void ReadSystemInfo(AutopilotRunResult result)
    {
        result.CpuName = ReadPowerShell("try { (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name).Trim() } catch { 'Не прочитано' }");
        var ram = ReadPowerShell("try { $os=Get-CimInstance Win32_OperatingSystem; '{0}|{1}' -f ([int64]$os.TotalVisibleMemorySize*1024),([int64]$os.FreePhysicalMemory*1024) } catch { '0|0' }");
        var parts = ram.Split('|');
        if (parts.Length == 2 && long.TryParse(parts[0], out var total) && long.TryParse(parts[1], out var free) && total > 0)
        {
            result.RamTotalGb = Math.Round(total / 1024d / 1024d / 1024d, 1);
            result.RamFreeGb = Math.Round(free / 1024d / 1024d / 1024d, 1);
            result.RamUsedPercent = Math.Round((total - free) * 100d / total, 1);
        }
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

    private static void CleanOldTempFiles(string path, int olderThanDays, AutopilotRunResult result)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            var limit = DateTime.Now.AddDays(-Math.Max(1, olderThanDays));

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (!info.Exists || info.LastWriteTime >= limit) continue;
                    var length = info.Length;
                    info.Delete();
                    result.FreedBytes += length;
                    result.RemovedFiles++;
                }
                catch
                {
                    result.RemoveErrors++;
                }
            }
        }
        catch { }
    }

    private static int CountStartupItems()
    {
        var count = 0;
        try { count += Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")?.GetValueNames().Length ?? 0; } catch { }
        try { count += Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")?.GetValueNames().Length ?? 0; } catch { }
        try { count += Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run")?.GetValueNames().Length ?? 0; } catch { }
        try
        {
            var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(userStartup)) count += Directory.GetFiles(userStartup).Length;
        }
        catch { }
        try
        {
            var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            if (Directory.Exists(commonStartup)) count += Directory.GetFiles(commonStartup).Length;
        }
        catch { }
        return count;
    }

    private static string ReadPowerShell(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return "";
            if (!process.WaitForExit(12000))
            {
                try { process.Kill(); } catch { }
                return "";
            }
            return process.StandardOutput.ReadToEnd().Trim();
        }
        catch { return ""; }
    }

    private static string MapDefenderStatus(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "ON" => "Включена",
            "OFF" => "Выключена",
            _ => "Не прочитано"
        };
    }

    private static string MapFirewallStatus(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "ON" => "Включён",
            "PARTIAL" => "Есть отключённые профили",
            _ => "Не прочитано"
        };
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static void BuildWarnings(AutopilotRunResult result)
    {
        if (result.DriveCFreeBytes > 0 && result.DriveCFreeBytes < 25L * 1024 * 1024 * 1024)
            result.Warnings.Add($"Мало свободного места на C: {FormatBytes(result.DriveCFreeBytes)}");

        if (result.DriveCUsedPercent >= 85)
            result.Warnings.Add($"Диск C заполнен на {result.DriveCUsedPercent}%");

        if (result.RamUsedPercent >= 85)
            result.Warnings.Add($"Высокая загрузка RAM: {result.RamUsedPercent}%");

        if (result.SystemErrors7Days > 20)
            result.Warnings.Add($"Много ошибок System за 7 дней: {result.SystemErrors7Days}");

        if (result.DefenderStatus.Contains("Выключена", StringComparison.OrdinalIgnoreCase))
            result.Warnings.Add("Защита Windows Defender выключена");

        if (result.Warnings.Count == 0)
            result.Warnings.Add("Критичных предупреждений нет");
    }

    private static void WriteReports(AutopilotRunResult result, bool safeClean)
    {
        var stamp = result.RunAt.ToString("yyyy-MM-dd_HH-mm-ss");
        result.TxtReportPath = Path.Combine(ReportsDirectory, $"pc-stab-autopilot-{stamp}.txt");
        result.JsonReportPath = Path.Combine(ReportsDirectory, $"pc-stab-autopilot-{stamp}.json");

        var warnings = string.Join(Environment.NewLine, result.Warnings.Select(w => "- " + w));
        var text = $$"""
        PC Штаб Автопилот — отчёт
        Дата: {{result.RunAt:yyyy-MM-dd HH:mm:ss}}

        Режим: {{(safeClean ? "проверка + безопасная очистка TEMP старше 7 дней" : "проверка и отчёт")}}
        Опасные действия отключены: реестр, службы, драйверы, программы и личные файлы не трогаются.

        ПК:
        Windows: {{result.Windows}}
        Компьютер: {{result.ComputerName}}
        Пользователь: {{result.UserName}}
        CPU: {{result.CpuName}}
        RAM: занято {{result.RamUsedPercent}}%, свободно {{result.RamFreeGb}} GB из {{result.RamTotalGb}} GB

        Диск C:
        Свободно: {{FormatBytes(result.DriveCFreeBytes)}}
        Всего: {{FormatBytes(result.DriveCTotalBytes)}}
        Занято: {{result.DriveCUsedPercent}}%

        Обслуживание:
        Временные файлы после проверки: {{FormatBytes(result.TempBytes)}}
        Освобождено: {{FormatBytes(result.FreedBytes)}}
        Удалено временных файлов: {{result.RemovedFiles}}
        Ошибок очистки: {{result.RemoveErrors}}
        Автозагрузка: {{result.StartupCount}}
        Defender: {{result.DefenderStatus}}
        Firewall: {{result.FirewallStatus}}
        Ошибки System за 7 дней: {{result.SystemErrors7Days}}

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
