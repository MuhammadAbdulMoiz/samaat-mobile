using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Samaat.Core.Battery;
using Samaat.Core.Capabilities;
using Samaat.Core.Commands;
using Samaat.Core.Devices;
using Samaat.Core.NoiseControl;
using Samaat.Mobile.Runtime;
using Samaat.Mobile.Simulation;
using Samaat.Mobile.ViewModels;
using Samaat.Mobile.Views;
using Samaat.Simulator;

namespace Samaat.Mobile.Tests;

/// <summary>The noise control panel: always shown, its layout from the device's options, and greyed out unless Samaat can change it.</summary>
public sealed class NoiseGatingTests
{
    private static readonly NoiseControlOptions Selector = new([NoiseMode.Off, NoiseMode.NoiseCancelling, NoiseMode.Transparency],
        new LevelRange(0, 10), new LevelRange(1, 5), WindReduction: true, ConversationAwareness: true);

    private static (DeviceItemViewModel Item, FakeNoise? Noise, FakeDevice Device) Make(NoiseControlOptions? options, bool connected = true,
        CapabilityAccess access = CapabilityAccess.ReadWrite, bool battery = false)
    {
        var noise = options is null ? null : new FakeNoise(options) { Access = access };
        var device = new FakeDevice(connected, noise, battery ? new FakeBattery() : null);
        return (new DeviceItemViewModel(device, SimulatedMobile.Create(), a => a(), _ => { }, () => { }), noise, device);
    }

    private static ItemsControl FindList(DeviceItemViewModel item, string name)
    {
        var window = new Window { Width = 412, Height = 1800, Content = new DeviceView { DataContext = item } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == name);
    }

    [Fact]
    public void NoFeature_SaysSamaatCantControlItYet()
    {
        var (item, _, _) = Make(null);

        Assert.True(item.IsNoiseUnavailable);
        Assert.Equal("Samaat can't control noise cancelling on this device yet. That needs support for this model's own protocol.", item.NoiseUnavailableText);
        Assert.False(item.CanControlNoise);
    }

    [Fact]
    public void FeatureWithoutModes_SaysTheModelHasNone()
    {
        var (item, _, _) = Make(new NoiseControlOptions([NoiseMode.Off]));

        Assert.True(item.IsNoiseUnavailable);
        Assert.Equal("This model doesn't have noise control.", item.NoiseUnavailableText);
    }

    [Fact]
    public void OnlyNoiseCancelling_IsASingleSwitch()
    {
        var (item, noise, _) = Make(new NoiseControlOptions([NoiseMode.Off, NoiseMode.NoiseCancelling]));

        Assert.True(item.IsNoiseToggle);
        Assert.False(item.IsNoiseSelector);
        item.IsAncOn = true;
        Assert.Equal(NoiseMode.NoiseCancelling, noise!.Current.Mode);
        item.IsAncOn = false;
        Assert.Equal(NoiseMode.Off, noise.Current.Mode);
    }

    [Fact]
    public void SeveralModes_AreASelector_WithTheLevelsOfTheCurrentMode()
    {
        var (item, noise, _) = Make(Selector);

        Assert.True(item.IsNoiseSelector);
        Assert.False(item.ShowAncLevel);
        noise!.Publish(new NoiseControlState(NoiseMode.NoiseCancelling, AncLevel: 7));
        Assert.True(item.ShowAncLevel);
        Assert.False(item.ShowAmbientLevel);
        Assert.Equal(7, item.AncLevel);
        Assert.True(item.HasExtras);
        noise.Publish(new NoiseControlState(NoiseMode.Transparency, AmbientLevel: 3));
        Assert.True(item.ShowAmbientLevel);
    }

    [AvaloniaFact]
    public void Disconnected_DisablesTheRenderedModeList_AndConnectingEnablesIt()
    {
        var (item, _, device) = Make(Selector, connected: false, battery: true);
        var list = FindList(item, "NoiseModeList");

        Assert.False(item.CanControlNoise);
        Assert.False(list.IsEffectivelyEnabled);
        Assert.False(item.RefreshBatteryCommand.CanExecute(null));

        device.SetConnected(true);
        Dispatcher.UIThread.RunJobs();

        Assert.True(item.CanControlNoise);
        Assert.True(list.IsEffectivelyEnabled);
        Assert.True(item.RefreshBatteryCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void ReadOnlyAccess_DisablesTheControls()
    {
        var (item, _, _) = Make(Selector, access: CapabilityAccess.ReadOnly);
        var list = FindList(item, "NoiseModeList");

        Assert.False(item.CanControlNoise);
        Assert.False(list.IsEffectivelyEnabled);
    }

    [Fact]
    public async Task AFailingSetter_ShowsTheError()
    {
        var (item, noise, _) = Make(Selector);
        noise!.Result = CommandResult.Rejected("no");

        await item.NoiseModes.First(m => m.Label == "Transparency").SelectCommand.ExecuteAsync(null);

        Assert.Equal("no", item.ErrorText);
    }

    [Fact]
    public async Task ASlider_SendsItsValue_AndAFailureShowsTheError()
    {
        var (item, noise, _) = Make(Selector);
        noise!.Publish(new NoiseControlState(NoiseMode.NoiseCancelling, AncLevel: 7));

        item.AncLevel = 3;
        await item.PendingLevel;
        Assert.Equal([3], noise.AncSent);

        noise.Result = CommandResult.Rejected("no");
        item.AncLevel = 4;
        await item.PendingLevel;
        Assert.Equal("no", item.ErrorText);
    }

    [Fact]
    public void ADevicePushedUpdate_IsNotSentBackAsACommand()
    {
        var (item, noise, _) = Make(Selector);

        noise!.Publish(new NoiseControlState(NoiseMode.NoiseCancelling, AncLevel: 9, WindReduction: true));

        Assert.Equal(9, item.AncLevel);
        Assert.True(item.WindReduction);
        Assert.Empty(noise.AncSent);
        Assert.Empty(noise.Other);
    }

    [Fact]
    public void TheExtras_GoThroughTheirSetters()
    {
        var (item, noise, _) = Make(Selector);

        item.WindReduction = true;
        item.ConversationAwareness = true;

        Assert.Contains("wind:True", noise!.Other);
        Assert.Contains("conversation:True", noise.Other);
    }

    [Fact]
    public async Task RefreshBattery_AsksTheDevice_AndAFailureIsReported()
    {
        var (item, _, _) = Make(null, battery: true);
        var battery = (FakeBattery)item.Device.GetFeature<IBatteryFeature>()!;

        await item.RefreshBatteryCommand.ExecuteAsync(null);
        Assert.Equal(1, battery.Refreshes);
        Assert.Null(item.ErrorText);

        battery.Throw = true;
        await item.RefreshBatteryCommand.ExecuteAsync(null);
        Assert.Equal("Couldn't refresh the battery", item.ErrorText);
    }

    [AvaloniaFact]
    public async Task EqPresets_AreDisabledWhenNotConnected()
    {
        var runtime = SimulatedMobile.Create();
        await runtime.Registry.RefreshAsync();
        var buds = (SimulatedEarbudDevice)runtime.Registry.Devices.First(d => d.Identity.Name == "Simulated Buds Pro");
        var item = new DeviceItemViewModel(buds, runtime, a => a(), _ => { }, () => { });
        var list = FindList(item, "EqPresetList");
        Assert.True(item.HasEq);

        buds.SetConnected(false);
        Dispatcher.UIThread.RunJobs();

        Assert.False(item.CanControlEq);
        Assert.False(list.IsEffectivelyEnabled);
    }

    [Fact]
    public void WithoutAnEqFeature_TheEqualizerIsUnavailable()
    {
        var (item, _, _) = Make(null);

        Assert.False(item.HasEq);
        Assert.False(item.CanControlEq);
    }

    private sealed class FakeDevice(bool connected, INoiseControlFeature? noise, IBatteryFeature? battery) : IEarbudDevice
    {
        private ConnectionState _connection = connected ? ConnectionState.Connected : ConnectionState.Disconnected;

        public DeviceIdentity Identity { get; } = new("fake", "Fake buds");

        public ConnectionState Connection => _connection;

        public IReadOnlyCollection<IFeature> Features { get; } = new IFeature?[] { noise, battery }.OfType<IFeature>().ToList();

        public event EventHandler? ConnectionChanged;

        public void SetConnected(bool value)
        {
            _connection = value ? ConnectionState.Connected : ConnectionState.Disconnected;
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeBattery : IBatteryFeature
    {
        public int Refreshes { get; private set; }

        public bool Throw { get; set; }

        public IReadOnlySet<BatteryComponent> Components { get; } = new HashSet<BatteryComponent> { BatteryComponent.Left };

        public bool ComponentsAreExhaustive => true;

        public CapabilitySource Source => CapabilitySource.Simulator;

        public Verification Verification => Verification.Verified;

        public BatteryState Current => BatteryState.Empty;

        public event EventHandler<BatteryState>? Changed
        {
            add { }
            remove { }
        }

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            Refreshes++;
            return Throw ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask;
        }
    }

    private sealed class FakeNoise(NoiseControlOptions options) : INoiseControlFeature
    {
        private NoiseControlState _current = new(NoiseMode.Off, options.AncLevel?.Max, options.AmbientLevel?.Min);

        public List<int> AncSent { get; } = [];

        public List<string> Other { get; } = [];

        public CommandResult Result { get; set; } = CommandResult.Confirmed;

        public NoiseControlOptions Options { get; } = options;

        public bool OptionsAreExhaustive => true;

        public CapabilitySource Source => CapabilitySource.Simulator;

        public Verification Verification => Verification.Verified;

        public NoiseControlState Current => _current;

        public CapabilityAccess Access { get; set; } = CapabilityAccess.ReadWrite;

        public event EventHandler<NoiseControlState>? Changed;

        public void Publish(NoiseControlState state)
        {
            _current = state;
            Changed?.Invoke(this, state);
        }

        private Task<CommandResult> Done(Func<NoiseControlState, NoiseControlState> change)
        {
            if (Result.Succeeded)
            {
                Publish(change(_current));
            }

            return Task.FromResult(Result);
        }

        public Task<CommandResult> SetModeAsync(NoiseMode mode, CancellationToken cancellationToken = default) => Done(s => s with { Mode = mode });

        public Task<CommandResult> SetAncLevelAsync(int level, CancellationToken cancellationToken = default)
        {
            AncSent.Add(level);
            return Done(s => s with { AncLevel = level });
        }

        public Task<CommandResult> SetAmbientLevelAsync(int level, CancellationToken cancellationToken = default) => Done(s => s with { AmbientLevel = level });

        public Task<CommandResult> SetWindReductionAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            Other.Add($"wind:{enabled}");
            return Done(s => s with { WindReduction = enabled });
        }

        public Task<CommandResult> SetConversationAwarenessAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            Other.Add($"conversation:{enabled}");
            return Done(s => s with { ConversationAwareness = enabled });
        }
    }
}
