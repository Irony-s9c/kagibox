using KagiBox.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace KagiBox.Features.Autotype;

/// <summary>Opened by the global hotkey: pick the account to type into the window that had the focus.</summary>
public sealed partial class AutotypeDialog : ContentDialog
{
    private readonly List<Account> _accounts;

    public Account? Picked { get; private set; }

    public AutotypeDialog(List<Account> accounts)
    {
        InitializeComponent();
        _accounts = accounts
            .OrderByDescending(a => a.Favorite)
            .ThenBy(a => a.Service, StringComparer.CurrentCulture)
            .ToList();
        Render();
        Opened += (_, _) => SearchBox.Focus(FocusState.Programmatic);
    }

    private List<Account> Filtered()
    {
        var q = SearchBox.Text.Trim();
        return _accounts.Where(a => q.Length == 0 || new[] { a.Service, a.Username, a.Url, a.Category }
            .Any(v => v.Contains(q, StringComparison.CurrentCultureIgnoreCase))).ToList();
    }

    private void Render()
    {
        var shown = Filtered();
        List.ItemsSource = shown;
        List.SelectedIndex = shown.Count > 0 ? 0 : -1;
        EmptyText.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Render();

    private void Pick(Account? account)
    {
        if (account is null) return;
        Picked = account;
        Hide();
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Pick(List.SelectedItem as Account);
        }
        else if (e.Key == VirtualKey.Down && List.Items.Count > 0)
        {
            e.Handled = true;
            (List.ContainerFromIndex(Math.Max(0, List.SelectedIndex)) as ListViewItem)?.Focus(FocusState.Keyboard);
        }
    }

    private void List_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        Pick(List.SelectedItem as Account);
    }

    private void List_ItemClick(object sender, ItemClickEventArgs e) => Pick(e.ClickedItem as Account);
}
