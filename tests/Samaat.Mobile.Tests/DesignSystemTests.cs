using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;

namespace Samaat.Mobile.Tests;

/// <summary>
/// The desktop's DesignSystemTests for the phone: colours come from the shared Tokens.axaml, shape and type from the
/// shared Metrics.axaml, and the few values the phone needs to differ from Metrics.Mobile.axaml. Styles and views use
/// those keys instead of literals, so a change to a shared token reaches both apps.
/// </summary>
public sealed class DesignSystemTests
{
    /// <summary>The phone's own token dictionary: the one markup file allowed to hold literal values.</summary>
    private const string MobileTokens = "Metrics.Mobile.axaml";

    private static readonly DirectoryInfo Root = FindRoot();

    private static DirectoryInfo FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "Samaat.Mobile.slnx")))
        {
            dir = dir.Parent ?? throw new InvalidOperationException("Samaat.Mobile.slnx not found above the test output");
        }

        return dir;
    }

    private static bool IsSource(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>Every .axaml file in the phone UI, except its token dictionary.</summary>
    public static TheoryData<string> MarkupFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root.FullName, "src", "Samaat.Mobile"), "*.axaml", SearchOption.AllDirectories)
                     .Where(f => IsSource(f) && Path.GetFileName(f) != MobileTokens)
                     .Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(Root.FullName, file).Replace('\\', '/'));
        }

        return data;
    }

    private static IReadOnlyList<string> Violations(string relativePath, Regex pattern) =>
        File.ReadAllLines(Path.Combine(Root.FullName, relativePath))
            .Select((line, i) => (line, number: i + 1))
            .Where(l => pattern.IsMatch(l.line))
            .Select(l => $"{relativePath}:{l.number}: {l.line.Trim()}")
            .ToList();

    private static Regex Attribute(string property, string value) =>
        new($"""(\b{property}="{value}"|Property="{property}"\s+Value="{value}")""", RegexOptions.Compiled);

    [Fact]
    public void ThereIsMarkupToCheck()
    {
        Assert.True(MarkupFiles().Count >= 5);
    }

    [Theory]
    [MemberData(nameof(MarkupFiles))]
    public void Markup_UsesColourTokens_NotHexLiterals(string file)
    {
        Assert.Empty(Violations(file, new Regex("#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?\\b")));
    }

    [Theory]
    [MemberData(nameof(MarkupFiles))]
    public void Markup_UsesTheTypeRamp_NotLiteralFontSizes(string file)
    {
        Assert.Empty(Violations(file, Attribute("FontSize", "[0-9][^\"]*")));
    }

    [Theory]
    [MemberData(nameof(MarkupFiles))]
    public void Markup_UsesElevationTokens_NotLiteralShadows(string file)
    {
        Assert.Empty(Violations(file, Attribute("BoxShadow", "[0-9][^\"]*")));
    }

    /// <summary>
    /// A radius that has a token (shared or phone) must use it. Round shapes, whose radius is half their size, are
    /// geometry rather than style and may stay literal.
    /// </summary>
    [Theory]
    [MemberData(nameof(MarkupFiles))]
    public void Markup_UsesRadiusTokens_WhereOneExists(string file)
    {
        Assert.Empty(Violations(file, Attribute("CornerRadius", "(2|6|7|8|10|12|14)")));
    }

    /// <summary>The phone's 44 px touch target is a token, so every control grows together if it changes.</summary>
    [Theory]
    [MemberData(nameof(MarkupFiles))]
    public void Markup_UsesTheTouchTargetToken(string file)
    {
        Assert.Empty(Violations(file, Attribute("(MinHeight|MinWidth|Width|Height)", "44")));
    }

    /// <summary>Hierarchy comes from size, not weight: no 700 to 900 weights in phone markup (unslop).</summary>
    [Theory]
    [MemberData(nameof(MarkupFiles))]
    public void Markup_UsesWeightsUpTo600(string file)
    {
        Assert.Empty(Violations(file, Attribute("FontWeight", "(Bold|ExtraBold|UltraBold|Black|Heavy|ExtraBlack|UltraBlack|[7-9]00)")));
    }

    /// <summary>
    /// Margins, paddings and gaps sit on the 4 px grid, with a 2 px half step for nudges inside small controls (unslop).
    /// </summary>
    [Theory]
    [MemberData(nameof(MarkupFiles))]
    public void Markup_SpacingIsOnThe4pxGrid(string file)
    {
        var spacing = new Regex(@"(?:\b(?:Margin|Padding|Spacing|RowSpacing|ColumnSpacing)=""|Property=""(?:Margin|Padding|Spacing|RowSpacing|ColumnSpacing)""\s+Value="")([-0-9., ]+)""");
        var offGrid = File.ReadAllLines(Path.Combine(Root.FullName, file))
            .Select((line, i) => (line, number: i + 1))
            .SelectMany(l => spacing.Matches(l.line).Select(m => (l.number, value: m.Groups[1].Value)))
            .Where(m => m.value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(v => Math.Abs(double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)))
                .Any(v => v % 4 != 0 && v != 2))
            .Select(m => $"{file}:{m.number}: {m.value}")
            .ToList();

        Assert.Empty(offGrid);
    }

    [Theory]
    [MemberData(nameof(MarkupFiles))]
    public void Markup_DoesNotUseTheSparkleIcon(string file)
    {
        Assert.Empty(Violations(file, new Regex(@"(Static|Dynamic)Resource Icon\.Sparkle\b")));
    }

    /// <summary>No emoji in anything the phone shows: icons come from the shared Fluent set, words say the rest.</summary>
    [Fact]
    public void PhoneSource_HasNoEmoji()
    {
        var emoji = new Regex(@"[☀-➿]|\uD83C[\uDF00-\uDFFF]|\uD83D[\uDC00-\uDEFF]|\uD83E[\uDD00-\uDEFF]");
        var found = Directory.EnumerateFiles(Path.Combine(Root.FullName, "src", "Samaat.Mobile"), "*.*", SearchOption.AllDirectories)
            .Where(f => IsSource(f) && (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".axaml", StringComparison.Ordinal)))
            .SelectMany(f => Violations(Path.GetRelativePath(Root.FullName, f), emoji))
            .ToList();

        Assert.Empty(found);
    }

    /// <summary>The shared tokens the phone relies on, and its own: each must resolve in both themes.</summary>
    [AvaloniaTheory]
    [InlineData("Samaat.Radius.Hairline")]
    [InlineData("Samaat.Radius.Tile")]
    [InlineData("Samaat.Radius.Panel")]
    [InlineData("Samaat.FontSize.Title")]
    [InlineData("Samaat.Shadow.Overlay")]
    [InlineData("Samaat.Mobile.Size.Touch")]
    [InlineData("Samaat.Mobile.Size.Nav")]
    [InlineData("Samaat.Mobile.Size.OptionRow")]
    [InlineData("Samaat.Mobile.Size.DeviceRow")]
    [InlineData("Samaat.Mobile.Radius.Control")]
    [InlineData("Samaat.Mobile.Radius.Overlay")]
    [InlineData("Samaat.Mobile.Radius.Panel")]
    [InlineData("Samaat.Mobile.FontSize.Label")]
    [InlineData("Samaat.Mobile.FontSize.Meta")]
    [InlineData("Samaat.Mobile.FontSize.Unit")]
    [InlineData("Samaat.Mobile.FontSize.Body")]
    [InlineData("Samaat.Mobile.FontSize.PanelTitle")]
    [InlineData("Samaat.Mobile.FontSize.Figure")]
    [InlineData("Samaat.Mobile.FontSize.Code")]
    public void Tokens_ResolveInBothThemes(string key)
    {
        Assert.True(Application.Current!.TryGetResource(key, ThemeVariant.Dark, out _), key);
        Assert.True(Application.Current!.TryGetResource(key, ThemeVariant.Light, out _), key);
    }

    /// <summary>
    /// A phone token exists only because the phone needs a different value. One that equals the shared token for the
    /// same role is a duplicate and should be replaced by the shared key.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Samaat.Mobile.Size.Touch", "Samaat.Size.Control")]
    [InlineData("Samaat.Mobile.Size.OptionRow", "Samaat.Size.OptionRow")]
    [InlineData("Samaat.Mobile.Radius.Control", "Samaat.Radius.Control")]
    [InlineData("Samaat.Mobile.FontSize.Label", "Samaat.FontSize.Label")]
    [InlineData("Samaat.Mobile.FontSize.Meta", "Samaat.FontSize.Meta")]
    [InlineData("Samaat.Mobile.FontSize.Body", "Samaat.FontSize.Body")]
    [InlineData("Samaat.Mobile.FontSize.PanelTitle", "Samaat.FontSize.PanelTitle")]
    [InlineData("Samaat.Mobile.FontSize.Figure", "Samaat.FontSize.Figure")]
    [InlineData("Samaat.Mobile.FontSize.Code", "Samaat.FontSize.Code")]
    public void PhoneTokens_DifferFromTheSharedTokenForTheSameRole(string phone, string shared)
    {
        Assert.True(Application.Current!.TryGetResource(phone, ThemeVariant.Dark, out var a), phone);
        Assert.True(Application.Current!.TryGetResource(shared, ThemeVariant.Dark, out var b), shared);
        Assert.NotEqual(b, a);
    }

    /// <summary>
    /// The phone merges the shared Metrics.axaml itself, rather than relying on Artwork.axaml merging it for its own use.
    /// </summary>
    [Fact]
    public void TheApp_MergesTheSharedTokensItself()
    {
        var app = File.ReadAllText(Path.Combine(Root.FullName, "src", "Samaat.Mobile", "MobileApp.axaml"));

        Assert.Contains("avares://Samaat.UI/Styles/Tokens.axaml", app);
        Assert.Contains("avares://Samaat.UI/Styles/Metrics.axaml", app);
        Assert.Contains("avares://Samaat.Mobile/Styles/Metrics.Mobile.axaml", app);
    }
}
