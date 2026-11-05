using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Samaat.Mobile.Runtime;
using Samaat.Mobile.Simulation;
using Samaat.Mobile.ViewModels;
using Samaat.Mobile.Views;
using Samaat.Simulator;

namespace Samaat.Mobile.Tests;

/// <summary>What the worst-case renders broke on the phone, each pinned by a test (break-ui: report, then fix).</summary>
public sealed class WorstCaseFixesTests : IAsyncDisposable
{
    private readonly MobileRuntime _runtime = SimulatedMobile.Create(scenario: SimulatorScenario.WorstCase);
    private readonly MainViewModel _viewModel;

    public WorstCaseFixesTests() => _viewModel = new MainViewModel(_runtime);

    private DeviceItemViewModel Device(string id) => _viewModel.Devices.SingleOrDefault(d => d.Device.Identity.Id == id)
        ?? throw new InvalidOperationException(string.Join(", ", _viewModel.Devices.Select(d => d.Device.Identity.Id)));

    private static void Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>"Jo" has no right-bud level and no case, but the caption said every level is reported.</summary>
    [AvaloniaFact]
    public async Task BatteryCaption_NamesEveryPieceWithoutALevel()
    {
        await _viewModel.StartAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("No level from the right bud yet, and the case isn't reported by this device", Device("worst-short").BatteryReasonText);
        Assert.DoesNotContain("Each level is reported", Device("worst-long").BatteryReasonText);
    }

    /// <summary>A disconnected device with no reading said "Left, right and case aren't reported".</summary>
    [AvaloniaFact]
    public async Task BatteryCaption_ForADisconnectedDeviceWithoutAReading_SaysToConnect()
    {
        await _viewModel.StartAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Connect the earbuds to read their battery", Device("worst-away").BatteryReasonText);
    }

    /// <summary>Devices the simulator gives no Bluetooth address can't be on the phone's audio list, and mustn't crash it.</summary>
    [Fact]
    public void PhoneAudio_ListsOnlyDevicesWithAnAddress()
    {
        var devices = _runtime.Audio.Devices;

        Assert.All(devices, d => Assert.False(string.IsNullOrEmpty(d.Address)));
        Assert.Single(devices);
    }

    /// <summary>The empty list said to pair in Bluetooth settings but offered no way there.</summary>
    [AvaloniaFact]
    public async Task EmptyList_OffersBluetoothSettings()
    {
        await using var runtime = SimulatedMobile.Create(scenario: SimulatorScenario.Empty);
        using var viewModel = new MainViewModel(runtime);
        await viewModel.StartAsync();

        await viewModel.OpenBluetoothSettingsCommand.ExecuteAsync(null);

        Assert.Equal(1, ((NoMobilePlatform)runtime.Platform).SettingsOpened);
    }

    /// <summary>The empty-state panel stretched to the bottom of the screen.</summary>
    [AvaloniaFact]
    public async Task EmptyList_PanelHugsItsContent()
    {
        await using var runtime = SimulatedMobile.Create(scenario: SimulatorScenario.Empty);
        using var viewModel = new MainViewModel(runtime);
        var window = new Window { Width = 360, Height = 892, Content = new MainView { DataContext = viewModel } };
        window.Show();
        await viewModel.StartAsync();
        Settle();

        var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "EmptyState");
        Assert.True(panel.Bounds.Height < 300, $"the empty state is {panel.Bounds.Height:0} px tall");
        window.Close();
    }

    /// <summary>A long toast covered the end of the page, with no room to scroll the last row clear of it.</summary>
    [AvaloniaFact]
    public async Task Toast_LeavesRoomToScrollTheLastRowClear()
    {
        var window = new Window { Width = 360, Height = 892, Content = new MainView { DataContext = _viewModel } };
        window.Show();
        await _viewModel.StartAsync();

        _viewModel.ShowMessage("Paired with Aleksandra Wiśniewska-Kowalczyk's ThinkPad X1 Carbon (work). Open an earbuds page to move them.");
        Settle();

        var toast = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Toast");
        var room = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "ToastRoom");
        Assert.True(room.IsEffectivelyVisible);
        Assert.True(room.Bounds.Height >= toast.Bounds.Height, $"room {room.Bounds.Height:0} px for a {toast.Bounds.Height:0} px toast");

        _viewModel.DismissMessageCommand.Execute(null);
        Settle();
        Assert.False(room.IsEffectivelyVisible);
        window.Close();
    }

    public async ValueTask DisposeAsync()
    {
        _viewModel.Dispose();
        await _runtime.DisposeAsync();
    }
}
