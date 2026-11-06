using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Samaat.Core.Battery;
using Samaat.Core.Commands;
using Samaat.Core.Devices;
using Samaat.Core.Equalizer;
using Samaat.Core.NoiseControl;
using Samaat.Core.Profiles;
using Avalonia.Media.Imaging;
using Samaat.Handoff.Protocol;
using Samaat.Mobile.Runtime;

namespace Samaat.Mobile.ViewModels;

/// <summary>One pair of earbuds on the phone: the list row and the device page.</summary>
public sealed partial class DeviceItemViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan PhoneLinkTimeout = TimeSpan.FromSeconds(60);

    private readonly MobileRuntime _runtime;
    private readonly Action<Action> _post;
    private readonly Action<string> _onMessage;
    private readonly Action _openComputer;
    private readonly IBatteryFeature? _battery;
    private readonly INoiseControlFeature? _noise;
    private readonly IEqualizerFeature? _eq;
    private readonly DeviceProfile? _profile;
    private string? _lookChoice;
    private ResolvedLook? _look;
    private IReadOnlyList<Bitmap> _partBitmaps = [];

    public DeviceItemViewModel(IEarbudDevice device, MobileRuntime runtime, Action<Action> post, Action<string> onMessage, Action openComputer)
    {
        Device = device;
        _runtime = runtime;
        _post = post;
        _onMessage = onMessage;
        _openComputer = openComputer;
        Address = device.Identity.Details?.Address is { } a ? MessageCodec.NormalizeAddress(a) : null;
        _battery = device.GetFeature<IBatteryFeature>();
        _noise = device.GetFeature<INoiseControlFeature>();
        _eq = device.GetFeature<IEqualizerFeature>();
        _profile = runtime.Profiles.Match(device.Identity);
        _lookChoice = runtime.Looks.Get(device.Identity.Id);

        device.ConnectionChanged += OnChanged;
        _runtime.Computer.Changed += OnChanged;
        _runtime.Computer.Progress += OnProgress;
        _runtime.Alerts.Changed += OnAlertPolicyChanged;
        if (_battery is not null)
        {
            _battery.Changed += OnBatteryChanged;
        }

        if (_noise is not null)
        {
            _noise.Changed += OnNoiseChanged;
        }

        if (_eq is not null)
        {
            _eq.Changed += OnEqChanged;
        }

        Refresh();
        RefreshBattery();
        RefreshNoise();
        RefreshEq();
    }

    public IEarbudDevice Device { get; }

    public string? Address { get; }

    public string Name => Device.Identity.Name;

    public string AccessibleText => $"{Name}, {ConnectionText}, battery {SummaryText}";

    // Connection on this phone

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnect), nameof(ShowDisconnect), nameof(AccessibleText), nameof(CanControlNoise), nameof(CanControlEq))]
    [NotifyCanExecuteChangedFor(nameof(RefreshBatteryCommand))]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial string ConnectionText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnect), nameof(ShowDisconnect))]
    public partial bool IsLinkBusy { get; set; }

    /// <summary>Hidden while the computer has the earbuds: then "Bring to this phone" is the one primary action.</summary>
    public bool ShowConnect => Address is not null && !IsConnected && !IsLinkBusy && !ComputerHasThem;

    public bool ShowDisconnect => Address is not null && IsConnected && !IsLinkBusy;

    [RelayCommand]
    private Task ConnectAsync() => RunLinkAsync(true);

    [RelayCommand]
    private Task DisconnectAsync() => RunLinkAsync(false);

    private async Task RunLinkAsync(bool connect)
    {
        if (Address is null || IsLinkBusy)
        {
            return;
        }

        IsLinkBusy = true;
        ErrorText = null;
        var result = await CommandRunner.RunAsync(
            ct => connect ? _runtime.Audio.ConnectAsync(Address, PhoneLinkTimeout, ct) : _runtime.Audio.DisconnectAsync(Address, PhoneLinkTimeout, ct),
            PhoneLinkTimeout + TimeSpan.FromSeconds(2), _runtime.Time);
        IsLinkBusy = false;
        if (!result.Succeeded)
        {
            ErrorText = result.UserMessage;
        }

        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorText { get; set; }

    public bool HasError => ErrorText is not null;

    private void OnChanged(object? sender, EventArgs e) => _post(Refresh);

    private void OnBatteryChanged(object? sender, BatteryState e) => _post(RefreshBattery);

    private void OnAlertPolicyChanged(object? sender, EventArgs e) => _post(RefreshBattery);

    private void OnNoiseChanged(object? sender, NoiseControlState e) => _post(RefreshNoise);

    private void OnEqChanged(object? sender, int? e) => _post(RefreshEq);

    private void Refresh()
    {
        IsConnected = Device.Connection == ConnectionState.Connected;
        ConnectionText = Device.Connection switch
        {
            ConnectionState.Connected => "Connected to this phone",
            ConnectionState.Connecting => "Connecting…",
            _ => "Not connected to this phone",
        };

        ApplyComputerState();
    }


    public void Dispose()
    {
        ArtworkImage?.Dispose();
        foreach (var bitmap in _partBitmaps)
        {
            bitmap.Dispose();
        }

        Device.ConnectionChanged -= OnChanged;
        _runtime.Computer.Changed -= OnChanged;
        _runtime.Computer.Progress -= OnProgress;
        _runtime.Alerts.Changed -= OnAlertPolicyChanged;
        if (_battery is not null)
        {
            _battery.Changed -= OnBatteryChanged;
        }

        if (_noise is not null)
        {
            _noise.Changed -= OnNoiseChanged;
        }

        if (_eq is not null)
        {
            _eq.Changed -= OnEqChanged;
        }
    }
}
