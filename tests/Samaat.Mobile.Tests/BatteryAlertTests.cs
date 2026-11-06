using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Samaat.Core.Battery;
using Samaat.Core.Capabilities;
using Samaat.Core.Devices;
using Samaat.Core.Platform;
using Samaat.Core.Settings;
using Samaat.Mobile.Runtime;
using Samaat.Mobile.Simulation;
using Samaat.Mobile.ViewModels;

namespace Samaat.Mobile.Tests;

/// <summary>Battery alerts: the policy the display follows, the watcher that notifies, and the settings that drive both.</summary>
public sealed class BatteryAlertTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"samaat-alerts-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    private static BatteryState State(params (BatteryComponent Component, int Percent)[] readings) =>
        BatteryState.Empty.With(readings.Select(r => new BatteryReading(r.Component, r.Percent, Now)));

    private static BatteryState Right(int percent) => State((BatteryComponent.Right, percent));

    private sealed class FakeBattery(BatteryState initial) : IBatteryFeature
    {
        public IReadOnlySet<BatteryComponent> Components { get; } = new HashSet<BatteryComponent>();
        public bool ComponentsAreExhaustive => true;
        public CapabilitySource Source => CapabilitySource.Simulator;
        public Verification Verification => Verification.Verified;
        public BatteryState Current { get; private set; } = initial;
        public event EventHandler<BatteryState>? Changed;
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public int Subscribers => Changed?.GetInvocationList().Length ?? 0;

        public void Publish(BatteryState state)
        {
            Current = state;
            Changed?.Invoke(this, state);
        }
    }

    private sealed class FakeDevice(string id, string name, FakeBattery battery) : IEarbudDevice
    {
        public DeviceIdentity Identity { get; } = new(id, name);
        public ConnectionState Connection => ConnectionState.Connected;
        public IReadOnlyCollection<IFeature> Features { get; } = [battery];
        public event EventHandler? ConnectionChanged { add { } remove { } }
    }

    private sealed class FakeSource : IDeviceSource
    {
        public List<IEarbudDevice> Devices { get; } = [];
        public string Name => "fake";
        public Task<IReadOnlyList<IEarbudDevice>> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IEarbudDevice>>([.. Devices]);
        public event EventHandler? DevicesChanged { add { } remove { } }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingNotifier : INotifier
    {
        public List<(string Title, string Body)> Shown { get; } = [];
        public void Show(string title, string body) => Shown.Add((title, body));
    }

    private sealed class Rig : IDisposable
    {
        public FakeSource Source { get; } = new();
        public DeviceRegistry Registry { get; }
        public BatteryAlertSettings Alerts { get; } = new(null);
        public RecordingNotifier Notifier { get; } = new();
        public BatteryAlertWatcher Watcher { get; }

        public Rig()
        {
            Registry = new DeviceRegistry([Source]);
            Watcher = new BatteryAlertWatcher(Registry, Alerts, Notifier);
            Alerts.SetSystemNotifications(true);
        }

        public async Task StartAsync()
        {
            await Registry.RefreshAsync();
            Watcher.Start();
        }

        public void Dispose() => Watcher.Dispose();
    }

    // The display follows the policy

    [AvaloniaFact]
    public async Task ChangingLow_UpdatesAnOpenDeviceCellAndSummary()
    {
        var runtime = SimulatedMobile.Create();
        var device = new FakeDevice("d1", "Buds", new FakeBattery(Right(25)));
        using var item = new DeviceItemViewModel(device, runtime, a => a(), _ => { }, () => { });
        Assert.False(item.SummaryIsLow);
        Assert.False(item.Battery.Single().IsLow);

        runtime.Alerts.SetLow(30);

        Assert.True(item.SummaryIsLow);
        Assert.True(item.Battery.Single().IsLow);
        Assert.Equal("Right bud low · 25%", item.AlertText);
        await runtime.DisposeAsync();
    }

    [Fact]
    public void IncludeCase_DecidesWhetherALowCaseRaisesTheAlert()
    {
        var state = State((BatteryComponent.Left, 90), (BatteryComponent.Right, 80), (BatteryComponent.Case, 15));

        Assert.Null(BatterySummary.From(state, true, new BatteryAlertPolicy()).AlertText);
        var included = BatterySummary.From(state, true, new BatteryAlertPolicy { IncludeCase = true });

        Assert.Equal("Case low · 15%", included.AlertText);
        Assert.False(included.IsCritical);
    }

    [Fact]
    public void TheCellsThresholds_ComeFromThePolicy()
    {
        var cell = BatterySummary.From(Right(12), true, new BatteryAlertPolicy { LowThreshold = 15, CriticalThreshold = 12 }).Cells.Single();

        Assert.True(cell.IsCritical);
        Assert.False(BatterySummary.From(Right(12), true, new BatteryAlertPolicy()).Cells.Single().IsCritical);
    }

    // The watcher

    [Fact]
    public async Task TheWatcher_NotifiesOncePerCrossing_UntilRearmed()
    {
        using var rig = new Rig();
        var battery = new FakeBattery(Right(50));
        rig.Source.Devices.Add(new FakeDevice("d1", "Buds", battery));
        await rig.StartAsync();
        Assert.Empty(rig.Notifier.Shown);

        battery.Publish(Right(18));
        battery.Publish(Right(17));
        Assert.Equal([("Samaat", "Buds: right bud low (18%)")], rig.Notifier.Shown);

        battery.Publish(Right(8));
        battery.Publish(Right(7));
        Assert.Equal(("Samaat", "Buds: right bud critically low (8%)"), rig.Notifier.Shown[^1]);
        Assert.Equal(2, rig.Notifier.Shown.Count);

        battery.Publish(Right(60)); // above low plus the re-arm margin
        battery.Publish(Right(15));
        Assert.Equal(3, rig.Notifier.Shown.Count);
    }

    [Fact]
    public async Task TheWatcher_ShowsNothing_WithSystemNotificationsOff()
    {
        using var rig = new Rig();
        rig.Alerts.SetSystemNotifications(false);
        var battery = new FakeBattery(Right(50));
        rig.Source.Devices.Add(new FakeDevice("d1", "Buds", battery));
        await rig.StartAsync();

        battery.Publish(Right(5));

        Assert.Empty(rig.Notifier.Shown);
    }

    [Fact]
    public async Task TheWatcher_UsesThePolicyAsItChanges()
    {
        using var rig = new Rig();
        var battery = new FakeBattery(Right(50));
        rig.Source.Devices.Add(new FakeDevice("d1", "Buds", battery));
        await rig.StartAsync();

        battery.Publish(Right(28));
        Assert.Empty(rig.Notifier.Shown);
        rig.Alerts.SetLow(30);
        battery.Publish(Right(27));

        Assert.Single(rig.Notifier.Shown);
    }

    [Fact]
    public async Task TheWatcher_CountsACaseOnlyWhenIncluded()
    {
        using var rig = new Rig();
        var battery = new FakeBattery(State((BatteryComponent.Right, 90), (BatteryComponent.Case, 90)));
        rig.Source.Devices.Add(new FakeDevice("d1", "Buds", battery));
        await rig.StartAsync();

        battery.Publish(State((BatteryComponent.Right, 90), (BatteryComponent.Case, 15)));
        Assert.Empty(rig.Notifier.Shown);
        rig.Alerts.SetIncludeCase(true);
        battery.Publish(State((BatteryComponent.Right, 90), (BatteryComponent.Case, 14)));

        Assert.Equal("Buds: case low (14%)", Assert.Single(rig.Notifier.Shown).Body);
    }

    [Fact]
    public async Task TheWatcher_ForgetsRemovedDevices_AndStopsListeningToThem()
    {
        using var rig = new Rig();
        var first = new FakeBattery(Right(50));
        rig.Source.Devices.Add(new FakeDevice("d1", "Buds", first));
        await rig.StartAsync();
        first.Publish(Right(15));
        Assert.Single(rig.Notifier.Shown);
        Assert.Equal(1, first.Subscribers);

        rig.Source.Devices.Clear();
        await rig.Registry.RefreshAsync();
        Assert.Equal(0, first.Subscribers);
        first.Publish(Right(5));
        Assert.Single(rig.Notifier.Shown);

        // The same earbuds come back already low: the old alert state was forgotten, so they alert again.
        rig.Source.Devices.Add(new FakeDevice("d1", "Buds", new FakeBattery(Right(15))));
        await rig.Registry.RefreshAsync();

        Assert.Equal(2, rig.Notifier.Shown.Count);
    }

    [Fact]
    public async Task TheWatcher_PicksUpDevicesFoundLater_AndStopsOnDispose()
    {
        using var rig = new Rig();
        await rig.StartAsync();
        var battery = new FakeBattery(Right(50));
        rig.Source.Devices.Add(new FakeDevice("d2", "Late buds", battery));
        await rig.Registry.RefreshAsync();

        battery.Publish(Right(15));
        Assert.Equal("Late buds: right bud low (15%)", Assert.Single(rig.Notifier.Shown).Body);

        rig.Watcher.Dispose();
        Assert.Equal(0, battery.Subscribers);
    }

    // Settings

    [Fact]
    public void TheAlertSettings_Persist_AndKeepTheOtherFields()
    {
        var store = new SettingsStore(_path);
        store.Save(new AppSettings { Theme = ThemeChoice.Light, ArtworkChoices = new Dictionary<string, string> { ["dev"] = "unknown.single" } });
        var alerts = new BatteryAlertSettings(store);

        alerts.SetLow(35);
        alerts.SetCritical(12);
        alerts.SetIncludeCase(true);
        alerts.SetSystemNotifications(true);

        var saved = new SettingsStore(_path).Load();
        Assert.Equal((35, 12, true, true), (saved.LowThreshold, saved.CriticalThreshold, saved.IncludeCase, saved.SystemNotifications));
        Assert.Equal(ThemeChoice.Light, saved.Theme);
        Assert.Equal("unknown.single", saved.ArtworkChoices["dev"]);
        var reloaded = new BatteryAlertSettings(new SettingsStore(_path));
        Assert.Equal(new BatteryAlertPolicy { LowThreshold = 35, CriticalThreshold = 12, IncludeCase = true }, reloaded.Policy);
        Assert.True(reloaded.SystemNotifications);
    }

    [Fact]
    public void TheAccent_IsSavedWithTheViewModel_AndKeepsTheOtherFields()
    {
        var store = new SettingsStore(_path);
        store.Save(new AppSettings { LowThreshold = 33 });
        var applied = new List<AccentChoice>();
        var settings = new SettingsViewModel(store, _ => { }, applied.Add);
        Assert.True(settings.IsLimeAccent);

        settings.SetAccentCommand.Execute(AccentChoice.Ice);

        var saved = new SettingsStore(_path).Load();
        Assert.Equal(AccentChoice.Ice, saved.Accent);
        Assert.Equal(33, saved.LowThreshold);
        Assert.Equal([AccentChoice.Ice], applied);
        Assert.True(settings.IsIceAccent);
        Assert.Equal(AccentChoice.Ice, new SettingsViewModel(new SettingsStore(_path), _ => { }).Accent);
    }

    [Fact]
    public void AFailedSave_OfAnAlertChoice_IsShown()
    {
        var folder = _path + ".dir";
        Directory.CreateDirectory(folder);
        try
        {
            var settings = new SettingsViewModel(new SettingsStore(folder), _ => { });

            settings.IncludeCase = true;

            Assert.StartsWith("Settings could not be saved", settings.ErrorText);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Critical_MustStayBelowLow()
    {
        var alerts = new BatteryAlertSettings(null);
        alerts.SetSystemNotifications(false); // the default is on
        var settings = new SettingsViewModel(null, _ => { }, alerts: alerts);

        settings.SelectedCritical = 20; // low is 20: refused
        Assert.Equal(10, alerts.Policy.CriticalThreshold);
        settings.SelectedCritical = 19;
        Assert.Equal(19, alerts.Policy.CriticalThreshold);
        Assert.Equal(Enumerable.Range(1, 19), settings.CriticalChoices);

        settings.SelectedLow = 8; // pulls critical under it
        Assert.Equal(7, alerts.Policy.CriticalThreshold);
        Assert.Equal(Enumerable.Range(1, 7), settings.CriticalChoices);
        Assert.Equal(7, settings.SelectedCritical);

        settings.SelectedLow = 99; // the desktop's range is 5 to 50
        Assert.Equal(50, alerts.Policy.LowThreshold);
        Assert.Equal(Enumerable.Range(5, 46), settings.LowChoices);
    }

    [Fact]
    public void ADeniedPermission_ShowsTheText()
    {
        var alerts = new BatteryAlertSettings(null);
        alerts.SetSystemNotifications(false); // the default is on
        var settings = new SettingsViewModel(null, _ => { }, alerts: alerts, requestNotifications: () => Task.FromResult(NotificationPermission.Denied));
        Assert.False(settings.NotificationsDenied);

        settings.SystemNotifications = true;

        Assert.True(settings.NotificationsDenied);
        Assert.Equal("Notifications are off for Samaat in Android settings", settings.NotificationsDeniedText);
        settings.SystemNotifications = false;
        Assert.False(settings.NotificationsDenied);
    }

    [Fact]
    public void Resume_AsksAgainOnlyAfterUnavailable()
    {
        var answers = new Queue<NotificationPermission>([NotificationPermission.Unavailable, NotificationPermission.Granted]);
        var asked = 0;
        var alerts = new BatteryAlertSettings(null);
        alerts.SetSystemNotifications(false); // the default is on
        var settings = new SettingsViewModel(null, _ => { }, alerts: alerts, requestNotifications: () =>
        {
            asked++;
            return Task.FromResult(answers.Dequeue());
        });

        settings.CheckNotificationPermission(); // off: nothing to ask
        Assert.Equal(0, asked);

        settings.SystemNotifications = true; // asks, no screen yet
        settings.CheckNotificationPermission(); // asks again: Granted
        settings.CheckNotificationPermission(); // settled
        settings.CheckNotificationPermission();

        Assert.Equal(2, asked);
        Assert.Equal(NotificationPermission.Granted, alerts.Permission);
        Assert.False(settings.NotificationsDenied);
    }

    // The accent on screen

    [AvaloniaTheory]
    [InlineData(AccentChoice.Ice, "#6CC4FF", "#0A66B8")]
    [InlineData(AccentChoice.Mono, "#E6E9EC", "#23282E")]
    public void ChoosingAnAccent_ReplacesTheAccentBrush_InBothThemes(AccentChoice choice, string dark, string light)
    {
        try
        {
            AccentPalette.Apply(choice);

            Assert.Equal(Color.Parse(dark), Brush(ThemeVariant.Dark, "Samaat.Accent"));
            Assert.Equal(Color.Parse(light), Brush(ThemeVariant.Light, "Samaat.Accent"));
            Assert.Equal(Color.Parse(dark), Brush(ThemeVariant.Dark, "Samaat.Wave"));
        }
        finally
        {
            AccentPalette.Apply(AccentChoice.Lime);
        }
    }

    [AvaloniaFact]
    public void TheStoredAccent_IsAppliedWhenTheMainViewIsCreated()
    {
        var store = new SettingsStore(_path);
        store.Save(new AppSettings { Accent = AccentChoice.Emerald });
        var runtime = SimulatedMobile.Create(settings: store);
        var factory = MobileApp.RuntimeFactory;
        MobileApp.RuntimeFactory = () => runtime;
        try
        {
            MobileApp.CreateMainView();

            Assert.Equal(Color.Parse("#3DDC97"), Brush(ThemeVariant.Dark, "Samaat.Accent"));
        }
        finally
        {
            MobileApp.RuntimeFactory = factory;
            AccentPalette.Apply(AccentChoice.Lime);
        }
    }

    private static Color Brush(ThemeVariant variant, string key)
    {
        Assert.True(Application.Current!.TryGetResource(key, variant, out var value), key);
        return ((SolidColorBrush)value!).Color;
    }
}
