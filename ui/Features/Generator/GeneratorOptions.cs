namespace KagiBox.Features.Generator;

/// <summary>Generator settings, shared by the generator page and the generate button of the edit dialog.</summary>
public static class GeneratorOptions
{
    public static int Length { get; set; } = 16;
    public static bool UseUpper { get; set; } = true;
    public static bool UseLower { get; set; } = true;
    public static bool UseDigits { get; set; } = true;
    public static bool UseSymbols { get; set; } = true;
    public static bool ExcludeAmbiguous { get; set; } = true;

    /// <summary>The "options" argument of the core's generate_password.</summary>
    public static object ToArgs() => new
    {
        Options = new
        {
            Length,
            UseUpper,
            UseLower,
            UseDigits,
            UseSymbols,
            ExcludeAmbiguous,
        },
    };
}
