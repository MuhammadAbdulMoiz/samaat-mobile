using Samaat.Core.Battery;
using Samaat.Core.Devices;
using Samaat.Core.Platform;

namespace Samaat.Mobile.Runtime;

/// <summary>
/// Raises a system notification the moment a device's battery crosses the low or critical threshold, even with no
/// screen open (the desktop's alert wording, through <see cref="INotifier"/>). Battery events arrive on any thread and
/// <see cref="LowBatteryDetector"/> is not thread-safe, so every Evaluate and policy change happens under one lock;
/// the notification itself is shown outside it.
/// </summary>
public sealed class BatteryAlertWatcher : IDisposable
{
    private sealed record Watch(IBatteryFeature Feature, EventHandler<BatteryState> Handler);

    private readonly DeviceRegistry _registry;
    private readonly BatteryAlertSettings _alerts;
    private readonly INotifier _notifier;
    private readonly LowBatteryDetector _detector;
    private readonly Dictionary<string, Watch> _watching = [];
    private readonly Lock _gate = new();
    private bool _started;
    private bool _disposed;

    public BatteryAlertWatcher(DeviceRegistry registry, BatteryAlertSettings alerts, INotifier notifier)
    {
        _registry = registry;
        _alerts = alerts;
        _notifier = notifier;
        _detector = new LowBatteryDetector(alerts.Policy);
    }

    /// <summary>Begins watching the devices the registry has now and any it finds later. Safe to call more than once.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _registry.DevicesChanged += OnDevicesChanged;
            _alerts.Changed += OnPolicyChanged;
        }

        SyncDevices();
    }

    public void Dispose()
    {
        List<Watch> watches;
        lock (_gate)
        {
            _disposed = true;
            watches = [.. _watching.Values];
            _watching.Clear();
        }

        _registry.DevicesChanged -= OnDevicesChanged;
        _alerts.Changed -= OnPolicyChanged;
        foreach (var watch in watches)
        {
            watch.Feature.Changed -= watch.Handler;
        }
    }

    private void OnDevicesChanged(object? sender, EventArgs e) => SyncDevices();

    private void OnPolicyChanged(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            _detector.Policy = _detector.Policy with
            {
                LowThreshold = _alerts.Policy.LowThreshold,
                CriticalThreshold = _alerts.Policy.CriticalThreshold,
                IncludeCase = _alerts.Policy.IncludeCase,
            };
        }
    }

    private void SyncDevices()
    {
        var current = _registry.Devices
            .Select(d => (Device: d, Feature: d.GetFeature<IBatteryFeature>()))
            .Where(d => d.Feature is not null)
            .GroupBy(d => d.Device.Identity.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var added = new List<(IEarbudDevice Device, IBatteryFeature Feature, EventHandler<BatteryState> Handler)>();
        var dropped = new List<Watch>();
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var (id, watch) in _watching.ToList())
            {
                if (!current.TryGetValue(id, out var now) || !ReferenceEquals(now.Feature, watch.Feature))
                {
                    _watching.Remove(id);
                    _detector.Forget(id);
                    dropped.Add(watch);
                }
            }

            foreach (var (id, (device, feature)) in current)
            {
                if (!_watching.ContainsKey(id))
                {
                    var name = device.Identity.Name;
                    EventHandler<BatteryState> handler = (_, state) => Evaluate(id, name, state);
                    _watching[id] = new Watch(feature!, handler);
                    added.Add((device, feature!, handler));
                }
            }
        }

        foreach (var watch in dropped)
        {
            watch.Feature.Changed -= watch.Handler;
        }

        foreach (var (device, feature, handler) in added)
        {
            feature.Changed += handler;
            // A device that is already low when it appears alerts once, as the desktop does.
            Evaluate(device.Identity.Id, device.Identity.Name, feature.Current);
        }
    }

    private void Evaluate(string deviceId, string deviceName, BatteryState? state)
    {
        if (state is null)
        {
            return;
        }

        IReadOnlyList<BatteryAlert> raised;
        lock (_gate)
        {
            if (_disposed || !_watching.ContainsKey(deviceId))
            {
                return;
            }

            raised = _detector.Evaluate(deviceId, state);
        }

        if (!_alerts.SystemNotifications)
        {
            return;
        }

        foreach (var alert in raised)
        {
            _notifier.Show("Samaat", AlertText(deviceName, alert));
        }
    }

    /// <summary>The desktop's wording (MainWindowViewModel.OnAlert): "Buds: right bud low (17%)".</summary>
    public static string AlertText(string deviceName, BatteryAlert alert)
    {
        var part = alert.Component switch
        {
            BatteryComponent.Left => "left bud",
            BatteryComponent.Right => "right bud",
            BatteryComponent.Case => "case",
            _ => "battery",
        };
        var level = alert.Level == BatteryAlertLevel.Critical ? "critically low" : "low";
        return $"{deviceName}: {part} {level} ({alert.Percent}%)";
    }
}
