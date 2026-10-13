namespace KagiBox.Core;

/// <summary>
/// UI-side errors go to the same logs\error.log as the core: %LOCALAPPDATA%\KagiBox\logs
/// (or KAGIBOX_DATA_DIR\logs). Rotated at about 5 MB. Never throws.
/// </summary>
public static class ErrorLog
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly object Gate = new();

    public static string DataDir =>
        Environment.GetEnvironmentVariable("KAGIBOX_DATA_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KagiBox");

    public static void Write(string context, Exception e) => Write(context, $"{e.GetType().Name}: {e.Message}");

    public static void Write(string context, string message)
    {
        try
        {
            var dir = Path.Combine(DataDir, "logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "error.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] ui {context}: {message}{Environment.NewLine}";
            lock (Gate)
            {
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    File.Move(path, path + ".1", overwrite: true);
                }
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
