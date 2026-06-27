using System.Diagnostics;

namespace FluentCleaner.Services;

public static class AutopilotScheduler
{
    private const string TaskName = "PC Stab Autopilot";

    public static async Task<bool> EnableDailyAsync(int hour)
    {
        var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "FCleaner.exe");
        var time = $"{Math.Clamp(hour, 0, 23):00}:00";
        var taskRun = $"\"{exe}\" /AUTOPILOT";
        var args = $"/Create /TN \"{TaskName}\" /SC DAILY /ST {time} /TR \"{taskRun}\" /F";
        return await RunSchtasksAsync(args);
    }

    public static async Task<bool> DisableAsync()
    {
        return await RunSchtasksAsync($"/Delete /TN \"{TaskName}\" /F");
    }

    public static async Task<bool> IsEnabledAsync()
    {
        return await RunSchtasksAsync($"/Query /TN \"{TaskName}\"");
    }

    private static async Task<bool> RunSchtasksAsync(string args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }
}
