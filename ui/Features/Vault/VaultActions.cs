using KagiBox.Core;
using Microsoft.UI.Xaml.Controls;

namespace KagiBox.Features.Vault;

/// <summary>
/// Copy / open actions shared by the list buttons, the right-click menu and the keyboard shortcuts.
/// Secrets never come to the UI for copying: the core puts them on the clipboard and clears it
/// after <see cref="ClipboardSeconds"/> seconds.
/// </summary>
public static class VaultActions
{
    public static int ClipboardSeconds { get; set; } = 30;

    private static void Notify(InfoBarSeverity severity, string message) => App.Window?.Notify(severity, message);

    public static async Task CopyPasswordAsync(Account acc)
    {
        try
        {
            var r = await CoreClient.Instance.Call<OkResult>("copy_password", new { ItemId = acc.Id });
            if (r.Ok) Notify(InfoBarSeverity.Success, $"{acc.DisplayName} のパスワードをコピーしました（{ClipboardSeconds} 秒後に自動消去）");
            else Notify(InfoBarSeverity.Warning, "パスワードが登録されていません");
        }
        catch (Exception e)
        {
            ErrorLog.Write("copy_password", e);
            Notify(InfoBarSeverity.Error, "コピーできませんでした");
        }
    }

    public static async Task CopyUsernameAsync(Account acc)
    {
        if (string.IsNullOrEmpty(acc.Username))
        {
            Notify(InfoBarSeverity.Warning, "ID が登録されていません");
            return;
        }
        try
        {
            await CoreClient.Instance.Call("copy_text", new { Text = acc.Username });
            Notify(InfoBarSeverity.Success, $"{acc.DisplayName} の ID をコピーしました");
        }
        catch (Exception e)
        {
            ErrorLog.Write("copy_text", e);
            Notify(InfoBarSeverity.Error, "コピーできませんでした");
        }
    }

    public static async Task OpenUrlAsync(Account acc)
    {
        try
        {
            var r = await CoreClient.Instance.Call<OkResult>("open_url", new { Url = acc.Url });
            if (!r.Ok) Notify(InfoBarSeverity.Warning, "この URL は開けません（http / https のみ）");
        }
        catch (Exception e)
        {
            ErrorLog.Write("open_url", e);
            Notify(InfoBarSeverity.Error, "URL を開けませんでした");
        }
    }

    /// <summary>Copies a value that is already shown on screen (e.g. a generated password).</summary>
    public static async Task CopyTextAsync(string text, bool sensitive, string message)
    {
        try
        {
            await CoreClient.Instance.Call("copy_text", new { Text = text, Sensitive = sensitive });
            Notify(InfoBarSeverity.Success, message);
        }
        catch (Exception e)
        {
            ErrorLog.Write("copy_text", e);
            Notify(InfoBarSeverity.Error, "コピーできませんでした");
        }
    }
}
