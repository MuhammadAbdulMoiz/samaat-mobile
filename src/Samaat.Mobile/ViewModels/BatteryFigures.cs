using Samaat.Core.Battery;

namespace Samaat.Mobile.ViewModels;

/// <summary>
/// One battery figure with the desktop's anatomy: label, value and unit apart, a level bar, and a meta line.
/// Low and critical use the desktop's default thresholds (20 % and 10 %) and always say so in words.
/// </summary>
public sealed class BatteryCellViewModel
{
    public const int LowThreshold = 20;
    public const int CriticalThreshold = 10;

    public BatteryCellViewModel(BatteryReading reading)
    {
        Component = reading.Component;
        Label = reading.Component switch
        {
            BatteryComponent.Left => "Left bud",
            BatteryComponent.Right => "Right bud",
            BatteryComponent.Case => "Case",
            _ => "Battery",
        };
        HasLevel = reading.Percent is not null;
        Percent = reading.Percent ?? 0;
        Value = reading.Percent?.ToString() ?? "–";
        Unit = HasLevel ? "%" : "";
        IsCharging = reading.IsCharging;
        IsCritical = HasLevel && Percent <= CriticalThreshold && !IsCharging;
        IsLow = HasLevel && Percent <= LowThreshold && !IsCharging && !IsCritical;
        var state = IsCritical ? "Critical" : IsLow ? "Low" : IsCharging ? "Charging" : reading.Charging == ChargingState.Full ? "Full" : "";
        var where = reading.Status switch
        {
            BudStatus.InCase => "In case",
            BudStatus.Connected => "In use",
            BudStatus.Disconnected => "Not connected",
            _ => "",
        };
        Meta = string.Join(" · ", new[] { state, where }.Where(t => t.Length > 0));
        AccessibleText = HasLevel
            ? $"{Label}, {Value} percent{(state.Length > 0 ? $", {state.ToLowerInvariant()}" : "")}"
            : $"{Label}, no reading";
    }

    public BatteryComponent Component { get; }

    public string Label { get; }

    public string Value { get; }

    public string Unit { get; }

    public double Percent { get; }

    public bool HasLevel { get; }

    public bool IsLow { get; }

    public bool IsCritical { get; }

    public bool IsCharging { get; }

    public string Meta { get; }

    public string AccessibleText { get; }
}

/// <summary>One piece of the device in the artwork (left bud, right bud, case) with its level, as on the desktop rail.</summary>
public sealed class PieceFigureViewModel
{
    public PieceFigureViewModel(BatteryComponent component, BatteryReading? reading)
    {
        var label = component switch
        {
            BatteryComponent.Left => "Left bud",
            BatteryComponent.Right => "Right bud",
            _ => "Case",
        };
        HasLevel = reading?.Percent is not null;
        Value = reading?.Percent?.ToString() ?? "–";
        Unit = HasLevel ? "%" : "";
        IsCharging = reading?.IsCharging == true;
        AccessibleText = HasLevel ? $"{label}, {Value} percent{(IsCharging ? ", charging" : "")}" : $"{label}, not reported";
    }

    public string Value { get; }

    public string Unit { get; }

    public bool HasLevel { get; }

    public bool IsCharging { get; }

    public string AccessibleText { get; }
}
