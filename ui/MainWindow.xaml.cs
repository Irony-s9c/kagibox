using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using KagiBox.Core;
using KagiBox.Features.Autotype;
using KagiBox.Features.Generator;
using KagiBox.Features.Health;
using KagiBox.Features.Settings;
using KagiBox.Features.Update;
using KagiBox.Features.Vault;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace KagiBox;

public sealed partial class MainWindow : Window
{
    private bool _closing;
    private string _current = "";
    private bool _updateChecked;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "KagiBox.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 760));
        if (WindowPlacement.Parse(Environment.GetEnvironmentVariable("KAGIBOX_WINDOW_POS")) is { } pos)
        {
            AppWindow.Move(new Windows.Graphics.PointInt32(pos.X, pos.Y));
        }
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.PreferredMinimumWidth = 960;
            p.PreferredMinimumHeight = 640;
        }
        AppWindow.Closing += OnClosing;

        var core = CoreClient.Instance;
        core.Event += OnCoreEvent;
        core.Exited += OnCoreExited;
        try
        {
            core.Start(DispatcherQueue);
        }
        catch (Exception e)
        {
            ErrorLog.Write("core start", e);
            ShowFatal($"KagiBox の処理部分（KagiBox-core）を起動できませんでした。\n{e.Message}");
        }
    }

    // ---- lock / unlock ----

    private async Task ShowLockAsync()
    {
        Dialogs.HideOpen();
        Nav.Visibility = Visibility.Collapsed;
        LockState.Visibility = Visibility.Collapsed;
        ContentFrame.Content = null;
        _current = "";
        LockView.Visibility = Visibility.Visible;
        try
        {
            var exists = await CoreClient.Instance.Call<bool>("vault_exists");
            LockView.Reset(vaultIsNew: !exists);
        }
        catch (Exception e)
        {
            ErrorLog.Write("vault_exists", e);
            LockView.Reset(vaultIsNew: false);
        }
    }

    private async void LockView_Unlocked(object? sender, EventArgs e)
    {
        LockView.Visibility = Visibility.Collapsed;
        LockState.Visibility = Visibility.Visible;
        Nav.Visibility = Visibility.Visible;
        _current = "";
        if (ReferenceEquals(Nav.SelectedItem, VaultItem)) Navigate("vault");
        else Nav.SelectedItem = VaultItem;

        try
        {
            var info = await CoreClient.Instance.Call<AppInfo>("get_app_info");
            VaultActions.ClipboardSeconds = info.ClipboardClearSeconds;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("get_app_info", ex);
        }
#if DEBUG
        // Debug builds only: scripts\smoke-test.ps1 -Page opens a screen to take its screenshot.
        var testPage = Environment.GetEnvironmentVariable("KAGIBOX_TEST_PAGE");
        if (!string.IsNullOrEmpty(testPage)) await Task.Delay(500); // let the NavigationView finish its first layout
        switch (testPage)
        {
            case "generator": Nav.SelectedItem = Nav.MenuItems[1]; break;
            case "health": Nav.SelectedItem = Nav.MenuItems[2]; break;
            case "settings": Nav.SelectedItem = Nav.SettingsItem; break;
            case "edit":
                await Task.Delay(800);
                if (ContentFrame.Content is VaultPage vp && vp.FirstAccount() is { } first) _ = vp.EditAsync(first);
                break;
            case "autotype": _ = ShowAutotypePickerAsync(); break;
        }
#endif
        if (!_updateChecked)
        {
            _updateChecked = true;
            await CheckForUpdateAsync(manual: false);
        }
    }

    /// <summary>Startup check (respects the setting and UPDATE_CHECK_ENABLED) or the "check now" button.</summary>
    public async Task CheckForUpdateAsync(bool manual)
    {
        UpdateInfo info;
        try
        {
            info = await CoreClient.Instance.Call<UpdateInfo>("check_for_update", new { Manual = manual });
        }
        catch (Exception e)
        {
            ErrorLog.Write("check_for_update", e);
            if (manual) Notify(InfoBarSeverity.Error, "アップデートを確認できませんでした");
            return;
        }
        if (!info.Available)
        {
            if (manual) Notify(InfoBarSeverity.Success, $"最新バージョン（v{info.CurrentVersion}）を使っています");
            return;
        }
        var dialog = new UpdateDialog(info) { XamlRoot = Content.XamlRoot };
        await Dialogs.ShowAsync(dialog);
        if (dialog.InstallerStarted)
        {
            // Quit so the installer can replace our files; it starts KagiBox again when done.
            _closing = true;
            CoreClient.Instance.CloseInput();
            ExitApp();
        }
    }

    public async void RefreshAccountCount()
    {
        try
        {
            var node = await CoreClient.Instance.Call("list_accounts");
            SetAccountCount(node is JsonArray arr ? arr.Count : 0);
            if (ContentFrame.Content is VaultPage page) await page.ReloadAsync();
        }
        catch (Exception e)
        {
            ErrorLog.Write("list_accounts", e);
        }
    }

    private void AddAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (Nav.Visibility != Visibility.Visible || Dialogs.IsOpen) return;
        args.Handled = true;
        if (!ReferenceEquals(Nav.SelectedItem, VaultItem)) Nav.SelectedItem = VaultItem;
        DispatcherQueue.TryEnqueue(() => _ = (ContentFrame.Content as VaultPage)?.AddAsync());
    }

    // ---- autotype (global hotkey Ctrl+Alt+V, handled by the core) ----

    private async Task ShowAutotypePickerAsync()
    {
        BringToFront();
        if (Nav.Visibility != Visibility.Visible)
        {
            Notify(InfoBarSeverity.Informational, "先にアンロックしてから、もう一度 Ctrl+Alt+V を押してください");
            return;
        }
        if (Dialogs.IsOpen) return;
        List<Account> accounts;
        try
        {
            var node = await CoreClient.Instance.Call("list_accounts");
            accounts = node is JsonArray ? node.Deserialize<List<Account>>(CoreClient.Json) ?? [] : [];
        }
        catch (Exception e)
        {
            ErrorLog.Write("list_accounts", e);
            return;
        }
        var dialog = new AutotypeDialog(accounts) { XamlRoot = Content.XamlRoot };
        await Dialogs.ShowAsync(dialog);
        if (dialog.Picked is not { } picked) return;
        // Get out of the way so the core can focus the original window and type into it.
        if (AppWindow.Presenter is OverlappedPresenter p) p.Minimize();
        await Task.Delay(250);
        try
        {
            await CoreClient.Instance.Call("request_autotype", new { ItemId = picked.Id });
        }
        catch (Exception e)
        {
            ErrorLog.Write("request_autotype", e);
        }
    }

    private void BringToFront()
    {
        if (AppWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized) p.Restore();
        Activate();
        SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr h);

    public async Task LockNowAsync()
    {
        if (LockView.Visibility == Visibility.Visible) return;
        try
        {
            await CoreClient.Instance.Call("lock_vault");
        }
        catch (Exception e)
        {
            ErrorLog.Write("lock_vault", e);
        }
        await ShowLockAsync();
    }

    private async void LockNow_Click(object sender, RoutedEventArgs e) => await LockNowAsync();

    private async void LockAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await LockNowAsync();
    }

    private void SearchAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (Nav.Visibility != Visibility.Visible) return;
        args.Handled = true;
        if (!ReferenceEquals(Nav.SelectedItem, VaultItem)) Nav.SelectedItem = VaultItem;
        DispatcherQueue.TryEnqueue(() => (ContentFrame.Content as VaultPage)?.FocusSearch());
    }

    public void SetAccountCount(int count)
    {
        CountBadge.Value = count;
        CountBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- navigation ----

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var key = args.IsSettingsSelected ? "settings" : (args.SelectedItem as NavigationViewItem)?.Tag as string ?? "vault";
        Navigate(key);
    }

    private void Navigate(string key)
    {
        if (key == _current) return;
        _current = key;
        var transition = new EntranceNavigationTransitionInfo();
        switch (key)
        {
            case "vault":
                ContentFrame.Navigate(typeof(VaultPage), null, transition);
                break;
            case "generator":
                ContentFrame.Navigate(typeof(GeneratorPage), null, transition);
                break;
            case "health":
                ContentFrame.Navigate(typeof(HealthPage), null, transition);
                break;
            case "settings":
                ContentFrame.Navigate(typeof(SettingsPage), null, transition);
                break;
        }
    }

    // ---- notifications ----

    public void Notify(InfoBarSeverity severity, string message)
    {
        var bar = new InfoBar
        {
            Severity = severity,
            Message = message,
            IsOpen = true,
            Background = (Brush)Application.Current.Resources["AcrylicInAppFillColorDefaultBrush"],
        };
        bar.Closed += (_, _) => Toasts.Children.Remove(bar);
        Toasts.Children.Add(bar);
        while (Toasts.Children.Count > 4) Toasts.Children.RemoveAt(0);
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(severity == InfoBarSeverity.Error ? 8 : 4);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => Toasts.Children.Remove(bar);
        timer.Start();
    }

    // ---- core events / lifecycle ----

    private async void OnCoreEvent(string name, JsonNode? payload)
    {
        switch (name)
        {
            case "ready":
                StartupOverlay.Visibility = Visibility.Collapsed;
                await ShowLockAsync();
                break;
            case "auto-locked":
                if (LockView.Visibility != Visibility.Visible)
                {
                    await ShowLockAsync();
                    Notify(InfoBarSeverity.Informational, "自動ロックしました");
                }
                break;
            case "autotype-requested":
                await ShowAutotypePickerAsync();
                break;
            case "fatal":
                var error = (string?)payload?["error"];
                ShowFatal(error == "already_running"
                    ? "KagiBox は既に起動しています（旧バージョンを含む）。開いている KagiBox を使ってください。"
                    : $"KagiBox の処理部分でエラーが起きました（{error}）。");
                break;
        }
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closing || !CoreClient.Instance.IsRunning) return;
        // Let the core wipe the key and the clipboard first; the window closes when it has exited
        // (or after a short grace period).
        args.Cancel = true;
        _closing = true;
        CoreClient.Instance.CloseInput();
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(3);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => ExitApp();
        timer.Start();
    }

    private void OnCoreExited(int code)
    {
        if (_closing)
        {
            ExitApp();
            return;
        }
        if (code == 3) return; // already running: the "fatal" event shows the message
        ErrorLog.Write("core exited", $"exit code {code}");
        ShowFatal($"KagiBox の処理部分が予期せず終了しました（終了コード {code}）。アプリを再起動してください。");
    }

    private void ExitApp()
    {
        _closing = true;
        Close();
        Application.Current.Exit();
    }

    private bool _fatalShown;

    private void ShowFatal(string message)
    {
        if (_fatalShown) return;
        _fatalShown = true;
        if (Content.XamlRoot is null)
        {
            // Too early (still in the constructor): show it once the window content is loaded.
            RoutedEventHandler? onLoaded = null;
            onLoaded = (_, _) =>
            {
                Root.Loaded -= onLoaded;
                _fatalShown = false;
                ShowFatal(message);
            };
            Root.Loaded += onLoaded;
            return;
        }
        DispatcherQueue.TryEnqueue(async () =>
        {
            StartupOverlay.Visibility = Visibility.Collapsed;
            var d = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "KagiBox",
                Content = message,
                CloseButtonText = "閉じる",
            };
            await d.ShowAsync();
            ExitApp();
        });
    }
}
