using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Samaat.Mobile.Runtime;
using Samaat.Mobile.Simulation;
using Samaat.Mobile.ViewModels;
using Samaat.Mobile.Views;
using Samaat.UI.Controls;

namespace Samaat.Mobile.Tests;

/// <summary>
/// The phone's motion, after the desktop's review-animations fixes: the battery ring jumps when the device changes and
/// glides only for the same device, and the phone's "remove animations" setting stops every movement.
/// </summary>
public sealed class MotionTests : IAsyncDisposable
{
    private readonly MobileRuntime _runtime = SimulatedMobile.Create();
    private readonly MainViewModel _viewModel;
    private readonly MainView _view;
    private readonly Window _window;

    public MotionTests()
    {
        _viewModel = new MainViewModel(_runtime);
        _view = new MainView { DataContext = _viewModel };
        _window = new Window { Width = 412, Height = 892, Content = _view };
        _window.Show();
    }

    private NoMobilePlatform Platform => (NoMobilePlatform)_runtime.Platform;

    private static void Pump(int frames = 30)
    {
        for (var i = 0; i < frames; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private async Task StartAsync()
    {
        await _viewModel.StartAsync();
        Pump();
    }

    private DeviceItemViewModel Device(string name) => _viewModel.Devices.Single(d => d.Name == name);

    private IEnumerable<ProgressBar> BusyBars() =>
        _window.GetVisualDescendants().OfType<ProgressBar>().Where(p => p.Classes.Contains("busy"));

    /// <summary>
    /// Switching from 81 % to 55 % must not play a 260 ms drain that never happened (the desktop's BatteryRing fix,
    /// checked where the phone uses it: one DeviceView whose data context is the open device).
    /// </summary>
    [AvaloniaFact]
    public async Task BatteryRing_JumpsToTheNewDevicesLevel_AndStillGlidesForTheSameDevice()
    {
        await StartAsync();
        var buds = Device("Simulated Buds Pro");
        var headset = Device("Simulated Headset");
        buds.LookOptions.Single(o => o.Label == "Unknown · single").Select.Execute(null);
        Assert.True(buds.ShowRing && headset.ShowRing);
        Assert.NotEqual(buds.SummaryPercent, headset.SummaryPercent);

        _viewModel.OpenCommand.Execute(buds);
        Pump();
        var ring = _window.GetVisualDescendants().OfType<BatteryRing>().Single(r => r.IsEffectivelyVisible);
        Assert.Equal(buds.SummaryPercent, ring.Value);

        _viewModel.GoBack();
        _viewModel.OpenCommand.Execute(headset);
        Pump(2); // two frames into a 260 ms glide the ring would still be far from the new level

        Assert.Equal(headset.SummaryPercent, ring.Value);
        Assert.NotEmpty(ring.Transitions!);
    }

    [AvaloniaFact]
    public async Task ReduceMotion_FromThePhone_StopsEveryMovement()
    {
        Platform.PrefersReducedMotion = true;
        await StartAsync();
        _viewModel.ShowComputerCommand.Execute(null);
        Pump();

        Assert.True(_viewModel.ReduceMotion);
        Assert.Contains("reduce-motion", _view.Classes);
        var busy = BusyBars().Where(b => b.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(busy); // "Looking for computers…"
        Assert.All(busy, b => Assert.False(b.IsIndeterminate));
        Assert.All(_window.GetVisualDescendants().OfType<BatteryRing>(), r => Assert.Empty(r.Transitions ?? []));
    }

    /// <summary>Android's setting can change while Samaat is in the background; the activity re-reads it on resume.</summary>
    [AvaloniaFact]
    public async Task ReduceMotion_IsReadAgain_WhenThePhoneComesBack()
    {
        await StartAsync();
        _viewModel.ShowComputerCommand.Execute(null);
        Pump();
        Assert.All(BusyBars().Where(b => b.IsEffectivelyVisible), b => Assert.True(b.IsIndeterminate));

        Platform.PrefersReducedMotion = true;
        _viewModel.RefreshMotion();
        Pump();
        Assert.Contains("reduce-motion", _view.Classes);

        Platform.PrefersReducedMotion = false;
        _viewModel.RefreshMotion();
        Pump();
        Assert.DoesNotContain("reduce-motion", _view.Classes);
        Assert.All(BusyBars().Where(b => b.IsEffectivelyVisible), b => Assert.True(b.IsIndeterminate));
    }

    public async ValueTask DisposeAsync()
    {
        _window.Close();
        _viewModel.Dispose();
        await _runtime.DisposeAsync();
    }
}
