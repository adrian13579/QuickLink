using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ResourceFinder.Pages;
using Windows.Graphics;

namespace ResourceFinder;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth  = 1000;
    private const int DefaultHeight = 700;
    private const int MinWidth      = 700;
    private const int MinHeight     = 500;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(DefaultWidth, DefaultHeight));

        AppWindow.Changed += (_, e) =>
        {
            if (!e.DidSizeChange) return;
            var sz = AppWindow.Size;
            int w = Math.Max(sz.Width, MinWidth);
            int h = Math.Max(sz.Height, MinHeight);
            if (w != sz.Width || h != sz.Height)
                AppWindow.Resize(new SizeInt32(w, h));
        };

        NavFrame.Navigate(typeof(SearchPage));
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
