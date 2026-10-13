using KagiBox.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KagiBox.Features.Update;

/// <summary>"Update / not now". On update the core downloads the installer (SHA-256 checked) and starts it
/// with /SILENT; the installer restarts KagiBox when it is done.</summary>
public sealed partial class UpdateDialog : ContentDialog
{
    /// <summary>True once the installer has been started: the app must quit so files can be replaced.</summary>
    public bool InstallerStarted { get; private set; }

    public UpdateDialog(UpdateInfo info)
    {
        InitializeComponent();
        VersionText.Text = $"v{info.CurrentVersion} → v{info.LatestVersion}";
        NotesText.Text = info.ReleaseNotes.Trim();
        NotesScroll.Visibility = NotesText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Update_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        IsPrimaryButtonEnabled = false;
        IsSecondaryButtonEnabled = false;
        Busy.Visibility = Visibility.Visible;
        Busy.IsActive = true;
        StatusText.Text = "ダウンロードしています。しばらくお待ちください...";
        try
        {
            var r = await CoreClient.Instance.Call<OkResult>("download_and_apply_update");
            if (r.Ok)
            {
                InstallerStarted = true;
                StatusText.Text = "インストーラーを起動しました。KagiBox は一度終了し、完了後に自動で起動します。";
                return;
            }
            args.Cancel = true;
            StatusText.Text = r.Error switch
            {
                "checksum_mismatch" => "ダウンロードしたファイルが壊れていました。もう一度お試しください。",
                "no_update" => "アップデートが見つかりませんでした。",
                _ => "ダウンロードに失敗しました。ネットワーク接続を確認してください。",
            };
            PrimaryButtonText = "もう一度試す";
        }
        catch (Exception e)
        {
            ErrorLog.Write("download_and_apply_update", e);
            args.Cancel = true;
            StatusText.Text = "アップデートに失敗しました。";
        }
        finally
        {
            Busy.IsActive = false;
            Busy.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = !InstallerStarted;
            deferral.Complete();
        }
    }
}
