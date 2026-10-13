using System.Text.Json;
using System.Text.Json.Nodes;
using KagiBox.Core;
using KagiBox.Features.Vault;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace KagiBox.Features.Health;

/// <summary>Reused and short passwords. Runs every time the page opens.</summary>
public sealed partial class HealthPage : Page
{
    private List<Account> _accounts = [];

    public HealthPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await RunAsync();
    }

    private async void Run_Click(object sender, RoutedEventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        try
        {
            var report = await CoreClient.Instance.Call<HealthReport>("run_health_check");
            if (report.Ok == false) return;
            var list = await CoreClient.Instance.Call("list_accounts");
            _accounts = list is JsonArray ? list.Deserialize<List<Account>>(CoreClient.Json) ?? [] : [];
            Show(report);
        }
        catch (Exception e)
        {
            ErrorLog.Write("run_health_check", e);
            SummaryText.Text = "診断できませんでした";
        }
    }

    private void Show(HealthReport r)
    {
        ScoreRing.Value = r.Score;
        ScoreRing.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
            r.Score >= 80 ? "SystemFillColorSuccessBrush" : r.Score >= 50 ? "SystemFillColorCautionBrush" : "SystemFillColorCriticalBrush"];
        ScoreText.Text = r.TotalAccounts == 0 ? "--" : r.Score.ToString();
        SummaryText.Text = r.TotalAccounts == 0
            ? "アカウントがまだありません"
            : r.Score == 100
                ? $"{r.TotalAccounts} 件すべて問題ありません"
                : $"{r.TotalAccounts} 件中、見直しをおすすめするアカウントがあります";
        Fill(ReusedList, ReusedEmpty, ReusedBadge, r.ReusedPasswords);
        Fill(WeakList, WeakEmpty, WeakBadge, r.WeakPasswords);
    }

    private static void Fill(ListView list, TextBlock empty, InfoBadge badge, List<HealthItem> items)
    {
        list.ItemsSource = items;
        list.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        empty.Visibility = items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        badge.Value = items.Count;
        badge.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Issue_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not HealthItem item || _accounts.FirstOrDefault(a => a.Id == item.Id) is not { } account) return;
        if (Dialogs.IsOpen) return;
        var dialog = new AccountDialog(account, _accounts.Select(a => a.Category).Where(c => c.Length > 0).Distinct().ToList()) { XamlRoot = XamlRoot };
        await dialog.LoadAsync();
        await Dialogs.ShowAsync(dialog);
        if (dialog.Outcome is { } message)
        {
            App.Window?.Notify(InfoBarSeverity.Success, message);
            await RunAsync();
        }
    }
}
