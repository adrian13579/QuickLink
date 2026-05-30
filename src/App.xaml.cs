using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using QuickLink.Models;
using QuickLink.Services;
using QuickLink.ViewModels;

namespace QuickLink;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static MainWindow? _mainWindow;
    public static Window? MainWindow => _mainWindow;

    private static Mutex?           _mutex;
    private static EventWaitHandle? _showEvent;
    private const string MutexName = "QuickLink_SingleInstance";
    private const string EventName = "QuickLink_ShowEvent";

    public App()
    {
        // Single-instance guard — must run before InitializeComponent
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirst);
        if (!isFirst)
        {
            // Signal the running instance to show itself, then exit immediately
            if (EventWaitHandle.TryOpenExisting(EventName, out var ev))
            {
                ev.Set();
                ev.Dispose();
            }
            Environment.Exit(0);
            return;
        }

        // First instance: listen for signals from future launches
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        new Thread(MonitorActivation) { IsBackground = true, Name = "ShowEventMonitor" }.Start();

        InitializeComponent();
        Services = BuildServices();
    }

    private static void MonitorActivation()
    {
        try
        {
            while (_showEvent?.WaitOne() == true)
                _mainWindow?.DispatcherQueue.TryEnqueue(_mainWindow.ShowApp);
        }
        catch (ObjectDisposedException) { }
    }

    private static IServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<SettingsService>();
        sc.AddSingleton<HotkeyService>();
        sc.AddSingleton<TrayIconService>();
        sc.AddSingleton<IResourceRepository, JsonResourceRepository>();
        sc.AddSingleton<SearchService>();
        sc.AddTransient<SearchViewModel>();
        sc.AddTransient<ManageViewModel>();
        return sc.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Show the window immediately — no async before Activate
        _mainWindow = new MainWindow();
        _mainWindow.Activate();

        // Seed sample data in the background after UI is visible
        _ = SeedSampleDataIfEmptyAsync();
    }

    public static void NavigateToSearch()                => _mainWindow?.NavigateTo("search");
    public static void NavigateToSettings()              => _mainWindow?.NavigateTo("settings");
    public static void NavigateToManageResource(Guid id) => _mainWindow?.NavigateToResource(id);

    private static readonly JsonSerializerOptions _caseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private static async Task SeedSampleDataIfEmptyAsync()
    {
        try
        {
            var repo = Services.GetRequiredService<IResourceRepository>();
            var existing = await repo.GetAllAsync();
            if (existing.Count > 0) return;

            var samplePath = Path.Combine(AppContext.BaseDirectory, "sample-data", "resources.json");
            if (!File.Exists(samplePath)) return;

            var json = await File.ReadAllTextAsync(samplePath);
            var resources = JsonSerializer.Deserialize<List<Resource>>(json, _caseInsensitive);
            if (resources == null) return;

            await repo.SaveRangeAsync(resources);
        }
        catch { /* non-critical — app works without seed data */ }
    }
}
