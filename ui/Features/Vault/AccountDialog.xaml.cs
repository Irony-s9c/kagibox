using KagiBox.Core;
using KagiBox.Features.Generator;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KagiBox.Features.Vault;

/// <summary>Add / edit / delete one account.</summary>
public sealed partial class AccountDialog : ContentDialog
{
    private readonly Account? _account;
    private readonly List<string> _categories;
    private readonly DispatcherQueueTimer _strengthTimer;

    /// <summary>Set when something was saved or deleted: the message to show in a notification.</summary>
    public string? Outcome { get; private set; }

    public AccountDialog(Account? account, List<string> categories)
    {
        InitializeComponent();
        _account = account;
        _categories = categories;
        _strengthTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _strengthTimer.Interval = TimeSpan.FromMilliseconds(150);
        _strengthTimer.IsRepeating = false;
        _strengthTimer.Tick += async (_, _) => await UpdateStrengthAsync();

        Title = account is null ? "アカウントを追加" : "アカウントを編集";
        SecondaryButtonText = account is null ? "" : "削除";
        ServiceBox.Text = account?.Service ?? "";
        CategoryBox.Text = account?.Category ?? "";
        UrlBox.Text = account?.Url ?? "";
        UserBox.Text = account?.Username ?? "";
        FavoriteBox.IsChecked = account?.Favorite ?? false;
        Opened += (_, _) => ServiceBox.Focus(FocusState.Programmatic);
    }

    /// <summary>Fetches the stored password (only this one item is decrypted).</summary>
    public async Task LoadAsync()
    {
        if (_account is null) return;
        var r = await CoreClient.Instance.Call<OkResult>("reveal_password", new { ItemId = _account.Id });
        if (r.Ok) PasswordBox.Text = r.Password ?? "";
        _strengthTimer.Stop();
        await UpdateStrengthAsync();
    }

    private void ShowError(string? message)
    {
        ErrorText.Text = message ?? "";
        ErrorText.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- fields ----

    private void CategoryBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var q = sender.Text.Trim();
        sender.ItemsSource = _categories.Where(c => q.Length == 0 || c.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    /// <summary>Fills the service name from the URL's host while it is still empty.</summary>
    private void UrlBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (ServiceBox.Text.Trim().Length > 0) return;
        var raw = UrlBox.Text.Trim();
        if (raw.Length == 0) return;
        if (Uri.TryCreate(raw.Contains("://") ? raw : "https://" + raw, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
        {
            ServiceBox.Text = uri.Host.StartsWith("www.") ? uri.Host[4..] : uri.Host;
        }
    }

    private void PasswordBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _strengthTimer.Stop();
        _strengthTimer.Start();
    }

    private async Task UpdateStrengthAsync()
    {
        if (PasswordBox.Text.Length == 0)
        {
            StrengthBar.Value = 0;
            StrengthText.Text = "";
            return;
        }
        try
        {
            var s = await CoreClient.Instance.Call<Strength>("estimate_strength", new { Password = PasswordBox.Text });
            ApplyStrength(s);
        }
        catch (Exception e)
        {
            ErrorLog.Write("estimate_strength", e);
        }
    }

    private void ApplyStrength(Strength s)
    {
        StrengthBar.Value = Math.Clamp(s.Score, 1, 3);
        StrengthBar.Foreground = StrengthBrush(s.Label);
        StrengthText.Text = $"強度: {s.LabelText}";
    }

    public static Brush StrengthBrush(string label) => (Brush)Application.Current.Resources[label switch
    {
        "strong" => "SystemFillColorSuccessBrush",
        "medium" => "SystemFillColorCautionBrush",
        _ => "SystemFillColorCriticalBrush",
    }];

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = await CoreClient.Instance.Call<GenerateResult>("generate_password", GeneratorOptions.ToArgs());
            PasswordBox.Text = r.Password;
            ApplyStrength(r.Strength);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("generate_password", ex);
        }
    }

    // ---- save ----

    private async void Save_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            ShowError(null);
            var service = ServiceBox.Text.Trim();
            if (service.Length == 0)
            {
                args.Cancel = true;
                ShowError("サービス名を入力してください");
                ServiceBox.Focus(FocusState.Programmatic);
                return;
            }

            var data = new Dictionary<string, object?>
            {
                ["service"] = service,
                ["username"] = UserBox.Text.Trim(),
                ["password"] = PasswordBox.Text,
                ["url"] = UrlBox.Text.Trim(),
                ["category"] = CategoryBox.Text.Trim(),
                ["favorite"] = FavoriteBox.IsChecked == true,
            };
            var r = _account is null
                ? await CoreClient.Instance.Call<OkResult>("add_account", new { Data = data })
                : await CoreClient.Instance.Call<OkResult>("update_account", new { ItemId = _account.Id, Data = data });
            if (!r.Ok)
            {
                args.Cancel = true;
                ShowError("保存できませんでした。もう一度お試しください。");
                return;
            }
            Outcome = _account is null ? $"{service} を追加しました" : "保存しました";
        }
        catch (Exception e)
        {
            ErrorLog.Write("save account", e);
            args.Cancel = true;
            ShowError("保存できませんでした。もう一度お試しください。");
        }
        finally
        {
            deferral.Complete();
        }
    }

    // ---- delete ----

    private void Delete_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        var user = UserBox.Text.Trim();
        DeleteConfirmText.Text = $"{(ServiceBox.Text.Trim().Length > 0 ? ServiceBox.Text.Trim() : "このアカウント")}{(user.Length > 0 ? $"（{user}）" : "")} を削除します。元に戻せません。";
        DeleteConfirm.Visibility = Visibility.Visible;
    }

    private void DeleteCancel_Click(object sender, RoutedEventArgs e) => DeleteConfirm.Visibility = Visibility.Collapsed;

    private async void DeleteConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (_account is null) return;
        try
        {
            await CoreClient.Instance.Call("delete_account", new { ItemId = _account.Id });
            Outcome = "削除しました";
            Hide();
        }
        catch (Exception ex)
        {
            ErrorLog.Write("delete_account", ex);
            ShowError("削除できませんでした");
        }
    }
}
