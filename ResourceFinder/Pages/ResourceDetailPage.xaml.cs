using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ResourceFinder.Models;
using ResourceFinder.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace ResourceFinder.Pages;

public sealed partial class ResourceDetailPage : Page
{
    public ManageViewModel ViewModel { get; }
    private CancellationTokenSource? _notifyCts;

    public ResourceDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<ManageViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ManageViewModel.SelectedResource))
                Bindings.Update();
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is Guid id && id == Guid.Empty)
            await ViewModel.AddResourceCommand.ExecuteAsync(null);
        else if (e.Parameter is Guid existingId)
            await ViewModel.LoadSingleAsync(existingId);

        Bindings.Update();
    }

    public async Task<bool> ConfirmDiscardAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Unsaved Changes",
            Content = "You have unsaved changes. If you leave now, your changes will be lost.",
            PrimaryButtonText = "Leave",
            CloseButtonText = "Stay",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void Back_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsDirty)
        {
            if (!await ConfirmDiscardAsync()) return;
            ViewModel.DiscardChanges();
        }
        Frame.GoBack();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.SaveCommand.ExecuteAsync(null);
            App.NavigateToSearch();
        }
        catch (Exception ex)
        {
            ShowNotification($"Save failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedResource == null) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Resource",
            Content = $"Delete \"{ViewModel.SelectedResource.Name}\"? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            await ViewModel.DeleteCommand.ExecuteAsync(null);
            App.NavigateToSearch();
        }
        catch (Exception ex)
        {
            ShowNotification($"Delete failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        var url = NewUrlBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            await ViewModel.AddUrlCommand.ExecuteAsync(url);
            NewUrlBox.Text = string.Empty;
            ShowNotification("URL added.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowNotification($"Could not add URL: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string url)
        {
            var data = new DataPackage();
            data.SetText(url);
            Clipboard.SetContent(data);
            ShowNotification("URL copied to clipboard.", InfoBarSeverity.Success);
        }
    }

    private async void SetCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ResourceUrl urlEntry)
        {
            try
            {
                await ViewModel.SetCurrentUrlCommand.ExecuteAsync(urlEntry);
                ShowNotification("Current URL updated.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowNotification($"Could not update current URL: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void DeprecateUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ResourceUrl urlEntry)
        {
            try
            {
                await ViewModel.DeprecateUrlCommand.ExecuteAsync(urlEntry);
                ShowNotification("URL deprecated.", InfoBarSeverity.Informational);
            }
            catch (Exception ex)
            {
                ShowNotification($"Could not deprecate URL: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void DeleteUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ResourceUrl urlEntry) return;

        var dialog = new ContentDialog
        {
            Title = "Remove URL",
            Content = $"Remove \"{urlEntry.Url}\" from history? This cannot be undone.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            await ViewModel.DeleteUrlCommand.ExecuteAsync(urlEntry);
            ShowNotification("URL removed.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowNotification($"Could not remove URL: {ex.Message}", InfoBarSeverity.Error);
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

    public static Visibility ToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility DeprecatedVis(bool v) => v ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CurrentVis(bool v) => v ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility NotCurrentVis(bool v) => v ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility NotDeprecatedVis(bool v) => v ? Visibility.Collapsed : Visibility.Visible;
    public static string FormatDate(DateTime dt) => dt.ToLocalTime().ToString("yyyy-MM-dd");
}
