namespace KagiBox.Core;

/// <summary>Account metadata from the core's list_accounts (never contains the password).</summary>
public sealed class Account
{
    public string Id { get; set; } = "";
    public string Service { get; set; } = "";
    public string Username { get; set; } = "";
    public string Url { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public bool Favorite { get; set; }
    public int PasswordLength { get; set; }

    public string Initial => string.IsNullOrWhiteSpace(Service) ? "?" : Service.Trim()[..1].ToUpperInvariant();
    public string DisplayName => string.IsNullOrWhiteSpace(Service) ? "（名前なし）" : Service;
    public string DisplayUser => string.IsNullOrWhiteSpace(Username) ? "（ID なし）" : Username;
}

/// <summary>{"ok": bool, "error": "...", "id": "..."} replies from vault methods.</summary>
public sealed class OkResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? Id { get; set; }
    public string? Password { get; set; }
    public bool? Favorite { get; set; }
}

public sealed class Strength
{
    public int Score { get; set; }
    public string Label { get; set; } = "weak";
    public double EntropyBits { get; set; }

    public string LabelText => Label switch { "strong" => "強い", "medium" => "普通", _ => "弱い" };
}

public sealed class GenerateResult
{
    public string Password { get; set; } = "";
    public Strength Strength { get; set; } = new();
}

public sealed class HealthItem
{
    public string Id { get; set; } = "";
    public string Service { get; set; } = "";
    public string Username { get; set; } = "";
    public int PasswordLength { get; set; }
}

public sealed class HealthReport
{
    public bool? Ok { get; set; }
    public int TotalAccounts { get; set; }
    public List<HealthItem> WeakPasswords { get; set; } = [];
    public List<HealthItem> ReusedPasswords { get; set; } = [];
    public int Score { get; set; }
}

public sealed class AppSettings
{
    public int IdleLockMinutes { get; set; } = 5;
    public bool CheckUpdatesOnStart { get; set; } = true;
}

public sealed class AppInfo
{
    public string Version { get; set; } = "";
    public string AutotypeHotkey { get; set; } = "Ctrl+Alt+V";
    public bool AutotypeAvailable { get; set; }
    public int ClipboardClearSeconds { get; set; } = 30;
    public string DataDir { get; set; } = "";
    public bool UpdateCheckAllowed { get; set; } = true;
}

public sealed class UpdateInfo
{
    public bool Available { get; set; }
    public string LatestVersion { get; set; } = "";
    public string CurrentVersion { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public bool? Skipped { get; set; }
}
