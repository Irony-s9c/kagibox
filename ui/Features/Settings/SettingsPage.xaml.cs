using KagiBox.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;

namespace KagiBox.Features.Settings;

public sealed partial class SettingsPage : Page
{
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _loading = true;
        try
        {
            var settings = await CoreClient.Instance.Call<AppSettings>("get_settings");
            var info = await CoreClient.Instance.Call<AppInfo>("get_app_info");
            IdleLockBox.SelectedItem = IdleLockBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Tag == settings.IdleLockMinutes.ToString()) ?? IdleLockBox.Items[2];
            UpdateToggle.IsOn = settings.CheckUpdatesOnStart;
            VersionText.Text = $"現在のバージョン: v{info.Version}";
            DataDirText.Text = info.DataDir;
            ClipboardText.Text = $"コピーしたパスワードは {info.ClipboardClearSeconds} 秒後に消去します。";
            AutotypeText.Text = $"ブラウザやアプリのログイン画面で ID 欄をクリックし、{info.AutotypeHotkey} を押します。\nアカウントを選ぶと「ID → Tab → パスワード → Enter」を自動入力します。";
            HotkeyShortcut.Text = info.AutotypeHotkey;
            AutotypeStatusText.Text = info.AutotypeAvailable ? "利用可能" : "利用不可";
            AutotypeStatus.Background = (Brush)Application.Current.Resources[info.AutotypeAvailable ? "SystemFillColorSuccessBackgroundBrush" : "SystemFillColorCriticalBackgroundBrush"];
            ToolTipService.SetToolTip(AutotypeStatus, info.AutotypeAvailable ? null : "キーボードフックを登録できませんでした（セキュリティソフト等の影響の可能性）");
        }
        catch (Exception ex)
        {
            ErrorLog.Write("load settings", ex);
        }
        finally
        {
            _loading = false;
        }
    }

    private static void Notify(InfoBarSeverity severity, string message) => App.Window?.Notify(severity, message);

    private async void IdleLockBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || IdleLockBox.SelectedItem is not ComboBoxItem item) return;
        var minutes = int.Parse((string)item.Tag);
        try
        {
            await CoreClient.Instance.Call("update_settings", new { Values = new { IdleLockMinutes = minutes } });
            Notify(InfoBarSeverity.Success, minutes == 0 ? "無操作での自動ロックをオフにしました" : $"{minutes} 分操作がないと自動ロックします");
        }
        catch (Exception ex)
        {
            ErrorLog.Write("update_settings", ex);
        }
    }

    private async void UpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try
        {
            await CoreClient.Instance.Call("update_settings", new { Values = new { CheckUpdatesOnStart = UpdateToggle.IsOn } });
            Notify(InfoBarSeverity.Success, UpdateToggle.IsOn ? "起動時にアップデートを確認します" : "起動時のアップデート確認をオフにしました");
        }
        catch (Exception ex)
        {
            ErrorLog.Write("update_settings", ex);
        }
    }

    private async void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await CoreClient.Instance.Call("open_data_folder");
        }
        catch (Exception ex)
        {
            ErrorLog.Write("open_data_folder", ex);
        }
    }

    private static void AttachToWindow(object picker)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Window!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = "kagibox-backup", SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add("KagiBox バックアップ", [".kagibox"]);
            AttachToWindow(picker);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            var r = await CoreClient.Instance.Call<OkResult>("export_vault", new { FilePath = file.Path });
            if (r.Ok) Notify(InfoBarSeverity.Success, "エクスポートしました（暗号化されたまま保存されています）");
            else Notify(InfoBarSeverity.Error, "エクスポートできませんでした");
        }
        catch (Exception ex)
        {
            ErrorLog.Write("export", ex);
            Notify(InfoBarSeverity.Error, "エクスポートできませんでした");
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ImportDialog { XamlRoot = XamlRoot };
        await Dialogs.ShowAsync(dialog);
        if (dialog.Imported)
        {
            Notify(InfoBarSeverity.Success, "インポートしました。次回からはバックアップのマスターパスワードでアンロックします");
            App.Window?.RefreshAccountCount();
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        CheckUpdateText.Text = "確認中...";
        try
        {
            if (App.Window is { } w) await w.CheckForUpdateAsync(manual: true);
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
            CheckUpdateText.Text = "今すぐ確認";
        }
    }

    /// <summary>Used by <see cref="ImportDialog"/>.</summary>
    internal static async Task<string?> PickImportFileAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add(".kagibox");
        picker.FileTypeFilter.Add(".json");
        AttachToWindow(picker);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}
