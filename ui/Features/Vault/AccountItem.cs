using KagiBox.Core;

namespace KagiBox.Features.Vault;

/// <summary>One row of the account list.</summary>
public sealed class AccountItem(Account account)
{
    public Account Account { get; } = account;

    public string Id => Account.Id;
    public string Initial => Account.Initial;
    public string DisplayName => Account.DisplayName;
    public string DisplayUser => Account.DisplayUser;
    public string Category => Account.Category;
    public bool HasCategory => !string.IsNullOrWhiteSpace(Account.Category);
    public bool HasUsername => !string.IsNullOrEmpty(Account.Username);
    public bool HasPassword => Account.PasswordLength > 0;
    public bool HasUrl => !string.IsNullOrWhiteSpace(Account.Url);
    public bool Favorite => Account.Favorite;
    public string FavoriteGlyph => Account.Favorite ? "\uE735" : "\uE734";
    public string FavoriteTip => Account.Favorite ? "お気に入りから外す" : "お気に入りに追加";
}
