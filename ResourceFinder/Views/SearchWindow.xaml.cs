using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using ResourceFinder.Pages;
using Windows.Graphics;

namespace ResourceFinder.Views;

public sealed partial class SearchWindow : Window
{
    private const int Width = 620;
    private const int Height = 520;

    public SearchWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Title = "ResourceFinder";

        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        AppWindow.SetPresenter(presenter);

        CenterOnScreen();
    }

    private void CenterOnScreen()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        int x = workArea.X + (workArea.Width - Width) / 2;
        int y = workArea.Y + (workArea.Height - Height) / 3;
        AppWindow.MoveAndResize(new RectInt32(x, y, Width, Height));
    }

    public void ShowAndFocus()
    {
        CenterOnScreen();
        AppWindow.Show();
        Activate();
        if (RootFrame.Content == null)
            RootFrame.Navigate(typeof(SearchPage));
        else if (RootFrame.Content is SearchPage page)
            page.ResetAndFocus();
    }

    public void HideOverlay()
    {
        AppWindow.Hide();
    }
}
