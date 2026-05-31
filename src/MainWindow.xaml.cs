using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickLink.Pages;
using QuickLink.Services;
using Windows.Graphics;

namespace QuickLink;

public sealed partial class MainWindow : Window
{
    private const int MinWidth      = 700;
    private const int MinHeight     = 500;
    private const int DragStripDips = 28;

    private bool _forceExit;

    [DllImport("user32.dll")]
    private static extern int GetDpiForWindow(IntPtr hwnd);

    public MainWindow()
    {
        InitializeComponent();

        // Fill the full client area — no WinUI title-bar reservation
        ExtendsContentIntoTitleBar = true;

        // Remove OS caption buttons (X / min / max) while keeping the Win11
        // rounded-corner border and drop shadow
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        AppWindow.SetIcon("Assets/AppIcon.ico");
        var settings = App.Services.GetRequiredService<SettingsService>();
        if (presenter != null)
            presenter.IsAlwaysOnTop = settings.Current.AlwaysOnTop;
        AppWindow.Resize(new SizeInt32(settings.Current.WindowWidth, settings.Current.WindowHeight));
        CenterOnScreen();
        UpdateDragRegion();

        AppWindow.Changed += (_, e) =>
        {
            if (!e.DidSizeChange) return;
            var sz = AppWindow.Size;
            int w = Math.Max(sz.Width, MinWidth);
            int h = Math.Max(sz.Height, MinHeight);
            if (w != sz.Width || h != sz.Height)
                AppWindow.Resize(new SizeInt32(w, h));
            UpdateDragRegion();
        };

        // Hide to tray instead of closing, unless Exit was chosen from the tray menu
        AppWindow.Closing += (_, args) =>
        {
            if (_forceExit) return;
            args.Cancel = true;
            AppWindow.Hide();
        };

        NavFrame.Navigate(typeof(SearchPage));

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        var hotkeyService = App.Services.GetRequiredService<HotkeyService>();
        hotkeyService.Initialize(hwnd);
        hotkeyService.HotkeyPressed += OnHotkeyPressed;

        if (!string.IsNullOrEmpty(settings.Current.Hotkey))
            hotkeyService.TryRegister(settings.Current.Hotkey);

        var trayService = App.Services.GetRequiredService<TrayIconService>();
        trayService.Initialize(hwnd);
        trayService.ShowRequested += () => DispatcherQueue.TryEnqueue(ShowApp);
        trayService.ExitRequested += () => DispatcherQueue.TryEnqueue(ExitApp);

        Closed += (_, _) =>
        {
            hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            hotkeyService.Dispose();
            trayService.Dispose();
        };
    }

    private void OnHotkeyPressed() => DispatcherQueue.TryEnqueue(ShowApp);

    private void CenterOnScreen()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        AppWindow.Move(new PointInt32(
            area.X + (area.Width  - AppWindow.Size.Width)  / 2,
            area.Y + (area.Height - AppWindow.Size.Height) / 2));
    }

    private void UpdateDragRegion()
    {
        var hwnd  = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale  = GetDpiForWindow(hwnd) / 96.0;
        int dragPx = (int)Math.Round(DragStripDips * scale);
        var source = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        source.SetRegionRects(
            NonClientRegionKind.Caption,
            [new RectInt32(0, 0, AppWindow.Size.Width, dragPx)]);
    }

    internal void ApplySavedWindowSize()
    {
        var s = App.Services.GetRequiredService<SettingsService>();
        AppWindow.Resize(new SizeInt32(s.Current.WindowWidth, s.Current.WindowHeight));
        CenterOnScreen();
    }

    internal void ApplyAlwaysOnTop()
    {
        if (AppWindow.Presenter is OverlappedPresenter p)
            p.IsAlwaysOnTop = App.Services.GetRequiredService<SettingsService>().Current.AlwaysOnTop;
    }

    internal void ShowApp()
    {
        ApplySavedWindowSize();
        AppWindow.Show();
        if (AppWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized)
            p.Restore();
        Activate();
        if (NavFrame.Content is SearchPage sp)
            sp.FocusSearchBox();
    }

    private void ExitApp()
    {
        _forceExit = true;
        Close();
    }

    public async void NavigateTo(string tag)
    {
        if (await TryInterceptUnsavedChangesAsync(tag == "settings", tag))
            return;

        NavigateDirect(tag);
    }

    private void NavigateDirect(string tag)
    {
        if (tag == "settings")
            NavFrame.Navigate(typeof(SettingsPage));
        else
        {
            NavFrame.Navigate(typeof(SearchPage));
            NavFrame.BackStack.Clear();
        }
    }

    public void NavigateToResource(Guid id)
    {
        NavFrame.Navigate(typeof(ResourceDetailPage), id);
        Activate();
    }

    private async Task<bool> TryInterceptUnsavedChangesAsync(bool targetIsSettings, string? targetTag)
    {
        Page? dirtyPage = null;
        if (NavFrame.Content is ResourceDetailPage rdp && rdp.ViewModel.IsDirty)
            dirtyPage = rdp;
        else if (NavFrame.Content is SettingsPage sp && sp.HasUnsavedChanges && !targetIsSettings)
            dirtyPage = sp;

        if (dirtyPage == null) return false;

        bool confirmed = dirtyPage switch
        {
            ResourceDetailPage rdp2 => await rdp2.ConfirmDiscardAsync(),
            SettingsPage sp2        => await sp2.ConfirmDiscardAsync(),
            _                       => true
        };

        if (!confirmed) return true;

        if (dirtyPage is ResourceDetailPage rdp3)
            rdp3.ViewModel.DiscardChanges();

        NavigateDirect(targetTag ?? "search");
        return true;
    }
}
