using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Samaat.Core.Battery;
using Samaat.Core.Capabilities;
using Samaat.Core.Diagnostics;
using Samaat.Handoff.Discovery;
using Samaat.Handoff.Storage;
using Samaat.Mobile.Diagnostics;
using Samaat.Mobile.Runtime;
using Samaat.Mobile.Simulation;
using Samaat.Mobile.ViewModels;
using Samaat.Platform.Android;

namespace Samaat.Mobile.Tests;

/// <summary>Fixes from the Tecno test: shared logging, font scale, the battery fallback and the Bluetooth-off screen.</summary>
public sealed class PlatformFixesTests
{
    // Logging

    [Fact]
    public void OneMobileLogError_IsExactlyOneLine_InTheLogAndInTheSinksTail()
    {
        var marker = Guid.NewGuid().ToString("N");
        var written = new List<string>();
        void Listen(string line) => written.Add(line);
        MobileLog.Written += Listen;
        try
        {
            MobileLog.Error($"Testing {marker}", new InvalidOperationException("boom"));
        }
        finally
        {
            MobileLog.Written -= Listen;
        }

        Assert.Single(MobileLog.Recent(), line => line.Contains(marker));
        var sink = Assert.IsType<MobileLogSink>(SamaatLog.Sink);
        Assert.Single(sink.Tail(20), line => line.Contains(marker));
        Assert.Single(written, line => line.Contains(marker));
        Assert.Contains(MobileLog.Recent(), line => line.Contains(" ERROR Mobile: Testing ") && line.EndsWith("InvalidOperationException: boom", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSink_IsInstalledOnce_AndSharedCodeLogsReachTheSameLog()
    {
        var first = SamaatLog.Sink;
        MobileLogSink.Install();
        var marker = Guid.NewGuid().ToString("N");

        SamaatLog.Warn("Shared", marker);

        Assert.Same(first, SamaatLog.Sink);
        Assert.Single(MobileLog.Recent(), line => line.Contains(marker) && line.Contains(" WARN  Shared: "));
    }

    // Font scale

    [AvaloniaFact]
    public void FontScale_MakesRenderedTextBigger_AndApplyingTwiceDoesNotCompound()
    {
        var app = Application.Current!;
        try
        {
            FontScaling.Apply(app, 1.0);
            var text = new TextBlock { Text = "Body" };
            var window = new Window { Width = 412, Height = 400, Content = new UserControl { Classes = { "shell" }, Content = text } };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var baseSize = text.FontSize;
            Assert.True(app.TryGetResource("Samaat.Mobile.FontSize.Body", app.ActualThemeVariant, out var body));
            Assert.Equal((double)body!, baseSize);

            FontScaling.Apply(app, 1.3);
            FontScaling.Apply(app, 1.3);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(baseSize * 1.3, text.FontSize, 3);
            Assert.True(app.TryGetResource("Samaat.FontSize.Title", app.ActualThemeVariant, out var title));
            Assert.Equal(22 * 1.3, (double)title!, 3);
            Assert.True(app.TryGetResource("ControlContentThemeFontSize", app.ActualThemeVariant, out var control));
            Assert.Equal(14 * 1.3, (double)control!, 3);
        }
        finally
        {
            FontScaling.Apply(app, 1.0);
        }
    }

    [AvaloniaFact]
    public void FontScale_IsClamped()
    {
        var app = Application.Current!;
        try
        {
            FontScaling.Apply(app, 5);
            Assert.True(app.TryGetResource("Samaat.Mobile.FontSize.Body", app.ActualThemeVariant, out var body));
            Assert.Equal(15 * FontScaling.MaxScale, (double)body!, 3);
        }
        finally
        {
            FontScaling.Apply(app, 1.0);
        }
    }

    [Fact]
    public void ThePlatformsFontScale_DefaultsToOne()
    {
        Assert.Equal(1.0, new NoMobilePlatform().FontScale);
    }

    // Battery fallback

    private static BatteryState Level(int percent) =>
        BatteryState.Empty.With(new BatteryReading(BatteryComponent.Combined, percent, DateTimeOffset.UnixEpoch));

    [Fact]
    public void Fallback_ShowsTheOsLevel_UntilThePrimaryHasOne_ThenThePrimaryWins()
    {
        var primary = new FakeBattery(CapabilitySource.StandardService);
        var os = new FakeBattery(CapabilitySource.OperatingSystem, Level(40));
        var fallback = new FallbackBatteryFeature(primary, os);

        Assert.Equal(40, fallback.Current.Get(BatteryComponent.Combined)!.Percent);
        Assert.Equal(CapabilitySource.OperatingSystem, fallback.Source);

        primary.Publish(Level(75));

        Assert.Equal(75, fallback.Current.Get(BatteryComponent.Combined)!.Percent);
        Assert.Equal(CapabilitySource.StandardService, fallback.Source);
    }

    [Fact]
    public void Fallback_ARowWithoutAPercentDoesNotCountAsAReading()
    {
        var primary = new FakeBattery(CapabilitySource.StandardService,
            BatteryState.Empty.With(new BatteryReading(BatteryComponent.Left, null, DateTimeOffset.UnixEpoch)));
        var os = new FakeBattery(CapabilitySource.OperatingSystem, Level(40));

        Assert.Equal(40, new FallbackBatteryFeature(primary, os).Current.Get(BatteryComponent.Combined)!.Percent);
    }

    [Fact]
    public void Fallback_RaisesChanged_WhenEitherSourceChanges()
    {
        var primary = new FakeBattery(CapabilitySource.StandardService);
        var os = new FakeBattery(CapabilitySource.OperatingSystem);
        var fallback = new FallbackBatteryFeature(primary, os);
        var seen = new List<int?>();
        fallback.Changed += (sender, state) => seen.Add(state.Readings.FirstOrDefault()?.Percent);

        os.Publish(Level(30));
        primary.Publish(Level(80));

        Assert.Equal([30, 80], seen);
    }

    [Fact]
    public async Task Fallback_Refresh_ReachesBoth_EvenWhenOneThrows()
    {
        var primary = new FakeBattery(CapabilitySource.StandardService) { Throw = true };
        var os = new FakeBattery(CapabilitySource.OperatingSystem);
        var fallback = new FallbackBatteryFeature(primary, os);

        await fallback.RefreshAsync();

        Assert.Equal(1, primary.Refreshes);
        Assert.Equal(1, os.Refreshes);
    }

    [Fact]
    public async Task Fallback_Dispose_DisposesBothAndStopsListening()
    {
        var primary = new FakeBattery(CapabilitySource.StandardService);
        var os = new FakeBattery(CapabilitySource.OperatingSystem);
        var fallback = new FallbackBatteryFeature(primary, os);
        var raised = 0;
        fallback.Changed += (sender, state) => raised++;

        await fallback.DisposeAsync();
        primary.Publish(Level(10));

        Assert.True(primary.Disposed);
        Assert.True(os.Disposed);
        Assert.Equal(0, raised);
    }

    private sealed class FakeBattery(CapabilitySource source, BatteryState? initial = null) : IBatteryFeature, IDisposable
    {
        public int Refreshes { get; private set; }

        public bool Throw { get; set; }

        public bool Disposed { get; private set; }

        public IReadOnlySet<BatteryComponent> Components { get; } = new HashSet<BatteryComponent> { BatteryComponent.Combined };

        public bool ComponentsAreExhaustive => false;

        public CapabilitySource Source => source;

        public Verification Verification => Verification.Unverified;

        public BatteryState Current { get; private set; } = initial ?? BatteryState.Empty;

        public event EventHandler<BatteryState>? Changed;

        public void Publish(BatteryState state)
        {
            Current = state;
            Changed?.Invoke(this, state);
        }

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            Refreshes++;
            return Throw ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask;
        }

        public void Dispose() => Disposed = true;
    }

    // Bluetooth off

    private static MainViewModel EmptyPhone(Action<SimulatedPhoneAudio> setUp)
    {
        var audio = new SimulatedPhoneAudio([]);
        setUp(audio);
        var runtime = new MobileRuntime([], audio, new PeerStore(null), "Phone", new InMemoryBeaconBus().CreateListener(), new NoMobilePlatform());
        return new MainViewModel(runtime, a => a());
    }

    [AvaloniaFact]
    public void WithABlocker_TheBannerShows_AndTheEmptyStateDoesNot()
    {
        using var shell = EmptyPhone(a => a.Blocker = "Allow Samaat to find and connect to nearby devices.");

        Assert.True(shell.HasBanner);
        Assert.False(shell.ShowEmpty);
    }

    [AvaloniaFact]
    public void WithBluetoothOff_TheBannerShows_AndTheEmptyStateDoesNot()
    {
        using var shell = EmptyPhone(a => a.IsBluetoothOn = false);

        Assert.Equal("Bluetooth is off", shell.BannerText);
        Assert.False(shell.ShowEmpty);
    }

    [AvaloniaFact]
    public void WithoutABanner_NoPairedEarbuds_ShowsTheEmptyState()
    {
        using var shell = EmptyPhone(a => { });

        Assert.False(shell.HasBanner);
        Assert.True(shell.ShowEmpty);
    }
}
