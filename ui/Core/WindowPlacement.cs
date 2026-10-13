namespace KagiBox.Core;

/// <summary>
/// Test runs (scripts\smoke-test.ps1) set KAGIBOX_WINDOW_POS="x,y" so the window opens on a secondary
/// monitor without taking the keyboard focus.
/// </summary>
public static class WindowPlacement
{
    public static (int X, int Y)? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split(',');
        if (parts.Length != 2) return null;
        if (!int.TryParse(parts[0].Trim(), out var x) || !int.TryParse(parts[1].Trim(), out var y)) return null;
        return (x, y);
    }
}
