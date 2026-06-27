using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluentCleaner.Services;

public record CleanHistoryEntry(DateTime Date, long BytesFreed, int ItemsRemoved);

public class AppSettings
{
    public static AppSettings Instance { get; private set; } = Load();

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FluentCleaner", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string? CustomWinapp2Path { get; set; }
    public string? Theme { get; set; } = "Dark";
    public HashSet<string> SelectedEntries { get; set; } = [];

    public bool EnableWinapp2 { get; set; } = true;
    public bool EnableWinapp3 { get; set; } = false;
    public bool EnableWinappx { get; set; } = false;

    public bool PostCleanEnabled { get; set; } = false;
    public string PostCleanCommands { get; set; } = "";

    public bool AutopilotEnabled { get; set; } = false;
    public string AutopilotMode { get; set; } = "Observe";
    public int AutopilotHour { get; set; } = 11;
    public int SafeCleanOlderThanDays { get; set; } = 7;
    public bool SafeCleanUserTemp { get; set; } = true;
    public bool SafeCleanWindowsTemp { get; set; } = true;
    public bool SafeCleanExplorerThumbCache { get; set; } = false;
    public DateTime? LastAutopilotRun { get; set; }
    public long LastAutopilotTempBytes { get; set; }
    public int LastAutopilotWarnings { get; set; }

    public string Backdrop { get; set; } = "mica";

    public int WindowWidth { get; set; } = 1100;
    public int WindowHeight { get; set; } = 720;

    public bool CleanHistoryEnabled { get; set; } = true;
    public List<CleanHistoryEntry> CleanHistory { get; set; } = [];

    public string? GroqApiKey { get; set; }

    public bool DonationDismissed { get; set; } = true;

    [JsonIgnore]
    public bool HasCustomPath =>
        !string.IsNullOrWhiteSpace(CustomWinapp2Path) && File.Exists(CustomWinapp2Path);

    public static void Reload() => Instance = Load();

    public IEnumerable<string> ResolveDatabasePaths()
    {
        if (EnableWinapp2)
        {
            var p = Path.Combine(AppContext.BaseDirectory, "Winapp2.ini");
            if (File.Exists(p)) yield return p;
        }
        if (EnableWinapp3)
        {
            var p = Path.Combine(AppContext.BaseDirectory, "Winapp3.ini");
            if (File.Exists(p)) yield return p;
        }
        if (!string.IsNullOrWhiteSpace(CustomWinapp2Path) && File.Exists(CustomWinapp2Path))
            yield return CustomWinapp2Path;
    }

    public string ResolveWinapp2Path()
    {
        if (!string.IsNullOrWhiteSpace(CustomWinapp2Path) && File.Exists(CustomWinapp2Path))
            return CustomWinapp2Path;
        return Path.Combine(AppContext.BaseDirectory, "Winapp2.ini");
    }

    public void Save()
    {
        try
        {
            DonationDismissed = true;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { }
    }

    private static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return new() { DonationDismissed = true };
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile), JsonOptions) ?? new();
            s.CustomWinapp2Path = NormalizePath(s.CustomWinapp2Path);
            s.DonationDismissed = true;
            return s;
        }
        catch { return new() { DonationDismissed = true }; }
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var result = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
