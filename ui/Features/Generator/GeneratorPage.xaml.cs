using KagiBox.Core;
using KagiBox.Features.Vault;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace KagiBox.Features.Generator;

public sealed partial class GeneratorPage : Page
{
    // True until OnNavigatedTo has applied the saved options: the Slider coerces its value to Minimum
    // while the XAML loads, and that must not overwrite the shared length.
    private bool _loading = true;

    public GeneratorPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _loading = true;
        LengthSlider.Value = GeneratorOptions.Length;
        UpperBox.IsChecked = GeneratorOptions.UseUpper;
        LowerBox.IsChecked = GeneratorOptions.UseLower;
        DigitsBox.IsChecked = GeneratorOptions.UseDigits;
        SymbolsBox.IsChecked = GeneratorOptions.UseSymbols;
        AmbiguousBox.IsChecked = GeneratorOptions.ExcludeAmbiguous;
        LengthLabel.Text = $"文字数: {GeneratorOptions.Length}";
        _loading = false;
        await GenerateAsync();
    }

    private void Option_Changed(object sender, RoutedEventArgs e) => _ = ApplyOptionsAsync();

    private void LengthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) =>
        _ = ApplyOptionsAsync();

    private async Task ApplyOptionsAsync()
    {
        if (_loading) return;
        GeneratorOptions.Length = (int)LengthSlider.Value;
        GeneratorOptions.UseUpper = UpperBox.IsChecked == true;
        GeneratorOptions.UseLower = LowerBox.IsChecked == true;
        GeneratorOptions.UseDigits = DigitsBox.IsChecked == true;
        GeneratorOptions.UseSymbols = SymbolsBox.IsChecked == true;
        GeneratorOptions.ExcludeAmbiguous = AmbiguousBox.IsChecked == true;
        LengthLabel.Text = $"文字数: {GeneratorOptions.Length}";
        await GenerateAsync();
    }

    private async void Regenerate_Click(object sender, RoutedEventArgs e) => await GenerateAsync();

    private async Task GenerateAsync()
    {
        try
        {
            var r = await CoreClient.Instance.Call<GenerateResult>("generate_password", GeneratorOptions.ToArgs());
            OutputText.Text = r.Password;
            StrengthBar.Value = Math.Clamp(r.Strength.Score, 1, 3);
            StrengthBar.Foreground = AccountDialog.StrengthBrush(r.Strength.Label);
            StrengthText.Text = $"強度: {r.Strength.LabelText}（推定 {r.Strength.EntropyBits:0.#} bit）";
        }
        catch (Exception e)
        {
            ErrorLog.Write("generate_password", e);
        }
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (OutputText.Text is "" or "--") return;
        await VaultActions.CopyTextAsync(OutputText.Text, sensitive: true,
            $"生成したパスワードをコピーしました（{VaultActions.ClipboardSeconds} 秒後に自動消去）");
    }
}
