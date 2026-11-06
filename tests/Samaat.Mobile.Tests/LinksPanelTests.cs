using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Samaat.Core.Capabilities;
using Samaat.Core.Commands;
using Samaat.Core.Connections;
using Samaat.Core.Devices;
using Samaat.Mobile.Simulation;
using Samaat.Mobile.ViewModels;
using Samaat.Mobile.Views;

namespace Samaat.Mobile.Tests;

/// <summary>The Devices panel on the device page: multipoint and the hosts the earbuds report.</summary>
public sealed class LinksPanelTests
{
    private static readonly MultipointOptions Options = new(MaxConnections: 2, CanSwitch: true, CanDisconnect: true, CanToggleMultipoint: true);

    private static readonly HostLink[] Hosts =
    [
        new("this-phone", "Pixel", HostKind.Phone, HostLinkState.Active, IsThisHost: true),
        new("pc", "Desk PC", HostKind.Computer, HostLinkState.Connected, IsThisHost: false),
        new("tablet", "Tablet", HostKind.Tablet, HostLinkState.Paired, IsThisHost: false),
    ];

    private static (DeviceItemViewModel Item, FakeMultipoint? Feature, FakeFeatureDevice Device) Make(MultipointOptions? options,
        IReadOnlyList<HostLink>? hosts = null, bool connected = true)
    {
        var feature = options is null ? null : new FakeMultipoint(options, new MultipointState(true, hosts ?? Hosts));
        var device = new FakeFeatureDevice(connected, feature);
        return (new DeviceItemViewModel(device, SimulatedMobile.Create(), a => a(), message => { }, () => { }), feature, device);
    }

    [Fact]
    public void WithoutAMultipointProtocol_SaysOtherDevicesArentShown()
    {
        var (item, _, _) = Make(null);

        Assert.False(item.HasMultipoint);
        Assert.Equal("Other devices aren't shown for these earbuds", item.HostsUnavailableTitle);
        Assert.Equal("Your computer or other devices may still be connected. Manage them on each device.", item.HostsUnavailableText);
        Assert.Empty(item.Hosts);
    }

    [Fact]
    public void Hosts_MarkThisPhone_AndCountConnectedOutOfTheLimit()
    {
        var (item, _, _) = Make(Options);

        Assert.True(item.HasMultipoint);
        Assert.Equal("Up to 2 devices at once", item.LimitText);
        Assert.Equal("2 of 2", item.ConnectedSummary);
        Assert.Equal(3, item.Hosts.Count);
        var thisPhone = item.Hosts.Single(h => h.IsThisHost);
        Assert.Equal("Pixel", thisPhone.Name);
        Assert.Equal("Playing audio", thisPhone.StateText);
        Assert.False(thisPhone.CanDisconnect);
        Assert.True(item.Hosts.Single(h => h.Id == "pc").CanDisconnect);
        Assert.Equal("Connect", item.Hosts.Single(h => h.Id == "tablet").SwitchText);
        Assert.False(item.ShowNoHosts);
        Assert.StartsWith("Samaat can list and switch other devices", item.LinksHelpText);
        Assert.EndsWith("While this phone is disconnected, Samaat can't read or change the earbuds.", item.LinksHelpText);
    }

    [Fact]
    public void NoHosts_ShowTheEmptyState()
    {
        var (item, _, _) = Make(Options, hosts: []);

        Assert.True(item.ShowNoHosts);
        Assert.Empty(item.Hosts);
        Assert.Equal("0 of 2", item.ConnectedSummary);
    }

    [Fact]
    public void WhenDisconnected_TheHostControlsAreDisabled_AndConnectingEnablesThem()
    {
        var (item, _, device) = Make(Options, connected: false);

        Assert.False(item.CanControlHosts);
        Assert.All(item.Hosts, h => Assert.False(h.CanSwitch || h.CanDisconnect));
        Assert.Equal("Connect this phone to switch or disconnect other devices.", item.HostsNote);

        device.SetConnected(true);

        Assert.True(item.CanControlHosts);
        Assert.Null(item.HostsNote);
        Assert.True(item.Hosts.Single(h => h.Id == "pc").CanSwitch);
    }

    [AvaloniaFact]
    public void TheRenderedPanel_IsDisabledWhileDisconnected_AndTheHelpIsClosed()
    {
        var (item, _, device) = Make(Options, connected: false);
        var window = new Window { Width = 412, Height = 2000, Content = new DeviceView { DataContext = item } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var hostList = window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "HostList");
        Assert.False(hostList.IsEffectivelyEnabled);
        Assert.False(window.GetVisualDescendants().OfType<Expander>().Single(e => e.Name == "HowLinksWork").IsExpanded);

        device.SetConnected(true);
        Dispatcher.UIThread.RunJobs();

        Assert.True(hostList.IsEffectivelyEnabled);
    }

    [Fact]
    public async Task ASwitchThatFails_ShowsItsMessageAsTheError()
    {
        var (item, feature, _) = Make(Options);
        feature!.Result = CommandResult.Rejected("Desk PC is out of range or switched off.");

        await item.Hosts.Single(h => h.Id == "pc").SwitchCommand.ExecuteAsync(null);

        Assert.Equal("Desk PC is out of range or switched off.", item.ErrorText);
        Assert.False(item.IsHostsBusy);
    }

    [Fact]
    public async Task ADisconnectThatFails_ShowsItsMessageAsTheError()
    {
        var (item, feature, _) = Make(Options);
        feature!.Result = CommandResult.Rejected("That device isn't connected.");

        await item.Hosts.Single(h => h.Id == "pc").DisconnectCommand.ExecuteAsync(null);

        Assert.Equal("That device isn't connected.", item.ErrorText);
        Assert.Equal(["disconnect:pc"], feature.Calls);
    }

    [Fact]
    public async Task Switch_AndTheMultipointSwitch_GoThroughTheFeature()
    {
        var (item, feature, _) = Make(Options);

        await item.Hosts.Single(h => h.Id == "tablet").SwitchCommand.ExecuteAsync(null);
        item.MultipointEnabled = false;
        await item.PendingHostCommand;

        Assert.Equal(["switch:tablet", "multipoint:False"], feature!.Calls);
        Assert.Null(item.ErrorText);
    }

    [Fact]
    public void WhenTheEarbudsReportNewHosts_TheListFollows()
    {
        var (item, feature, _) = Make(Options);

        feature!.Publish(new MultipointState(true, [Hosts[0]]));

        Assert.Single(item.Hosts);
        Assert.Equal("1 of 2", item.ConnectedSummary);
    }

    internal sealed class FakeFeatureDevice(bool connected, params IFeature?[] features) : IEarbudDevice
    {
        private ConnectionState _connection = connected ? ConnectionState.Connected : ConnectionState.Disconnected;

        public DeviceIdentity Identity { get; } = new("fake", "Fake buds");

        public ConnectionState Connection => _connection;

        public IReadOnlyCollection<IFeature> Features { get; } = features.OfType<IFeature>().ToList();

        public event EventHandler? ConnectionChanged;

        public void SetConnected(bool value)
        {
            _connection = value ? ConnectionState.Connected : ConnectionState.Disconnected;
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeMultipoint(MultipointOptions options, MultipointState initial) : IMultipointFeature
    {
        public List<string> Calls { get; } = [];

        public CommandResult Result { get; set; } = CommandResult.Confirmed;

        public MultipointOptions Options { get; } = options;

        public CapabilitySource Source => CapabilitySource.Simulator;

        public Verification Verification => Verification.Verified;

        public MultipointState Current { get; private set; } = initial;

        public event EventHandler<MultipointState>? Changed;

        public void Publish(MultipointState state)
        {
            Current = state;
            Changed?.Invoke(this, state);
        }

        public Task<CommandResult> SwitchToAsync(string hostId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"switch:{hostId}");
            return Task.FromResult(Result);
        }

        public Task<CommandResult> DisconnectAsync(string hostId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"disconnect:{hostId}");
            return Task.FromResult(Result);
        }

        public Task<CommandResult> SetMultipointEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            Calls.Add($"multipoint:{enabled}");
            return Task.FromResult(Result);
        }
    }
}
