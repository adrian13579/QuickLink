using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ResourceFinder.Models;
using ResourceFinder.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace ResourceFinder.Pages;

public sealed partial class SearchPage : Page
{
    public SearchViewModel ViewModel { get; }
    private CancellationTokenSource? _notifyCts;

    public SearchPage()
    {
        ViewModel = App.Services.GetRequiredService<SearchViewModel>();
        InitializeComponent();
    }

    public void ResetAndFocus()
    {
        ViewModel.SearchText = string.Empty;
        _ = ViewModel.LoadResultsAsync(string.Empty);
        SearchBox.Focus(FocusState.Programmatic);
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.LoadResultsAsync(ViewModel.SearchText);
        SearchBox.Focus(FocusState.Programmatic);
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

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Down && ResultsList.Items.Count > 0)
        {
            ResultsList.Focus(FocusState.Keyboard);
            ResultsList.SelectedIndex = 0;
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
    }

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
            ActivateResult(result);
    }

    private void ActivateResult(SearchResult result) =>
        App.NavigateToManageResource(result.Resource.Id);

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

    private async void ShowNotification(string message, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        _notifyCts?.Cancel();
        _notifyCts?.Dispose();
        _notifyCts = new CancellationTokenSource();
        var token = _notifyCts.Token;

        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;

        try
        {
            await Task.Delay(3000, token);
            StatusBar.IsOpen = false;
        }
        catch (OperationCanceledException) { }
    }

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is SearchResult result && !string.IsNullOrEmpty(result.CurrentUrl))
            _ = Launcher.LaunchUriAsync(new Uri(result.CurrentUrl));
    }
}
