namespace ResourceFinder.Models;

public class AppSettings
{
    public string Hotkey { get; set; } = "Ctrl+Shift+Space";
    public string DefaultAction { get; set; } = "CopyToClipboard";
    public bool ShowInTaskbar { get; set; } = false;
    public string DataFilePath { get; set; } = string.Empty;
    public int WindowWidth  { get; set; } = 1000;
    public int WindowHeight { get; set; } = 700;
}
