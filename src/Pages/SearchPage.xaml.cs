using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using QuickLink.Models;
using QuickLink.Services;
using QuickLink.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace QuickLink.Pages;

public sealed partial class SearchPage : Page
{
    // Attached property — set on the name TextBlock in the DataTemplate to apply match highlights
    public static readonly DependencyProperty HighlightRangesProperty =
        DependencyProperty.RegisterAttached(
            "HighlightRanges",
            typeof(List<HighlightRange>),
            typeof(SearchPage),
            new PropertyMetadata(null, OnHighlightRangesChanged));

    public static void SetHighlightRanges(DependencyObject obj, List<HighlightRange> value) =>
        obj.SetValue(HighlightRangesProperty, value);

    public static List<HighlightRange> GetHighlightRanges(DependencyObject obj) =>
        (List<HighlightRange>)obj.GetValue(HighlightRangesProperty);

    private static void OnHighlightRangesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        tb.TextHighlighters.Clear();
        if (e.NewValue is not List<HighlightRange> { Count: > 0 } ranges) return;
        var highlighter = new TextHighlighter();
        if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var bg) && bg is Brush bgBrush)
            highlighter.Background = bgBrush;
        if (Application.Current.Resources.TryGetValue("TextOnAccentFillColorPrimaryBrush", out var fg) && fg is Brush fgBrush)
            highlighter.Foreground = fgBrush;
        foreach (var r in ranges)
            highlighter.Ranges.Add(new TextRange { StartIndex = r.StartIndex, Length = r.Length });
        tb.TextHighlighters.Add(highlighter);
    }


    public SearchViewModel ViewModel { get; }
    private CancellationTokenSource? _copiedCts;
    private readonly SettingsService _settings;

    public string CurrentHotkey => _settings.Current.Hotkey;

    public SearchPage()
    {
        _settings = App.Services.GetRequiredService<SettingsService>();
        ViewModel = App.Services.GetRequiredService<SearchViewModel>();
        InitializeComponent();
        Loaded += (_, _) => EnsureFocused();
        _settings.Changed += () => DispatcherQueue.TryEnqueue(Bindings.Update);
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.LoadResultsAsync(ViewModel.SearchText);
        Bindings.Update();
    }

    public void FocusSearchBox() => EnsureFocused();

    private void EnsureFocused()
    {
        if (SearchBox.Focus(FocusState.Programmatic)) return;

        // Window not yet active (e.g. initial launch) — wait for activation then focus
        if (App.MainWindow is not { } win) return;
        void OnActivated(object s, WindowActivatedEventArgs a)
        {
            if (a.WindowActivationState == WindowActivationState.Deactivated) return;
            win.Activated -= OnActivated;
            DispatcherQueue.TryEnqueue(() => SearchBox.Focus(FocusState.Programmatic));
        }
        win.Activated += OnActivated;
    }

    public static Style? GetNameStyle(bool isDeprecated) =>
        isDeprecated
            ? Application.Current.Resources.TryGetValue("DeprecatedTextStyle", out var s) ? s as Style : null
            : null;

    public static Visibility DeprecatedVisibility(bool isDeprecated) =>
        isDeprecated ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility BoolToVis(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static string GetInitial(string name) =>
        string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();

    public static string FormatCount(int count) => count == 1 ? "1 result" : $"{count} results";

    public static double GetTagOpacity(bool isMatched) => isMatched ? 1.0 : 0.35;

    public static string GetPinGlyph(bool isPinned) => isPinned ? "" : "";
    public static string GetPinTooltip(bool isPinned) => isPinned ? "Unpin (Ctrl+P)" : "Pin (Ctrl+P)";
    public static Style? GetPinButtonStyle(bool isPinned) =>
        isPinned && Application.Current.Resources.TryGetValue("AccentButtonStyle", out var s) ? s as Style : null;

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Down)
        {
            if (ResultsList.Items.Count > 0)
            {
                ResultsList.Focus(FocusState.Keyboard);
                ResultsList.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (PinnedList.Items.Count > 0)
            {
                PinnedList.Focus(FocusState.Keyboard);
                PinnedList.SelectedIndex = 0;
                e.Handled = true;
            }
        }
    }

    private void ResultsList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ResultsList.SelectedItem is SearchResult result)
        {
            ActivateResult(result, ResultsList.ContainerFromItem(result) as FrameworkElement);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Up && ResultsList.SelectedIndex == 0)
        {
            SearchBox.Focus(FocusState.Keyboard);
            e.Handled = true;
        }
    }

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
            ActivateResult(result, ResultsList.ContainerFromItem(result) as FrameworkElement);
    }

    private void PinnedList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
            ActivateResult(result, PinnedList.ContainerFromItem(result) as FrameworkElement);
    }

    private void PinnedList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && PinnedList.SelectedItem is SearchResult result)
        {
            ActivateResult(result, PinnedList.ContainerFromItem(result) as FrameworkElement);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Up && PinnedList.SelectedIndex == 0)
        {
            SearchBox.Focus(FocusState.Keyboard);
            e.Handled = true;
        }
    }

    private void ActivateResult(SearchResult result, FrameworkElement? container = null)
    {
        if (string.IsNullOrEmpty(result.CurrentUrl)) return;
        var data = new DataPackage();
        data.SetText(result.CurrentUrl);
        Clipboard.SetContent(data);
        // Prefer anchoring to the copy button inside the row so the card appears in the same
        // spot regardless of whether the copy came from a click or a keyboard shortcut.
        var anchor = (container is not null ? FindCopyButton(container) : null)
                     ?? container
                     ?? (FrameworkElement)ResultsList;
        ShowCopiedFlyout(anchor);
    }

    private static FrameworkElement? FindCopyButton(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button { Content: SymbolIcon { Symbol: Symbol.Copy } } btn)
                return btn;
            if (FindCopyButton(child) is { } found)
                return found;
        }
        return null;
    }

    private void ViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is SearchResult result)
            App.NavigateToManageResource(result.Resource.Id);
    }

    private void Settings_Click(object sender, RoutedEventArgs e) =>
        App.NavigateToSettings();

    private void AddResource_Click(object sender, RoutedEventArgs e) =>
        App.NavigateToManageResource(Guid.Empty);

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is SearchResult result && !string.IsNullOrEmpty(result.CurrentUrl))
        {
            var data = new DataPackage();
            data.SetText(result.CurrentUrl);
            Clipboard.SetContent(data);
            ShowCopiedFlyout(btn);
        }
    }

    private void ShowCopiedFlyout(FrameworkElement anchor)
    {
        _copiedCts?.Cancel();
        _copiedCts?.Dispose();
        _copiedCts = new CancellationTokenSource();
        var token = _copiedCts.Token;

        // Local flag shared between the Closed handler and DismissAfterAsync so we can tell
        // whether the flyout was dismissed programmatically (auto-hide / new copy) vs by the
        // user pressing Escape (light-dismiss), in which case we should also hide the window.
        bool programmatic = false;

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(new SymbolIcon { Symbol = Symbol.Accept });
        content.Children.Add(new TextBlock { Text = "Copied!", VerticalAlignment = VerticalAlignment.Center });

        var flyout = new Flyout { Content = content };
        flyout.Closed += (_, _) =>
        {
            if (!programmatic &&
                InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Escape)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                _copiedCts?.Cancel();
                App.MainWindow?.AppWindow.Hide();
            }
        };
        flyout.ShowAt(anchor, new FlyoutShowOptions
        {
            Placement = FlyoutPlacementMode.Left,
            ShowMode = FlyoutShowMode.Transient
        });

        _ = DismissAfterAsync(flyout, token, () => programmatic = true);
    }

    private static async Task DismissAfterAsync(Flyout flyout, CancellationToken token, Action beforeHide)
    {
        try { await Task.Delay(1500, token); }
        catch (OperationCanceledException) { }
        beforeHide();
        flyout.Hide();
    }

    private async void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is SearchResult result)
            await ViewModel.TogglePinAsync(result);
    }

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is SearchResult result && !string.IsNullOrEmpty(result.CurrentUrl))
            _ = Launcher.LaunchUriAsync(new Uri(result.CurrentUrl));
    }

    private void OpenInBrowser_Accelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ResultsList.SelectedItem is SearchResult result && !string.IsNullOrEmpty(result.CurrentUrl))
            _ = Launcher.LaunchUriAsync(new Uri(result.CurrentUrl));
        args.Handled = true;
    }

    private void EditResource_Accelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ResultsList.SelectedItem is SearchResult result)
            App.NavigateToManageResource(result.Resource.Id);
        args.Handled = true;
    }

    private void Escape_Accelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        App.MainWindow?.AppWindow.Hide();
    }

    private async void PinResource_Accelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        var result = ResultsList.SelectedItem as SearchResult ?? PinnedList.SelectedItem as SearchResult;
        if (result is not null)
            await ViewModel.TogglePinAsync(result);
    }
}
