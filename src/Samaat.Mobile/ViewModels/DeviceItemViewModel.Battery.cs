using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Samaat.Core.Battery;
using Samaat.Mobile.Diagnostics;

namespace Samaat.Mobile.ViewModels;

// Battery: the figures, the summary, the low-battery alert and the caption (decided by BatterySummary).
public sealed partial class DeviceItemViewModel
{
    public ObservableCollection<BatteryCellViewModel> Battery { get; } = [];

    public bool HasBattery => Battery.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleText))]
    public partial string SummaryText { get; set; } = "–";

    /// <summary>The hero figure: number and unit apart, like the desktop ("81" "%").</summary>
    [ObservableProperty]
    public partial string SummaryValue { get; set; } = "–";

    [ObservableProperty]
    public partial string SummaryUnit { get; set; } = "";

    [ObservableProperty]
    public partial double SummaryPercent { get; set; }

    [ObservableProperty]
    public partial bool HasSummary { get; set; }

    [ObservableProperty]
    public partial bool SummaryIsLow { get; set; }

    [ObservableProperty]
    public partial bool SummaryIsCritical { get; set; }

    /// <summary>"Right bud low · 17%": low battery is never shown by colour alone (docs/design/ui-audit.md, principle 2).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlert))]
    public partial string? AlertText { get; set; }

    public bool HasAlert => AlertText is not null;

    [ObservableProperty]
    public partial bool AlertIsCritical { get; set; }

    [ObservableProperty]
    public partial string BatteryReasonText { get; set; } = "";

    /// <summary>"Lowest bud" when the summary comes from the buds' own levels, otherwise "Battery".</summary>
    [ObservableProperty]
    public partial string SummaryLabel { get; set; } = "Battery";

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task RefreshBatteryAsync()
    {
        if (_battery is null)
        {
            return;
        }

        // As on the desktop: watch for a reading during the refresh (equal levels still count) and say so when none came.
        var answered = false;
        void OnReading(object? sender, BatteryState state) => answered = true;
        _battery.Changed += OnReading;
        try
        {
            await _battery.RefreshAsync();
        }
        catch (Exception ex)
        {
            MobileLog.Error("refreshing the battery", ex);
            ErrorText = "Couldn't refresh the battery";
            return;
        }
        finally
        {
            _battery.Changed -= OnReading;
        }

        if (!answered)
        {
            _onMessage($"{Name} sent no new battery reading. Samaat shows its next update as soon as it arrives.");
        }
    }

    private void RefreshBattery()
    {
        var state = _battery?.Current;
        var summary = BatterySummary.From(_battery is null ? null : state ?? BatteryState.Empty, IsConnected, _runtime.Alerts.Policy);
        Battery.Clear();
        foreach (var cell in summary.Cells)
        {
            Battery.Add(cell);
        }

        Pieces = [.. new[] { BatteryComponent.Left, BatteryComponent.Right, BatteryComponent.Case }
            .Select(c => new PieceFigureViewModel(c, state?.Get(c)))];
        ApplyLook();

        SummaryLabel = summary.Label;
        SummaryText = summary.Text;
        SummaryValue = summary.Value;
        SummaryUnit = summary.Unit;
        SummaryPercent = summary.Percent;
        HasSummary = summary.HasSummary;
        SummaryIsCritical = summary.IsCritical;
        SummaryIsLow = summary.IsLow;
        AlertIsCritical = summary.IsCritical;
        AlertText = summary.AlertText;
        BatteryReasonText = summary.Caption;
        OnPropertyChanged(nameof(HasBattery));
    }
}
