using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ResourceFinder.Services;
using Windows.Storage.Pickers;

namespace ResourceFinder.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsService _settings;

    public bool HasUnsavedChanges =>
        DataFilePathBox.Text.Trim() != _settings.Current.DataFilePath;

    public SettingsPage()
    {
        _settings = App.Services.GetRequiredService<SettingsService>();
        InitializeComponent();
        DataFilePathBox.Text = _settings.Current.DataFilePath;
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

    private void Back_Click(object sender, RoutedEventArgs e) =>
        App.NavigateToSearch();

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
            DataFilePathBox.Text = file.Path;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var path = DataFilePathBox.Text.Trim();
        if (!string.IsNullOrEmpty(path))
            _settings.Current.DataFilePath = path;
        _settings.Save();
    }
}
