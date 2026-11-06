using CommunityToolkit.Mvvm.ComponentModel;
using Samaat.Core.Commands;
using Samaat.Core.NoiseControl;
using Samaat.Mobile.Diagnostics;

namespace Samaat.Mobile.ViewModels;

// Noise control beyond the mode: the single on/off switch, the two level sliders and the extras, as on the desktop.
public sealed partial class DeviceItemViewModel
{
    private LatestValueSender<int>? _ancSender;
    private LatestValueSender<int>? _ambientSender;

    // True while the view model copies the device's state into the controls, so that isn't sent back as a command.
    private bool _applying;

    public int AncMin => _noise?.Options.AncLevel?.Min ?? 0;

    public int AncMax => _noise?.Options.AncLevel?.Max ?? 0;

    public int AmbientMin => _noise?.Options.AmbientLevel?.Min ?? 0;

    public int AmbientMax => _noise?.Options.AmbientLevel?.Max ?? 0;

    public bool ShowWind => _noise?.Options.WindReduction == true;

    public bool ShowConversation => _noise?.Options.ConversationAwareness == true;

    public bool HasExtras => ShowWind || ShowConversation;

    /// <summary>The last slider command, so tests can await it.</summary>
    public Task PendingLevel { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    public partial bool IsAncOn { get; set; }

    [ObservableProperty]
    public partial double AncLevel { get; set; }

    [ObservableProperty]
    public partial double AmbientLevel { get; set; }

    [ObservableProperty]
    public partial bool ShowAncLevel { get; set; }

    [ObservableProperty]
    public partial bool ShowAmbientLevel { get; set; }

    [ObservableProperty]
    public partial bool WindReduction { get; set; }

    [ObservableProperty]
    public partial bool ConversationAwareness { get; set; }

    partial void OnIsAncOnChanged(bool value)
    {
        if (!_applying && _noise is { } noise)
        {
            SetAsync(ct => noise.SetModeAsync(value ? NoiseMode.NoiseCancelling : NoiseMode.Off, ct)).Observe("switching noise cancelling");
        }
    }

    partial void OnWindReductionChanged(bool value)
    {
        if (!_applying && _noise is { } noise)
        {
            SetAsync(ct => noise.SetWindReductionAsync(value, ct)).Observe("switching wind noise reduction");
        }
    }

    partial void OnConversationAwarenessChanged(bool value)
    {
        if (!_applying && _noise is { } noise)
        {
            SetAsync(ct => noise.SetConversationAwarenessAsync(value, ct)).Observe("switching conversation awareness");
        }
    }

    partial void OnAncLevelChanged(double value)
    {
        if (!_applying && _noise is { } noise)
        {
            _ancSender ??= new LatestValueSender<int>((v, ct) => noise.SetAncLevelAsync(v, ct));
            PendingLevel = SendLevelAsync(_ancSender, value);
            PendingLevel.Observe("sending the noise cancelling strength");
        }
    }

    partial void OnAmbientLevelChanged(double value)
    {
        if (!_applying && _noise is { } noise)
        {
            _ambientSender ??= new LatestValueSender<int>((v, ct) => noise.SetAmbientLevelAsync(v, ct));
            PendingLevel = SendLevelAsync(_ambientSender, value);
            PendingLevel.Observe("sending the ambient sound level");
        }
    }

    private async Task SendLevelAsync(LatestValueSender<int> sender, double value)
    {
        ErrorText = null;
        // Through CommandRunner, as on the desktop: a hung device times out and says so instead of waiting forever.
        var level = (int)Math.Round(value);
        var result = await CommandRunner.RunAsync(ct => sender.SubmitAsync(level), CommandRunner.DefaultTimeout, _runtime.Time);
        if (!result.Succeeded)
        {
            ErrorText = result.UserMessage;
            RefreshNoise();
        }
    }

    private void ApplyNoiseLevels(INoiseControlFeature noise)
    {
        var state = noise.Current;
        var options = noise.Options;
        _applying = true;
        try
        {
            IsAncOn = state.Mode == NoiseMode.NoiseCancelling;
            AncLevel = state.AncLevel ?? AncMin;
            AmbientLevel = state.AmbientLevel ?? AmbientMin;
            WindReduction = state.WindReduction == true;
            ConversationAwareness = state.ConversationAwareness == true;
            ShowAncLevel = NoiseControlPresentation.ShowsAncLevel(options, state.Mode);
            ShowAmbientLevel = NoiseControlPresentation.ShowsAmbientLevel(options, state.Mode);
        }
        finally
        {
            _applying = false;
        }
    }
}
