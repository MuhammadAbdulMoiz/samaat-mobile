using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Samaat.Mobile.Tests;

/// <summary>
/// WCAG AA contrast for the phone, in both themes: the desktop's ContrastTests token pairs (the phone uses the same
/// Tokens.axaml), plus the pairs only the phone draws.
/// </summary>
public sealed class ContrastTests
{
    private static Color Token(string key, ThemeVariant theme)
    {
        Assert.True(Application.Current!.TryGetResource(key, theme, out var value), key);
        return ((ISolidColorBrush)value!).Color;
    }

    private static Color Over(Color top, Color bottom)
    {
        var a = top.A / 255.0;
        byte Mix(byte t, byte b) => (byte)Math.Round(t * a + b * (1 - a));
        return Color.FromRgb(Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B));
    }

    private static double Ratio(Color a, Color b)
    {
        static double L(Color c)
        {
            static double Ch(byte v) => v / 255.0 <= 0.03928 ? v / 255.0 / 12.92 : Math.Pow((v / 255.0 + 0.055) / 1.055, 2.4);
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }

        var (l1, l2) = (L(a), L(b));
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    /// <summary>
    /// The colour pairs the phone adds beyond the shared token checks in the desktop's ContrastTests:
    /// low and critical figures on the page, selected rows and tabs on accent-soft, status bars on their track.
    /// </summary>
    [AvaloniaFact]
    public void PhoneColourPairs_MeetWcagAA_InBothThemes()
    {
        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            Color T(string key) => Token(key, theme);
            var checks = new (string What, Color Fore, Color Back, double Min)[]
            {
                ("Low figure on the page", T("Samaat.Warn"), T("Samaat.Bg"), 4.5),
                ("Critical figure on the page", T("Samaat.Critical"), T("Samaat.Bg"), 4.5),
                ("Low words in a device row", T("Samaat.Warn"), T("Samaat.Surface"), 4.5),
                ("Selected option label", T("Samaat.Text"), Over(T("Samaat.AccentSoft"), T("Samaat.Surface")), 4.5),
                // Muted would be 4.45:1 here in dark mode; selected rows switch their description to the text colour.
                ("Selected option description", T("Samaat.Text"), Over(T("Samaat.AccentSoft"), T("Samaat.Surface")), 4.5),
                ("Selected tab label", T("Samaat.Text"), Over(T("Samaat.AccentSoft"), T("Samaat.Rail")), 4.5),
                ("Unselected tab label", T("Samaat.Muted"), T("Samaat.Rail"), 4.5),
                ("Check mark on a selected row", T("Samaat.Accent"), Over(T("Samaat.AccentSoft"), T("Samaat.Surface")), 3.0),
                ("Low alert pill on the page", T("Samaat.Warn"), Over(T("Samaat.WarnSoft"), T("Samaat.Bg")), 4.5),
                ("Critical alert pill on the page", T("Samaat.Critical"), Over(T("Samaat.CriticalSoft"), T("Samaat.Bg")), 4.5),
                ("Critical pill inside a panel", T("Samaat.Critical"), Over(T("Samaat.CriticalSoft"), T("Samaat.Surface")), 4.5),
                ("Low level bar on its track", T("Samaat.Warn"), T("Samaat.Track"), 3.0),
                ("Critical level bar on its track", T("Samaat.Critical"), T("Samaat.Track"), 3.0),
                ("Battery ring on the page", T("Samaat.Accent"), T("Samaat.Bg"), 3.0),
                ("Icon in an option tile", T("Samaat.Muted"), T("Samaat.Surface2"), 3.0),
            };
            foreach (var (what, fore, back, min) in checks)
            {
                var ratio = Ratio(fore, back);
                Assert.True(ratio >= min, $"{what} ({theme}) is {ratio:0.00}:1, needs {min}:1");
            }
        }
    }

    /// <summary>The desktop's text and graphic pairs (ContrastTests.TextPairs), which the phone draws too.</summary>
    public static TheoryData<string, string, double> SharedPairs => new()
    {
        { "Samaat.Text", "Samaat.Bg", 4.5 },
        { "Samaat.Text", "Samaat.Surface", 4.5 },
        { "Samaat.Text", "Samaat.Surface2", 4.5 },
        { "Samaat.Muted", "Samaat.Surface", 4.5 },
        { "Samaat.Muted", "Samaat.Surface2", 4.5 },
        { "Samaat.Muted", "Samaat.Bg", 4.5 },
        { "Samaat.Muted", "Samaat.Rail", 4.5 },
        { "Samaat.Text", "Samaat.Rail", 4.5 },
        { "Samaat.Text", "Samaat.Key", 4.5 },
        { "Samaat.OnAccent", "Samaat.Accent", 4.5 },
        { "Samaat.Accent", "Samaat.Surface", 4.5 },
        { "Samaat.Warn", "Samaat.Surface", 4.5 },
        { "Samaat.Critical", "Samaat.Surface", 4.5 },
        { "Samaat.Accent", "Samaat.Track", 3.0 },
    };

    [AvaloniaTheory]
    [MemberData(nameof(SharedPairs))]
    public void SharedTokens_MeetWcagAA_InBothThemes(string foreground, string background, double minimum)
    {
        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            var ratio = Ratio(Token(foreground, theme), Token(background, theme));
            Assert.True(ratio >= minimum, $"{foreground} on {background} ({theme}) is {ratio:0.00}:1, needs {minimum}:1");
        }
    }

    /// <summary>The style, not only the colour pair: a selected option's description must actually use the text colour.</summary>
    [AvaloniaFact]
    public void SelectedOptionDescription_UsesTheTextColour()
    {
        var meta = new TextBlock { Text = "No processing", Classes = { "meta" } };
        var window = new Window { Content = new Button { Classes = { "option", "selected" }, Content = meta } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Token("Samaat.Text", ThemeVariant.Dark), ((ISolidColorBrush)meta.Foreground!).Color);
        window.Close();
    }
}
