using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Samaat.Core.Capabilities;
using Samaat.Core.Commands;
using Samaat.Core.NoiseControl;

namespace Samaat.Mobile.ViewModels;

// Sound: noise control modes and the EQ presets stored on the earbuds. The levels and extras are in DeviceItemViewModel.NoiseLevels.cs.
public sealed partial class DeviceItemViewModel
{
    // Noise control

    public bool HasNoise => _noise is not null && _noise.Options.Modes.Count > 0;

    public NoiseControlLayout NoiseLayout => NoiseControlPresentation.LayoutFor(_noise?.Options);

    public bool IsNoiseUnavailable => NoiseLayout == NoiseControlLayout.None;

    public bool IsNoiseToggle => NoiseLayout == NoiseControlLayout.Toggle;

    public bool IsNoiseSelector => NoiseLayout == NoiseControlLayout.Selector;

    public string NoiseUnavailableText => _noise is null
        ? "Samaat can't control noise cancelling on this device yet. That needs support for this model's own protocol."
        : "This model doesn't have noise control.";

    /// <summary>Connected, and the device lets Samaat change its noise control (not read-only).</summary>
    public bool CanControlNoise => IsConnected && _noise is not null && _noise.Access != CapabilityAccess.ReadOnly;

    public ObservableCollection<OptionViewModel> NoiseModes { get; } = [];

    // EQ

    public bool HasEq => _eq is not null && _eq.Presets.Count > 0;

    public bool CanControlEq => IsConnected && HasEq;

    public ObservableCollection<OptionViewModel> EqPresets { get; } = [];

    private void RefreshNoise()
    {
        NoiseModes.Clear();
        if (_noise is null)
        {
            return;
        }

        foreach (var mode in NoiseControlPresentation.OrderedModes(_noise.Options))
        {
            NoiseModes.Add(new OptionViewModel(
                NoiseControlPresentation.Label(mode),
                NoiseControlPresentation.Description(mode),
                _noise.Current.Mode == mode,
                () => SetAsync(ct => _noise.SetModeAsync(mode, ct)),
                mode switch
                {
                    NoiseMode.NoiseCancelling => "Icon.NoiseCancelling",
                    NoiseMode.Adaptive => "Icon.Adaptive",
                    NoiseMode.Transparency => "Icon.Transparency",
                    _ => "Icon.CircleOff",
                }));
        }

        NoiseStatusText = _noise.Current.Mode == NoiseMode.Off
            ? "Noise control is off"
            : $"{NoiseControlPresentation.Label(_noise.Current.Mode)} is on";
        ApplyNoiseLevels(_noise);
    }

    [ObservableProperty]
    public partial string NoiseStatusText { get; set; } = "";

    private void RefreshEq()
    {
        EqPresets.Clear();
        if (_eq is null)
        {
            return;
        }

        foreach (var preset in _eq.Presets)
        {
            EqPresets.Add(new OptionViewModel(preset.Name, null, _eq.CurrentPresetId == preset.Id, () => SetAsync(ct => _eq.SetPresetAsync(preset.Id, ct))));
        }
    }

    private async Task SetAsync(Func<CancellationToken, Task<CommandResult>> command)
    {
        ErrorText = null;
        var result = await CommandRunner.RunAsync(command, CommandRunner.DefaultTimeout, _runtime.Time);
        if (!result.Succeeded)
        {
            ErrorText = result.UserMessage;
        }

        RefreshNoise();
        RefreshEq();
    }
}
