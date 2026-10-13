using System.Text.Json;
using System.Text.Json.Nodes;
using KagiBox.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace KagiBox.Features.Vault;

/// <summary>
/// Account list: search, filter chips, copy ID / password, open the login page, favorites, and the
/// add / edit dialog.
/// </summary>
public sealed partial class VaultPage : Page
{
    private const string FilterAll = "all";
    private const string FilterFavorite = "favorite";

    private List<Account> _accounts = [];
    private List<AccountItem> _shown = [];
    private string _filter = FilterAll;
    private bool _dialogOpen;

    public VaultPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ReloadAsync();
        FocusSearch();
    }

    public void FocusSearch()
    {
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    public async Task ReloadAsync()
    {
        try
        {
            var node = await CoreClient.Instance.Call("list_accounts");
            // A locked vault answers {"ok": false, ...} instead of a list.
            _accounts = node is JsonArray ? node.Deserialize<List<Account>>(CoreClient.Json) ?? [] : [];
        }
        catch (Exception ex)
        {
            ErrorLog.Write("list_accounts", ex);
            _accounts = [];
        }
        App.Window?.SetAccountCount(_accounts.Count);
        BuildChips();
        Render();
    }

    // ---- filtering ----

    private List<string> Categories() => _accounts
        .Select(a => a.Category.Trim())
        .Where(c => c.Length > 0)
        .Distinct()
        .OrderBy(c => c, StringComparer.CurrentCulture)
        .ToList();

    private void BuildChips()
    {
        var cats = Categories();
        if (_filter.StartsWith("cat:") && !cats.Contains(_filter[4..])) _filter = FilterAll;
        if (_filter == FilterFavorite && !_accounts.Any(a => a.Favorite)) _filter = FilterAll;

        var chips = new List<(string Key, string Label)> { (FilterAll, $"すべて（{_accounts.Count}）") };
        var favorites = _accounts.Count(a => a.Favorite);
        if (favorites > 0) chips.Add((FilterFavorite, $"お気に入り（{favorites}）"));
        chips.AddRange(cats.Select(c => ($"cat:{c}", c)));

        Chips.Children.Clear();
        Chips.Visibility = chips.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (key, label) in chips)
        {
            var chip = new ToggleButton { Content = label, IsChecked = key == _filter, Padding = new Thickness(12, 4, 12, 5) };
            chip.Click += (_, _) =>
            {
                _filter = key;
                foreach (var c in Chips.Children.OfType<ToggleButton>()) c.IsChecked = ReferenceEquals(c, chip);
                Render();
            };
            Chips.Children.Add(chip);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Render();

    private void Render()
    {
        var q = SearchBox.Text.Trim();
        _shown = _accounts
            .Where(a => _filter == FilterAll
                || (_filter == FilterFavorite && a.Favorite)
                || (_filter.StartsWith("cat:") && a.Category.Trim() == _filter[4..]))
            .Where(a => q.Length == 0 || new[] { a.Service, a.Username, a.Url, a.Category }
                .Any(v => v.Contains(q, StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(a => a.Favorite)
            .ThenBy(a => a.Service, StringComparer.CurrentCulture)
            .ThenBy(a => a.Username, StringComparer.CurrentCulture)
            .Select(a => new AccountItem(a))
            .ToList();
        List.ItemsSource = _shown;
        // While searching, the first match is selected so Enter / Shift+Enter visibly act on it.
        List.SelectedIndex = q.Length > 0 && _shown.Count > 0 ? 0 : -1;

        CountText.Text = _accounts.Count == 0 ? "" : _shown.Count == _accounts.Count ? $"{_accounts.Count} 件" : $"{_shown.Count} / {_accounts.Count} 件";
        HintText.Visibility = _accounts.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (_accounts.Count == 0)
        {
            EmptyTitle.Text = "まだアカウントがありません";
            EmptyBody.Text = "「追加」からログイン情報を登録しましょう。\n登録後は、検索して Enter を押すだけでパスワードをコピーできます。";
            EmptyAddButton.Visibility = Visibility.Visible;
        }
        else if (_shown.Count == 0)
        {
            EmptyTitle.Text = "一致するアカウントはありません";
            EmptyBody.Text = q.Length > 0 ? $"「{q}」に一致するアカウントはありません。" : "この絞り込みに当てはまるアカウントはありません。";
            EmptyAddButton.Visibility = Visibility.Collapsed;
        }
        EmptyState.Visibility = _shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- keyboard ----

    private AccountItem? Current => List.SelectedItem as AccountItem ?? _shown.FirstOrDefault();

    private void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter when Current is { } item:
                e.Handled = true;
                _ = IsShiftDown() ? VaultActions.CopyUsernameAsync(item.Account) : VaultActions.CopyPasswordAsync(item.Account);
                break;
            case VirtualKey.Down when _shown.Count > 0:
                e.Handled = true;
                List.SelectedIndex = Math.Max(0, List.SelectedIndex);
                (List.ContainerFromIndex(List.SelectedIndex) as ListViewItem)?.Focus(FocusState.Keyboard);
                break;
            case VirtualKey.Escape when SearchBox.Text.Length > 0:
                e.Handled = true;
                SearchBox.Text = "";
                break;
        }
    }

    private void List_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (List.SelectedItem is not AccountItem item) return;
        switch (e.Key)
        {
            case VirtualKey.Enter:
                // Enter copies; opening the editor is click / F2 (otherwise ItemClick would also fire).
                e.Handled = true;
                _ = IsShiftDown() ? VaultActions.CopyUsernameAsync(item.Account) : VaultActions.CopyPasswordAsync(item.Account);
                break;
            case VirtualKey.F2:
                e.Handled = true;
                _ = EditAsync(item.Account);
                break;
            case VirtualKey.Up when List.SelectedIndex == 0:
                e.Handled = true;
                FocusSearch();
                break;
        }
    }

    private static bool IsShiftDown() =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    // ---- row buttons ----

    private static AccountItem? ItemOf(object sender) => (sender as FrameworkElement)?.DataContext as AccountItem;

    private void CopyPassword_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) _ = VaultActions.CopyPasswordAsync(item.Account);
    }

    private void CopyUser_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) _ = VaultActions.CopyUsernameAsync(item.Account);
    }

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) _ = VaultActions.OpenUrlAsync(item.Account);
    }

    private async void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item) return;
        try
        {
            await CoreClient.Instance.Call("toggle_favorite", new { ItemId = item.Id });
        }
        catch (Exception ex)
        {
            ErrorLog.Write("toggle_favorite", ex);
        }
        await ReloadAsync();
    }

    private void List_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AccountItem item) _ = EditAsync(item.Account);
    }

    private void List_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not AccountItem item) return;
        List.SelectedItem = item;
        var menu = new MenuFlyout();
        void Add(string text, string glyph, Func<Task> action, bool enabled = true)
        {
            var mi = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph }, IsEnabled = enabled };
            mi.Click += (_, _) => _ = action();
            menu.Items.Add(mi);
        }
        Add("パスワードをコピー", "\uE8C8", () => VaultActions.CopyPasswordAsync(item.Account), item.HasPassword);
        Add("ID / メールアドレスをコピー", "\uE77B", () => VaultActions.CopyUsernameAsync(item.Account), item.HasUsername);
        if (item.HasUrl) Add("ログインページを開く", "\uE8A7", () => VaultActions.OpenUrlAsync(item.Account));
        menu.Items.Add(new MenuFlyoutSeparator());
        Add("編集", "\uE70F", () => EditAsync(item.Account));
        menu.ShowAt(e.OriginalSource as FrameworkElement, e.GetPosition(e.OriginalSource as UIElement));
        e.Handled = true;
    }

    // ---- add / edit ----

    public Account? FirstAccount() => _shown.FirstOrDefault()?.Account;

    private void Add_Click(object sender, RoutedEventArgs e) => _ = AddAsync();

    public Task AddAsync() => EditAsync(null);

    public async Task EditAsync(Account? account)
    {
        if (_dialogOpen || Dialogs.IsOpen) return;
        _dialogOpen = true;
        try
        {
            var dialog = new AccountDialog(account, Categories()) { XamlRoot = XamlRoot };
            await dialog.LoadAsync();
            await Dialogs.ShowAsync(dialog);
            if (dialog.Outcome is { } message)
            {
                App.Window?.Notify(InfoBarSeverity.Success, message);
                await ReloadAsync();
            }
        }
        catch (Exception e)
        {
            ErrorLog.Write("account dialog", e);
        }
        finally
        {
            _dialogOpen = false;
        }
    }
}
