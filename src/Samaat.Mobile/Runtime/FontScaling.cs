using System.Runtime.CompilerServices;
using Avalonia;

namespace Samaat.Mobile.Runtime;

/// <summary>
/// Android's "Font size" setting (Avalonia 11.3 ignores it): every font size the phone uses is a resource, and this
/// writes each one scaled into the application's resources. Base values are read once, so applying again replaces the
/// scale instead of compounding it.
/// </summary>
public static class FontScaling
{
    public const double MinScale = 0.85;

    public const double MaxScale = 2.0;

    /// <summary>The size resources the phone's markup uses, plus the Fluent theme's own control size.</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        "Samaat.Mobile.FontSize.Label",
        "Samaat.Mobile.FontSize.Meta",
        "Samaat.Mobile.FontSize.Unit",
        "Samaat.Mobile.FontSize.Body",
        "Samaat.Mobile.FontSize.PanelTitle",
        "Samaat.Mobile.FontSize.Figure",
        "Samaat.Mobile.FontSize.Code",
        "Samaat.FontSize.Title",
        "Samaat.FontSize.Heading",
        "ControlContentThemeFontSize",
    ];

    private static readonly ConditionalWeakTable<Application, Dictionary<string, double>> Bases = [];

    public static void Apply(Application app, double scale)
    {
        var clamped = double.IsFinite(scale) ? Math.Clamp(scale, MinScale, MaxScale) : 1.0;
        var bases = Bases.GetValue(app, ReadBases);
        foreach (var (key, value) in bases)
        {
            app.Resources[key] = value * clamped;
        }
    }

    private static Dictionary<string, double> ReadBases(Application app)
    {
        var bases = new Dictionary<string, double>();
        foreach (var key in Keys)
        {
            if (app.TryGetResource(key, app.ActualThemeVariant, out var value) && value is double size)
            {
                bases[key] = size;
            }
        }

        return bases;
    }
}
