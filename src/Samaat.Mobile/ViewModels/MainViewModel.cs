using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Samaat.Mobile.Diagnostics;
using Samaat.Mobile.Runtime;

namespace Samaat.Mobile.ViewModels;

public enum MobileTab
{
    Earbuds,
    Computer,
    Settings,
}

/// <summary>The phone app's shell: three tabs (earbuds, computer, settings), a device page, a banner for blockers and short messages.</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan MessageLifetime = TimeSpan.FromSeconds(6);

    private readonly MobileRuntime _runtime;
    private readonly Action<Action> _post;
    private ITimer? _messageTimer;

    public MainViewModel(MobileRuntime runtime, Action<Action>? post = null)
    {
        _runtime = runtime;
        _post = post ?? (a => Dispatcher.UIThread.Post(a));
        Computer = new ComputerViewModel(runtime, _post, ShowMessage);
        Settings = new SettingsViewModel(runtime.Settings, ThemePolicy.Apply, AccentPalette.Apply, runtime.Alerts, runtime.Platform.RequestNotificationPermissionAsync, _post);
        _runtime.Registry.DevicesChanged += (_, _) => _post(SyncDevices);
        _runtime.Audio.Changed += (_, _) => _post(RefreshBanner);
        SyncDevices();
        RefreshBanner();
        RefreshMotion();
    }

    /// <summary>The phone asks for no animations: the view stops the ring's glide, the busy sweep and the press scale.</summary>
    [ObservableProperty]
    public partial bool ReduceMotion { get; set; }

    /// <summary>Reads the phone's animation setting again (the Android activity calls this on resume).</summary>
    public void RefreshMotion() => ReduceMotion = _runtime.Platform.PrefersReducedMotion;

    /// <summary>Asks for notification permission again if it is on and the phone had no screen to ask from (the activity calls this on resume).</summary>
    public void CheckNotificationPermission() => Settings.CheckNotificationPermission();

    public ComputerViewModel Computer { get; }

    public SettingsViewModel Settings { get; }

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = [];

    public bool HasDevices => Devices.Count > 0;

    public bool ShowEmpty => IsEarbuds && SelectedDevice is null && !HasDevices && !HasBanner;

    public string EmptyText => "No earbuds are paired with this phone. Pair them in Bluetooth settings, then come back.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEarbuds), nameof(IsComputer), nameof(IsSettings), nameof(ShowDeviceList), nameof(ShowDevice), nameof(ShowDiagnostics), nameof(ShowEmpty), nameof(Title), nameof(CanGoBack), nameof(ShowBrand))]
    public partial MobileTab Tab { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDeviceList), nameof(ShowDevice), nameof(ShowDiagnostics), nameof(ShowEmpty), nameof(Title), nameof(CanGoBack), nameof(ShowBrand))]
    public partial DeviceItemViewModel? SelectedDevice { get; set; }

    public bool IsEarbuds => Tab == MobileTab.Earbuds;

    public bool IsComputer => Tab == MobileTab.Computer;

    public bool IsSettings => Tab == MobileTab.Settings;

    public bool ShowDeviceList => IsEarbuds && SelectedDevice is null && HasDevices;

    public bool ShowDevice => IsEarbuds && SelectedDevice is not null && Diagnostics is null;

    public bool ShowDiagnostics => IsEarbuds && Diagnostics is not null;

    public bool CanGoBack => ShowDevice || ShowDiagnostics;

    /// <summary>The brand mark sits beside "Samaat" on the home screen only, as in the desktop sidebar.</summary>
    public bool ShowBrand => IsEarbuds && SelectedDevice is null;

    public string Title => ShowDiagnostics ? "Diagnostics" : ShowDevice ? SelectedDevice!.Name : IsComputer ? "Computer" : IsSettings ? "Settings" : "Samaat";

    /// <summary>Something stops Samaat working (a permission, Bluetooth off), with a button that fixes it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBanner), nameof(ShowEmpty))]
    public partial string? BannerText { get; set; }

    [ObservableProperty]
    public partial string BannerAction { get; set; } = "";

    public bool HasBanner => BannerText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; set; }

    public bool HasMessage => Message is not null;

    /// <summary>Starts without waiting (the app's entry point): a failure is logged and shown, never lost.</summary>
    public void Start() =>
        StartAsync().Observe("Starting the phone app",
            _ => _post(() => ShowMessage("Samaat couldn't start everything. Close and reopen the app to try again.")));

    public async Task StartAsync()
    {
        if (_runtime.Audio.Blocker is not null)
        {
            await _runtime.Platform.RequestPermissionsAsync();
        }

        _runtime.Platform.KeepLinkAlive(true);
        await _runtime.StartAsync();
        RefreshBanner();
        RefreshMotion();
    }

    [RelayCommand]
    private void ShowEarbuds()
    {
        Tab = MobileTab.Earbuds;
        SelectedDevice = null;
    }

    [RelayCommand]
    private void ShowComputer() => Tab = MobileTab.Computer;

    [RelayCommand]
    private void ShowSettings() => Tab = MobileTab.Settings;

    [RelayCommand]
    private void Open(DeviceItemViewModel device)
    {
        Tab = MobileTab.Earbuds;
        SelectedDevice = device;
    }

    /// <summary>Back from a device page. Returns false when there is nowhere to go back to (Android then leaves the app).</summary>
    public bool GoBack()
    {
        if (Diagnostics is not null)
        {
            CloseDiagnostics();
            return true;
        }

        if (SelectedDevice is not null)
        {
            SelectedDevice = null;
            return true;
        }

        if (Tab != MobileTab.Earbuds)
        {
            Tab = MobileTab.Earbuds;
            return true;
        }

        return false;
    }

    [RelayCommand]
    private void Back() => GoBack();

    [RelayCommand]
    private async Task FixBannerAsync()
    {
        if (_runtime.Audio.Blocker is not null)
        {
            await _runtime.Platform.RequestPermissionsAsync();
            await _runtime.Registry.RefreshAsync();
        }
        else
        {
            _runtime.Platform.OpenBluetoothSettings();
        }

        RefreshBanner();
    }

    [RelayCommand]
    private Task RefreshAsync() => _runtime.Registry.RefreshAsync();

    /// <summary>The empty list's way out: earbuds are paired in the phone's Bluetooth settings, not in Samaat.</summary>
    [RelayCommand]
    private Task OpenBluetoothSettingsAsync()
    {
        _runtime.Platform.OpenBluetoothSettings();
        return Task.CompletedTask;
    }

    public void ShowMessage(string text)
    {
        Message = text;
        _messageTimer?.Dispose();
        _messageTimer = _runtime.Time.CreateTimer(_ => _post(() =>
        {
            if (Message == text)
            {
                Message = null;
            }
        }), null, MessageLifetime, Timeout.InfiniteTimeSpan);
    }

    [RelayCommand]
    private void DismissMessage() => Message = null;

    private void RefreshBanner()
    {
        if (_runtime.Audio.Blocker is { } blocker)
        {
            BannerText = blocker;
            BannerAction = "Allow";
        }
        else if (!_runtime.Audio.IsBluetoothOn)
        {
            BannerText = "Bluetooth is off";
            BannerAction = "Turn on";
        }
        else
        {
            BannerText = null;
        }
    }

    private void SyncDevices()
    {
        var current = _runtime.Registry.Devices;
        foreach (var stale in Devices.Where(vm => !current.Contains(vm.Device)).ToList())
        {
            if (ReferenceEquals(SelectedDevice, stale))
            {
                SelectedDevice = null;
            }

            stale.Dispose();
            Devices.Remove(stale);
        }

        foreach (var device in current.Where(d => Devices.All(vm => vm.Device != d)))
        {
            Devices.Add(new DeviceItemViewModel(device, _runtime, _post, ShowMessage, () => Tab = MobileTab.Computer, () => OpenDiagnostics(device)));
        }

        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(ShowDeviceList));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    public void Dispose()
    {
        _messageTimer?.Dispose();
        CloseDiagnostics();
        Computer.Dispose();
        foreach (var device in Devices)
        {
            device.Dispose();
        }
    }
}
