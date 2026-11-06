using Avalonia;
using Avalonia.Styling;
using Samaat.Core.Settings;

namespace Samaat.Mobile.Runtime;

/// <summary>How the saved theme choice becomes the app's theme variant (the desktop's mapping).</summary>
public static class ThemePolicy
{
    public static ThemeVariant VariantFor(ThemeChoice choice) => choice switch
    {
        ThemeChoice.Light => ThemeVariant.Light,
        ThemeChoice.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    /// <summary>The stored choice, or the default (dark) without a settings file.</summary>
    public static ThemeChoice Stored(SettingsStore? store) => store?.Load().Theme ?? new AppSettings().Theme;

    public static void Apply(ThemeChoice choice)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = VariantFor(choice);
        }
    }
}
