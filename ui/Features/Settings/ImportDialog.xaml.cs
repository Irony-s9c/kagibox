using KagiBox.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KagiBox.Features.Settings;

public sealed partial class ImportDialog : ContentDialog
{
    private string? _path;

    public bool Imported { get; private set; }

    public ImportDialog()
    {
        InitializeComponent();
    }

    private void ShowError(string? message)
    {
        ErrorText.Text = message ?? "";
        ErrorText.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void Choose_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await SettingsPage.PickImportFileAsync() is { } path)
            {
                _path = path;
                FileText.Text = Path.GetFileName(path);
                PasswordBox.Focus(FocusState.Programmatic);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("pick import file", ex);
        }
    }

    private async void Import_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            ShowError(null);
            if (_path is null)
            {
                args.Cancel = true;
                ShowError("バックアップファイルを選択してください");
                return;
            }
            if (PasswordBox.Password.Length == 0)
            {
                args.Cancel = true;
                ShowError("マスターパスワードを入力してください");
                return;
            }
            var r = await CoreClient.Instance.Call<OkResult>("import_vault", new { FilePath = _path, MasterPassword = PasswordBox.Password });
            if (!r.Ok)
            {
                args.Cancel = true;
                ShowError(r.Error switch
                {
                    "invalid_password" => "マスターパスワードが正しくありません",
                    "invalid_file" => "KagiBox のバックアップファイルではありません",
                    _ => "インポートできませんでした",
                });
                return;
            }
            Imported = true;
        }
        catch (Exception e)
        {
            ErrorLog.Write("import_vault", e);
            args.Cancel = true;
            ShowError("インポートできませんでした");
        }
        finally
        {
            deferral.Complete();
        }
    }
}
