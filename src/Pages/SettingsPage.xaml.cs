using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using QuickLink.Services;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;

namespace QuickLink.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsService _settings;
    private CancellationTokenSource? _notifyCts;
    private bool _recordingHotkey;
    private bool _intentRecord;
    private string? _pendingHotkey;

    public bool HasUnsavedChanges =>
        DataFilePathBox.Text.Trim() != _settings.Current.DataFilePath ||
        (_pendingHotkey is not null && _pendingHotkey != _settings.Current.Hotkey) ||
        (int)WindowWidthBox.Value  != _settings.Current.WindowWidth  ||
        (int)WindowHeightBox.Value != _settings.Current.WindowHeight;

    public SettingsPage()
    {
        _settings = App.Services.GetRequiredService<SettingsService>();
        InitializeComponent();
        DataFilePathBox.Text  = _settings.Current.DataFilePath;
        HotkeyBox.Text        = _settings.Current.Hotkey;
        WindowWidthBox.Value  = _settings.Current.WindowWidth;
        WindowHeightBox.Value = _settings.Current.WindowHeight;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DataFilePathBox.Focus(FocusState.Programmatic);

        // Surface startup registration failures so the user knows immediately
        var hotkeyService = App.Services.GetRequiredService<HotkeyService>();
        if (hotkeyService.LastRegisterError is { } err)
            ShowHotkeyError(err);
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

    // Keyboard accelerators are suppressed while a TextBox has focus, so these
    // only fire when some other element is focused.
    private void Escape_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        App.NavigateToSearch();
    }

    private void Backspace_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement(XamlRoot) is TextBox or PasswordBox or RichEditBox or NumberBox) return;
        args.Handled = true;
        App.NavigateToSearch();
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

    // ── Hotkey recording ──────────────────────────────────────────────────────

    private void RecordHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_recordingHotkey)
        {
            // Cancel clicked. LostFocus already fired before this Click event,
            // but we told it to defer (focus moved to RecordButton), so we stop
            // recording here instead.
            StopRecording();
        }
        else
        {
            // Set intent flag first so GotFocus knows this is deliberate.
            // A focused TextBox suppresses page-level keyboard accelerators,
            // which prevents Escape from navigating away mid-recording.
            _intentRecord = true;
            HotkeyBox.Focus(FocusState.Programmatic);
        }
    }

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_recordingHotkey) return;
        if (!_intentRecord) return; // ignore Tab or other accidental focus
        _intentRecord = false;
        _recordingHotkey = true;
        RecordButton.Content = "Cancel";
        HotkeyBox.Text = string.Empty;
        HotkeyBox.PlaceholderText = "Press a key combination…";
        HotkeyErrorText.Visibility = Visibility.Collapsed;
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_recordingHotkey) return;

        // If focus moved to the Cancel button, let its Click handler call
        // StopRecording() — don't act here, otherwise we'd stop and then
        // the Click would immediately start a new recording session.
        if (ReferenceEquals(FocusManager.GetFocusedElement(XamlRoot), RecordButton)) return;

        StopRecording();
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Suppress default TextBox key handling so characters aren't inserted.
        e.Handled = true;

        if (!_recordingHotkey) return;

        var key = e.Key;

        if (key == VirtualKey.Escape)
        {
            StopRecording();
            return;
        }

        // Skip standalone modifier key presses — wait for the non-modifier key
        if (key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
                or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
                or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
                or VirtualKey.LeftWindows or VirtualKey.RightWindows)
            return;

        bool ctrl  = IsKeyDown(VirtualKey.Control);
        bool alt   = IsKeyDown(VirtualKey.Menu);
        bool shift = IsKeyDown(VirtualKey.Shift);
        bool win   = IsKeyDown(VirtualKey.LeftWindows) || IsKeyDown(VirtualKey.RightWindows);

        if (!ctrl && !alt && !shift && !win)
        {
            ShowHotkeyError("Include at least one modifier: Ctrl, Alt, Shift, or Win.");
            return;
        }

        var parts = new List<string>(5);
        if (ctrl)  parts.Add("Ctrl");
        if (alt)   parts.Add("Alt");
        if (shift) parts.Add("Shift");
        if (win)   parts.Add("Win");
        parts.Add(GetKeyName(key));

        _pendingHotkey = string.Join("+", parts);
        HotkeyBox.Text = _pendingHotkey;
        HotkeyErrorText.Visibility = Visibility.Collapsed;
        _recordingHotkey = false;
        RecordButton.Content = "Change";

        // Move focus back to a non-TextBox element so keyboard accelerators
        // (Escape, Back) work normally again.
        RecordButton.Focus(FocusState.Programmatic);
    }

    private void StopRecording()
    {
        _recordingHotkey = false;
        RecordButton.Content = "Change";
        HotkeyBox.Text = _pendingHotkey ?? _settings.Current.Hotkey;
        HotkeyErrorText.Visibility = Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_recordingHotkey) StopRecording();

        var path = DataFilePathBox.Text.Trim();
        if (!string.IsNullOrEmpty(path))
            _settings.Current.DataFilePath = path;

        if (_pendingHotkey is not null && _pendingHotkey != _settings.Current.Hotkey)
        {
            var hotkeyService = App.Services.GetRequiredService<HotkeyService>();
            var error = hotkeyService.TryRegister(_pendingHotkey);
            if (error is not null)
            {
                ShowHotkeyError(error);
                ShowNotification(error, InfoBarSeverity.Error);
                return;
            }
            _settings.Current.Hotkey = _pendingHotkey;
            _pendingHotkey = null;
        }

        int newW = Math.Max(700,  (int)WindowWidthBox.Value);
        int newH = Math.Max(500, (int)WindowHeightBox.Value);
        WindowWidthBox.Value  = newW;
        WindowHeightBox.Value = newH;
        _settings.Current.WindowWidth  = newW;
        _settings.Current.WindowHeight = newH;

        _settings.Save();
        (App.MainWindow as QuickLink.MainWindow)?.ApplySavedWindowSize();
        ShowNotification("Settings saved.");
    }

    private void ShowHotkeyError(string message)
    {
        HotkeyErrorText.Text = message;
        HotkeyErrorText.Visibility = Visibility.Visible;
    }

    private void ShowNotification(string message, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        _notifyCts?.Cancel();
        _notifyCts?.Dispose();
        _notifyCts = new CancellationTokenSource();
        _ = Helpers.NotificationHelper.ShowAsync(StatusBar, message, severity, _notifyCts.Token);
    }

    private static bool IsKeyDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private static string GetKeyName(VirtualKey key) => key switch
    {
        VirtualKey.Space    => "Space",
        VirtualKey.Tab      => "Tab",
        VirtualKey.Enter    => "Enter",
        VirtualKey.Insert   => "Insert",
        VirtualKey.Delete   => "Delete",
        VirtualKey.Home     => "Home",
        VirtualKey.End      => "End",
        VirtualKey.PageUp   => "PageUp",
        VirtualKey.PageDown => "PageDown",
        VirtualKey.Left     => "Left",
        VirtualKey.Right    => "Right",
        VirtualKey.Up       => "Up",
        VirtualKey.Down     => "Down",
        >= VirtualKey.F1 and <= VirtualKey.F12 =>
            $"F{(int)key - (int)VirtualKey.F1 + 1}",
        >= VirtualKey.Number0 and <= VirtualKey.Number9 =>
            ((char)('0' + (int)key - (int)VirtualKey.Number0)).ToString(),
        >= VirtualKey.A and <= VirtualKey.Z => key.ToString(),
        _ => key.ToString()
    };
}
