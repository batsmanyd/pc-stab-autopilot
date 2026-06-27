namespace FluentCleaner.Tools;

public enum ToolsCategory
{
    All,
    System,
    Privacy,
    Network,
    Apps,
    Debloat
}

public sealed class ScriptMeta
{
    public string Description { get; set; } = "No description available.";
    public List<string> Options { get; set; } = [];
    public ToolsCategory Category { get; set; } = ToolsCategory.All;
    public bool UseConsole { get; set; }
    public bool UseLog { get; set; }
    public bool SupportsInput { get; set; }
    public string InputPlaceholder { get; set; } = "";
    public string PoweredByText { get; set; } = "";
    public string PoweredByUrl { get; set; } = "";
}

public sealed class ToolsDefinition
{
    public ToolsDefinition(string title, string icon, string scriptPath, ScriptMeta meta)
    {
        Title = title;
        Icon = icon;
        ScriptPath = scriptPath;
        Description = meta.Description;
        Options = meta.Options;
        Category = meta.Category;
        UseConsole = meta.UseConsole;
        UseLog = meta.UseLog;
        SupportsInput = meta.SupportsInput;
        InputPlaceholder = meta.InputPlaceholder;
        PoweredByText = meta.PoweredByText;
        PoweredByUrl = meta.PoweredByUrl;
    }

    public string Title { get; }
    public string Icon { get; }
    public string ScriptPath { get; }
    public string Description { get; }
    public List<string> Options { get; }
    public ToolsCategory Category { get; }
    public bool UseConsole { get; }
    public bool UseLog { get; }
    public bool SupportsInput { get; }
    public string InputPlaceholder { get; }
    public string PoweredByText { get; }
    public string PoweredByUrl { get; }
}
