using Microsoft.UI.Xaml;

namespace KagiBox.Core;

/// <summary>Small helpers for x:Bind function bindings in XAML.</summary>
public static class Ui
{
    public static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Collapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Keeps a button's space (so columns line up) but hides it when it has nothing to act on.</summary>
    public static double Opacity(bool value) => value ? 1.0 : 0.0;

    public static bool Not(bool value) => !value;

    public static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}
