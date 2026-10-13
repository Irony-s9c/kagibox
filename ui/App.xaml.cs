using System.Runtime.InteropServices;
using KagiBox.Core;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace KagiBox;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }

    private static Mutex? _single;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            ErrorLog.Write("unhandled", e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // One KagiBox window at a time: a second start brings the running window to the front.
        _single = new Mutex(true, "KagiBox.UI.SingleInstance.v1", out var created);
        if (!created)
        {
            var h = FindWindow(null, "KagiBox");
            if (h != IntPtr.Zero)
            {
                ShowWindow(h, 9); // SW_RESTORE
                SetForegroundWindow(h);
            }
            Exit();
            return;
        }

        ApplyTransparencyPreference();

        Window = new MainWindow();
        if (WindowPlacement.Parse(Environment.GetEnvironmentVariable("KAGIBOX_WINDOW_POS")) is not null)
        {
            // Test runs: show on a secondary monitor without taking the keyboard focus.
            Window.AppWindow.Show(false);
        }
        else
        {
            Window.Activate();
        }
    }

    /// <summary>
    /// With Windows' "Transparency effects" turned off, the glass cards become opaque so text stays
    /// readable. Done before the first window exists so every ThemeResource picks up the swap.
    /// </summary>
    private void ApplyTransparencyPreference()
    {
        try
        {
            if (new UISettings().AdvancedEffectsEnabled) return;
            foreach (var dict in Resources.MergedDictionaries)
            {
                foreach (var theme in dict.ThemeDictionaries.Values.OfType<ResourceDictionary>())
                {
                    if (theme.ContainsKey("FluentCardSolidBrush")) theme["FluentCardBrush"] = theme["FluentCardSolidBrush"];
                    if (theme.ContainsKey("FluentPaneSolidBrush")) theme["FluentPaneBrush"] = theme["FluentPaneSolidBrush"];
                }
            }
        }
        catch (Exception e)
        {
            ErrorLog.Write("transparency preference", e);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? cls, string title);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr h, int cmd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr h);
}
