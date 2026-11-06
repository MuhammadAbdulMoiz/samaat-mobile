using Samaat.Core.Battery;
using Samaat.Core.Settings;

namespace Samaat.Mobile.Runtime;

/// <summary>
/// The battery-alert choices (thresholds, the case, system notifications), kept in <see cref="AppSettings"/> like the
/// desktop and shared by everything that shows or raises an alert. Without a settings file they last for the session.
/// Changes come from the UI thread; <see cref="Policy"/> and <see cref="SystemNotifications"/> may be read from any thread.
/// </summary>
public sealed class BatteryAlertSettings
{
    /// <summary>The desktop's allowed range for the low threshold; critical runs from 1 up to one below low.</summary>
    public const int MinLow = 5;
    public const int MaxLow = 50;
    public const int MinCritical = 1;

    private readonly SettingsStore? _store;
    private volatile BatteryAlertPolicy _policy;
    private volatile bool _systemNotifications;

    public BatteryAlertSettings(SettingsStore? store)
    {
        _store = store;
        var saved = (store?.Load() ?? new AppSettings()).Normalize();
        _policy = new BatteryAlertPolicy { LowThreshold = saved.LowThreshold, CriticalThreshold = saved.CriticalThreshold, IncludeCase = saved.IncludeCase };
        _systemNotifications = saved.SystemNotifications;
    }

    public BatteryAlertPolicy Policy => _policy;

    public bool SystemNotifications => _systemNotifications;

    /// <summary>What the phone last said about showing notifications; null until asked (kept per process).</summary>
    public NotificationPermission? Permission { get; set; }

    public event EventHandler? Changed;

    /// <summary>Sets the low threshold (clamped to 5 to 50); critical is pulled below it. Returns the save error, if any.</summary>
    public string? SetLow(int low)
    {
        var clamped = Math.Clamp(low, MinLow, MaxLow);
        return Update(_policy with { LowThreshold = clamped, CriticalThreshold = Math.Min(_policy.CriticalThreshold, clamped - 1) }, _systemNotifications);
    }

    /// <summary>Sets the critical threshold; it must stay below low, so a value that doesn't is refused.</summary>
    public string? SetCritical(int critical) =>
        critical < MinCritical || critical >= _policy.LowThreshold ? null : Update(_policy with { CriticalThreshold = critical }, _systemNotifications);

    public string? SetIncludeCase(bool include) => Update(_policy with { IncludeCase = include }, _systemNotifications);

    public string? SetSystemNotifications(bool on) => Update(_policy, on);

    private string? Update(BatteryAlertPolicy policy, bool notifications)
    {
        _policy = policy;
        _systemNotifications = notifications;
        // The store logs the failure; return it so the UI says so too.
        var error = _store is null || _store.Save(_store.Load() with
        {
            LowThreshold = policy.LowThreshold,
            CriticalThreshold = policy.CriticalThreshold,
            IncludeCase = policy.IncludeCase,
            SystemNotifications = notifications,
        }) ? null : _store.LastError;
        Changed?.Invoke(this, EventArgs.Empty);
        return error;
    }
}
