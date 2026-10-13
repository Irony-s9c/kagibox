using System.Runtime.InteropServices;
using KagiBox.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace KagiBox.Features.Lock;

/// <summary>Unlock screen; on the first run it creates the vault (password + confirmation).</summary>
public sealed partial class LockView : UserControl
{
    private const int MinLength = 8;
    private bool _vaultIsNew;
    private bool _busy;

    public event EventHandler? Unlocked;

    public LockView()
    {
        InitializeComponent();
    }

    public void Reset(bool vaultIsNew)
    {
        _vaultIsNew = vaultIsNew;
        Subtitle.Text = vaultIsNew ? "はじめに、マスターパスワードを決めてください" : "マスターパスワードを入力してください";
        NewNote.Visibility = vaultIsNew ? Visibility.Visible : Visibility.Collapsed;
        ConfirmBox.Visibility = vaultIsNew ? Visibility.Visible : Visibility.Collapsed;
        SubmitText.Text = vaultIsNew ? "保管庫を作成" : "アンロック";
        MasterBox.Password = "";
        ConfirmBox.Password = "";
        ShowError(null);
        UpdateCapsWarning();
        DispatcherQueue.TryEnqueue(() => MasterBox.Focus(FocusState.Programmatic));
#if DEBUG
        // Debug builds only (never shipped): scripts\smoke-test.ps1 unlocks a throwaway test vault this way
        // to take screenshots of the unlocked screens.
        if (!vaultIsNew && Environment.GetEnvironmentVariable("KAGIBOX_TEST_MASTER_PASSWORD") is { Length: > 0 } testPassword)
        {
            MasterBox.Password = testPassword;
            _ = SubmitAsync();
        }
#endif
    }

    private void ShowError(string? message)
    {
        ErrorText.Text = message ?? "";
        ErrorText.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Box_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        if (ReferenceEquals(sender, MasterBox) && _vaultIsNew && ConfirmBox.Password.Length == 0)
        {
            ConfirmBox.Focus(FocusState.Keyboard);
            return;
        }
        _ = SubmitAsync();
    }

    private void Box_KeyUp(object sender, KeyRoutedEventArgs e) => UpdateCapsWarning();

    private void UpdateCapsWarning()
    {
        var capsOn = (GetKeyState(0x14) & 0x0001) != 0; // VK_CAPITAL toggled
        CapsWarning.Visibility = capsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Submit_Click(object sender, RoutedEventArgs e) => _ = SubmitAsync();

    private async Task SubmitAsync()
    {
        if (_busy) return;
        var password = MasterBox.Password;
        ShowError(null);
        if (password.Length == 0)
        {
            MasterBox.Focus(FocusState.Keyboard);
            return;
        }
        if (_vaultIsNew)
        {
            if (password.Length < MinLength)
            {
                ShowError("8 文字以上にしてください");
                return;
            }
            if (password != ConfirmBox.Password)
            {
                ShowError(ConfirmBox.Password.Length == 0 ? "確認のため、もう一度入力してください" : "2 つのパスワードが一致しません");
                ConfirmBox.Focus(FocusState.Keyboard);
                return;
            }
        }

        SetBusy(true);
        try
        {
            var result = await CoreClient.Instance.Call<OkResult>(
                _vaultIsNew ? "create_vault" : "unlock_vault",
                new { MasterPassword = password });
            if (!result.Ok)
            {
                ShowError(result.Error == "too_short" ? "8 文字以上にしてください" : "マスターパスワードが正しくありません");
                MasterBox.Password = "";
                MasterBox.Focus(FocusState.Keyboard);
                return;
            }
            MasterBox.Password = "";
            ConfirmBox.Password = "";
            Unlocked?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            ErrorLog.Write(_vaultIsNew ? "create_vault" : "unlock_vault", e);
            ShowError("処理に失敗しました。もう一度お試しください。");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        SubmitButton.IsEnabled = !busy;
        MasterBox.IsEnabled = !busy;
        ConfirmBox.IsEnabled = !busy;
        SubmitText.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        Busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        Busy.IsActive = busy;
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);
}
