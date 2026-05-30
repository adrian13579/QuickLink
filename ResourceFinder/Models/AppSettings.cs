namespace ResourceFinder.Models;

public class AppSettings
{
    public string Hotkey { get; set; } = "Ctrl+Space";
    public string DefaultAction { get; set; } = "CopyToClipboard";
    public bool ShowInTaskbar { get; set; } = false;
    public string DataFilePath { get; set; } = string.Empty;
}
