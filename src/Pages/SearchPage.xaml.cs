using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private CancellationTokenSource? _notifyCts;
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

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Down && ResultsList.Items.Count > 0)
        {
            ResultsList.Focus(FocusState.Keyboard);
            ResultsList.SelectedIndex = 0;
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            App.MainWindow?.AppWindow.Hide();
            e.Handled = true;
        }
    }

    private void ResultsList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ResultsList.SelectedItem is SearchResult result)
        {
            ActivateResult(result);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Up && ResultsList.SelectedIndex == 0)
        {
            SearchBox.Focus(FocusState.Keyboard);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            App.MainWindow?.AppWindow.Hide();
            e.Handled = true;
        }
    }

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
            ActivateResult(result);
    }

    private void PinnedList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
            ActivateResult(result);
    }

    private void ActivateResult(SearchResult result)
    {
        if (string.IsNullOrEmpty(result.CurrentUrl)) return;
        var data = new DataPackage();
        data.SetText(result.CurrentUrl);
        Clipboard.SetContent(data);
        ShowNotification("URL copied to clipboard.", InfoBarSeverity.Success);
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
            ShowNotification("URL copied to clipboard.", InfoBarSeverity.Success);
        }
    }

    private void ShowNotification(string message, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        _notifyCts?.Cancel();
        _notifyCts?.Dispose();
        _notifyCts = new CancellationTokenSource();
        _ = Helpers.NotificationHelper.ShowAsync(StatusBar, message, severity, _notifyCts.Token);
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
}
