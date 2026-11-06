using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Samaat.Core.Commands;
using Samaat.Core.Connections;
using Samaat.Core.Devices;

namespace Samaat.Mobile.ViewModels;

// The Devices panel: multipoint and every host the earbuds report. Ported from the desktop's
// external/samaat.io/src/Samaat.App/Features/Connections/ConnectionsViewModel.cs, with "this phone" for "this computer".
public sealed partial class DeviceItemViewModel
{
    private IMultipointFeature? _multipoint;
    private bool _applyingHosts;

    public bool HasMultipoint => _multipoint is not null;

    public bool CanToggleMultipoint => _multipoint?.Options.CanToggleMultipoint == true;

    public string LimitText => _multipoint is null ? "" : $"Up to {_multipoint.Options.MaxConnections} devices at once";

    /// <summary>Why the multipoint switch is greyed out; null when it can be changed.</summary>
    public string? MultipointDisabledReason => CanToggleMultipoint ? null : "These earbuds don't let Samaat change this";

    public string HostsUnavailableTitle => "Other devices aren't shown for these earbuds";

    public string HostsUnavailableText => "Your computer or other devices may still be connected. Manage them on each device.";

    /// <summary>The limits we always state, behind the "How links work" disclosure.</summary>
    public string LinksHelpText =>
        "Samaat can list and switch other devices only when your earbuds' own protocol allows it. "
        + "To forget a device completely, remove the pairing in that device's Bluetooth settings. "
        + "While this phone is disconnected, Samaat can't read or change the earbuds.";

    /// <summary>The count's tooltip.</summary>
    public string ConnectedSummaryTip => "Devices connected now, out of how many the earbuds allow";

    public ObservableCollection<LinkHostViewModel> Hosts { get; } = [];

    public Task PendingHostCommand { get; private set; } = Task.CompletedTask;

    /// <summary>Hosts and the switch only respond while the earbuds are connected to this phone.</summary>
    public bool CanControlHosts => IsConnected;

    [ObservableProperty]
    public partial bool MultipointEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsHostsBusy { get; set; }

    /// <summary>"2 of 2": hosts connected now, out of how many the earbuds allow in the current mode.</summary>
    [ObservableProperty]
    public partial string ConnectedSummary { get; set; } = "";

    /// <summary>Why the host actions are disabled; null while this phone is connected.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHostsNote))]
    public partial string? HostsNote { get; set; }

    public bool HasHostsNote => HostsNote is not null;

    /// <summary>Multipoint is supported but the earbuds report no hosts at all.</summary>
    [ObservableProperty]
    public partial bool ShowNoHosts { get; set; }

    partial void OnMultipointEnabledChanged(bool value)
    {
        if (!_applyingHosts && _multipoint is { } feature)
        {
            PendingHostCommand = RunHostCommandAsync(ct => feature.SetMultipointEnabledAsync(value, ct));
        }
    }

    private Task SwitchHostAsync(string hostId) =>
        _multipoint is { } feature ? PendingHostCommand = RunHostCommandAsync(ct => feature.SwitchToAsync(hostId, ct)) : Task.CompletedTask;

    private Task DisconnectHostAsync(string hostId) =>
        _multipoint is { } feature ? PendingHostCommand = RunHostCommandAsync(ct => feature.DisconnectAsync(hostId, ct)) : Task.CompletedTask;

    private async Task RunHostCommandAsync(Func<CancellationToken, Task<CommandResult>> command)
    {
        IsHostsBusy = true;
        ErrorText = null;
        var result = await CommandRunner.RunAsync(command, CommandRunner.DefaultTimeout, _runtime.Time);
        IsHostsBusy = false;
        if (!result.Succeeded)
        {
            ErrorText = result.UserMessage;
        }

        ApplyHosts();
    }

    [RelayCommand]
    private void OpenDiagnostics() => _openDiagnostics?.Invoke();

    private void OnMultipointChanged(object? sender, MultipointState state) => _post(ApplyHosts);

    private void ApplyHosts()
    {
        _applyingHosts = true;
        try
        {
            HostsNote = IsConnected ? null : "Connect this phone to switch or disconnect other devices.";
            if (_multipoint is not { } feature)
            {
                return;
            }

            var state = feature.Current;
            MultipointEnabled = state.MultipointEnabled;
            Hosts.Clear();
            foreach (var host in state.Hosts.OrderBy(h => h.State).ThenByDescending(h => h.IsThisHost))
            {
                Hosts.Add(new LinkHostViewModel(host, feature.Options, IsConnected, SwitchHostAsync, DisconnectHostAsync));
            }

            var allowed = state.MultipointEnabled ? feature.Options.MaxConnections : 1;
            ShowNoHosts = Hosts.Count == 0;
            ConnectedSummary = $"{state.Hosts.Count(h => h.IsConnected)} of {allowed}";
        }
        finally
        {
            _applyingHosts = false;
        }
    }
}

/// <summary>One host row on the Devices panel.</summary>
public sealed class LinkHostViewModel
{
    public LinkHostViewModel(HostLink host, MultipointOptions options, bool earbudsReachable, Func<string, Task> switchTo, Func<string, Task> disconnect)
    {
        Id = host.Id;
        Name = host.Name;
        IsThisHost = host.IsThisHost;
        IsActive = host.State == HostLinkState.Active;
        IsConnected = host.IsConnected;
        CanSwitch = earbudsReachable && options.CanSwitch && host.State is HostLinkState.Connected or HostLinkState.Paired;
        CanDisconnect = earbudsReachable && options.CanDisconnect && host.IsConnected && !host.IsThisHost;
        SwitchText = host.State == HostLinkState.Paired ? "Connect" : "Switch here";
        SwitchIconKey = host.State == HostLinkState.Paired ? "Icon.Link" : "Icon.Switch";
        SwitchLabel = host.State == HostLinkState.Paired ? $"Connect {Name}" : $"Switch audio to {Name}";
        DisconnectLabel = $"Disconnect {Name}";
        StateText = host.State switch
        {
            HostLinkState.Active => "Playing audio",
            HostLinkState.Connected => "Connected · standby",
            HostLinkState.Paired => "Not connected",
            _ => "Out of range",
        };
        IconKey = host.Kind switch
        {
            HostKind.Phone => "Icon.Phone",
            HostKind.Tablet => "Icon.Tablet",
            HostKind.Computer => "Icon.Laptop",
            _ => "Icon.Bluetooth",
        };
        SwitchCommand = new AsyncRelayCommand(() => switchTo(Id));
        DisconnectCommand = new AsyncRelayCommand(() => disconnect(Id));
    }

    public string Id { get; }

    public string Name { get; }

    public bool IsThisHost { get; }

    public bool IsActive { get; }

    public bool IsConnected { get; }

    public bool CanSwitch { get; }

    public bool CanDisconnect { get; }

    public string SwitchText { get; }

    public string SwitchIconKey { get; }

    /// <summary>Accessible name for the switch or connect button, naming the host.</summary>
    public string SwitchLabel { get; }

    public string DisconnectLabel { get; }

    public string StateText { get; }

    public string IconKey { get; }

    public IAsyncRelayCommand SwitchCommand { get; }

    public IAsyncRelayCommand DisconnectCommand { get; }
}
