using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Samaat.Core.Settings;
using Samaat.Mobile.Diagnostics;
using Samaat.Mobile.Runtime;

namespace Samaat.Mobile.ViewModels;

/// <summary>The Settings tab: appearance and battery alerts (saved, applied at once) and the About section.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore? _store;
    private readonly Action<ThemeChoice> _apply;
    private readonly Action<AccentChoice>? _applyAccent;
    private readonly BatteryAlertSettings _alerts;
    private readonly Func<Task<NotificationPermission>> _requestNotifications;
    private readonly Action<Action> _post;
    private bool _asking;

    /// <param name="store">The settings file, or null: then the choices last for the session.</param>
    /// <param name="apply">Puts a theme choice on screen (<see cref="ThemePolicy.Apply"/> in the app).</param>
    /// <param name="applyAccent">Puts an accent choice on screen (<see cref="AccentPalette.Apply(AccentChoice)"/> in the app).</param>
    /// <param name="alerts">The runtime's battery-alert choices; without one the view model keeps its own.</param>
    /// <param name="requestNotifications">Asks the phone to allow notifications; without one they are always allowed.</param>
    /// <param name="post">Runs something on the UI thread (the answer to a permission request arrives elsewhere).</param>
    public SettingsViewModel(SettingsStore? store, Action<ThemeChoice> apply, Action<AccentChoice>? applyAccent = null,
        BatteryAlertSettings? alerts = null, Func<Task<NotificationPermission>>? requestNotifications = null, Action<Action>? post = null)
    {
        _store = store;
        _apply = apply;
        _applyAccent = applyAccent;
        _alerts = alerts ?? new BatteryAlertSettings(store);
        _requestNotifications = requestNotifications ?? (() => Task.FromResult(NotificationPermission.Granted));
        _post = post ?? (a => a());
        Theme = ThemePolicy.Stored(store);
        Accent = store?.Load().Accent ?? new AppSettings().Accent;
        CriticalChoices = CriticalRange();
    }

    /// <summary>Saves one change on top of what is stored, so other settings survive; says so when saving fails.</summary>
    private void Save(Func<AppSettings, AppSettings> change)
    {
        // The store logs the failure; say it here too so the choice isn't silently lost on restart.
        ErrorText = _store is null || _store.Save(change(_store.Load())) ? null : _store.LastError;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSystemTheme), nameof(IsLightTheme), nameof(IsDarkTheme))]
    public partial ThemeChoice Theme { get; private set; }

    public bool IsSystemTheme => Theme == ThemeChoice.System;

    public bool IsLightTheme => Theme == ThemeChoice.Light;

    public bool IsDarkTheme => Theme == ThemeChoice.Dark;

    [RelayCommand]
    private void SetTheme(ThemeChoice theme)
    {
        Theme = theme;
        _apply(theme);
        Save(s => s with { Theme = theme });
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLimeAccent), nameof(IsEmeraldAccent), nameof(IsIceAccent), nameof(IsMonoAccent))]
    public partial AccentChoice Accent { get; private set; }

    public bool IsLimeAccent => Accent == AccentChoice.Lime;

    public bool IsEmeraldAccent => Accent == AccentChoice.Emerald;

    public bool IsIceAccent => Accent == AccentChoice.Ice;

    public bool IsMonoAccent => Accent == AccentChoice.Mono;

    [RelayCommand]
    private void SetAccent(AccentChoice accent)
    {
        Accent = accent;
        _applyAccent?.Invoke(accent);
        Save(s => s with { Accent = accent });
    }

    // Battery alerts: the desktop's ranges (low 5 to 50, critical 1 to one below low), as lists to pick from.

    public IReadOnlyList<int> LowChoices { get; } = [.. Enumerable.Range(BatteryAlertSettings.MinLow, BatteryAlertSettings.MaxLow - BatteryAlertSettings.MinLow + 1)];

    [ObservableProperty]
    public partial IReadOnlyList<int> CriticalChoices { get; private set; }

    private IReadOnlyList<int> CriticalRange() =>
        [.. Enumerable.Range(BatteryAlertSettings.MinCritical, _alerts.Policy.LowThreshold - BatteryAlertSettings.MinCritical)];

    // Nullable because a ComboBox reports null while its list is being replaced; that is not a choice.
    public int? SelectedLow
    {
        get => _alerts.Policy.LowThreshold;
        set
        {
            if (value is { } low && low != _alerts.Policy.LowThreshold)
            {
                var error = _alerts.SetLow(low);
                CriticalChoices = CriticalRange();
                OnPropertyChanged(nameof(SelectedCritical));
                ShowSaveError(error);
            }
        }
    }

    /// <summary>Critical must stay below low: a choice at or above it is refused and the list snaps back.</summary>
    public int? SelectedCritical
    {
        get => _alerts.Policy.CriticalThreshold;
        set
        {
            if (value is { } critical && critical != _alerts.Policy.CriticalThreshold)
            {
                ShowSaveError(_alerts.SetCritical(critical));
            }

            OnPropertyChanged();
        }
    }

    public bool IncludeCase
    {
        get => _alerts.Policy.IncludeCase;
        set
        {
            if (value != _alerts.Policy.IncludeCase)
            {
                ShowSaveError(_alerts.SetIncludeCase(value));
            }
        }
    }

    public bool SystemNotifications
    {
        get => _alerts.SystemNotifications;
        set
        {
            if (value == _alerts.SystemNotifications)
            {
                return;
            }

            ShowSaveError(_alerts.SetSystemNotifications(value));
            OnPropertyChanged();
            OnPropertyChanged(nameof(NotificationsDenied));
            if (value)
            {
                AskForNotifications();
            }
        }
    }

    /// <summary>The switch is on but Android won't show Samaat's notifications.</summary>
    public bool NotificationsDenied => _alerts.SystemNotifications && _alerts.Permission == NotificationPermission.Denied;

    public string NotificationsDeniedText => "Notifications are off for Samaat in Android settings";

    /// <summary>Asks again only when it is on and the phone hasn't answered yet (it had no screen to ask from).</summary>
    public void CheckNotificationPermission()
    {
        if (_alerts.SystemNotifications && _alerts.Permission is null or NotificationPermission.Unavailable)
        {
            AskForNotifications();
        }
    }

    private void AskForNotifications()
    {
        if (_asking)
        {
            return;
        }

        _asking = true;
        AskAsync().Observe("Asking to show notifications", _ => _post(() => _asking = false));
    }

    private async Task AskAsync()
    {
        var answer = await _requestNotifications();
        _post(() =>
        {
            _alerts.Permission = answer;
            _asking = false;
            OnPropertyChanged(nameof(NotificationsDenied));
        });
    }

    private void ShowSaveError(string? error) => ErrorText = error;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorText { get; private set; }

    public bool HasError => ErrorText is not null;

    /// <summary>"Version 1.0.0", without the build hash (the desktop's approach, on this assembly).</summary>
    public string VersionText { get; } = "Version " + (typeof(SettingsViewModel).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0]
        ?? typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "unknown");

    /// <summary>The GPL-3.0 "Appropriate Legal Notices" shown in the About section.</summary>
    public string LicenceNotice => "Free software under the GNU GPL v3 or later. No warranty.";

    public Uri LicenceUri { get; } = new("https://www.gnu.org/licenses/gpl-3.0.html");

    public Uri SourceUri { get; } = new("https://github.com/MuhammadAbdulMoiz/samaat-mobile");
}
