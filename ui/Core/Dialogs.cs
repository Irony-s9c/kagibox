using Microsoft.UI.Xaml.Controls;

namespace KagiBox.Core;

/// <summary>
/// WinUI allows only one ContentDialog at a time; every dialog goes through here so a second one
/// (e.g. the update prompt while the edit dialog is open) is skipped instead of throwing.
/// </summary>
public static class Dialogs
{
    private static ContentDialog? _open;

    public static bool IsOpen => _open is not null;

    public static async Task<ContentDialogResult?> ShowAsync(ContentDialog dialog)
    {
        if (_open is not null) return null;
        _open = dialog;
        try
        {
            return await dialog.ShowAsync();
        }
        catch (Exception e)
        {
            ErrorLog.Write("dialog", e);
            return null;
        }
        finally
        {
            _open = null;
        }
    }

    /// <summary>Closes the open dialog (used when the vault locks).</summary>
    public static void HideOpen() => _open?.Hide();
}
