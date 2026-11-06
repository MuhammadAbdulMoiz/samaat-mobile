using Samaat.Core.Battery;

namespace Samaat.Mobile.ViewModels;

/// <summary>
/// What the battery panel and the summary figure say for one battery state: the figures in order, the summary (the
/// lowest bud, as on the desktop), the low-battery alert in words, and the caption. A plain function of its inputs.
/// </summary>
public sealed record BatterySummary(
    IReadOnlyList<BatteryCellViewModel> Cells,
    string Label,
    string Text,
    string Value,
    string Unit,
    double Percent,
    bool HasSummary,
    bool IsLow,
    bool IsCritical,
    string? AlertText,
    string Caption)
{
    private static readonly BatteryComponent[] Order = [BatteryComponent.Left, BatteryComponent.Right, BatteryComponent.Case, BatteryComponent.Combined];

    /// <param name="state">The device's battery state, or null when it has no battery feature at all.</param>
    /// <param name="policy">The thresholds and whether a low case raises the alert (the settings' battery-alert choices).</param>
    /// <param name="isConnected">Whether the earbuds are connected to this phone (a disconnected device can't report).</param>
    public static BatterySummary From(BatteryState? state, bool isConnected, BatteryAlertPolicy policy)
    {
        var current = state ?? BatteryState.Empty;
        var cells = Order.Select(current.Get).OfType<BatteryReading>().Select(r => new BatteryCellViewModel(r, policy)).ToList();
        var summary = current.SummaryPercent;

        // The most urgent bud (or the single level) decides the alert; the case only counts when the policy includes it.
        var urgent = cells.Where(c => (c.Component != BatteryComponent.Case || policy.IncludeCase) && (c.IsLow || c.IsCritical))
            .OrderByDescending(c => c.IsCritical).ThenBy(c => c.Percent).FirstOrDefault();

        return new BatterySummary(
            cells,
            Label: current.Get(BatteryComponent.Left)?.Percent is not null || current.Get(BatteryComponent.Right)?.Percent is not null ? "Lowest bud" : "Battery",
            Text: summary is { } p ? $"{p}%" : "–",
            Value: summary?.ToString() ?? "–",
            Unit: summary is null ? "" : "%",
            Percent: summary ?? 0,
            HasSummary: summary is not null,
            IsLow: urgent is { IsCritical: false },
            IsCritical: urgent?.IsCritical == true,
            AlertText: urgent is null ? null : $"{urgent.Label} {(urgent.IsCritical ? "critically low" : "low")} · {urgent.Value}%",
            Caption: CaptionFor(state is not null, cells, isConnected));
    }

    /// <summary>
    /// What the panel can and can't show, naming every piece without a level (the desktop's caption rule):
    /// "No level from the right bud yet, and the case isn't reported by this device".
    /// </summary>
    private static string CaptionFor(bool hasBattery, IReadOnlyList<BatteryCellViewModel> cells, bool isConnected)
    {
        if (!hasBattery)
        {
            return "This device reports no battery level";
        }

        var pieces = cells.Where(c => c.Component is BatteryComponent.Left or BatteryComponent.Right or BatteryComponent.Case).ToList();
        if (pieces.Count == 0)
        {
            return cells.Any(c => c.HasLevel) ? "Left, right and case aren't reported for this device"
                : isConnected ? "Waiting for a reading"
                : "Connect the earbuds to read their battery";
        }

        static string Name(BatteryComponent c) => c switch
        {
            BatteryComponent.Left => "left bud",
            BatteryComponent.Right => "right bud",
            _ => "case",
        };

        var missing = new[] { BatteryComponent.Left, BatteryComponent.Right, BatteryComponent.Case }
            .Where(c => pieces.All(p => p.Component != c)).Select(Name).ToList();
        var noLevel = pieces.Where(p => !p.HasLevel).Select(p => Name(p.Component)).ToList();
        var notReported = missing.Count > 0 ? $"the {string.Join(" and ", missing)} {(missing.Count == 1 ? "isn't" : "aren't")} reported by this device" : null;
        var waiting = noLevel.Count > 0 ? $"No level from the {string.Join(" and ", noLevel)} yet" : null;
        return (waiting, notReported) switch
        {
            ({ } w, { } n) => $"{w}, and {n}",
            ({ } w, null) => w,
            (null, { } n) => char.ToUpperInvariant(n[0]) + n[1..],
            _ => "Each level is reported by the earbuds",
        };
    }
}
