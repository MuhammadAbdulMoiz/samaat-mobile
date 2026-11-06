namespace Samaat.Mobile.Diagnostics;

public sealed record InfoRow(string Label, string Value);

public sealed record LedgerRow(string Feature, string Name, string State, string Source, bool Verified, string Access, bool IsEnabled);

public sealed record EventRow(string Time, string Text);

public sealed record CapabilityRow(string Name, string Meta)
{
    public string AccessibleName => $"{Name}, {Meta}";
}

/// <summary>Features of one module that can't be used, with the reason they share.</summary>
public sealed record UnavailableGroup(string Module, int Count, string Reason, IReadOnlyList<CapabilityRow> Items, bool Partial = false)
{
    // "· 5 more features" when some of the module works, so the row doesn't read as "noise control isn't supported".
    public string Title => $"{Module} · {Count} {(Partial ? "more " : "")}{(Count == 1 ? "feature" : "features")}";
}
