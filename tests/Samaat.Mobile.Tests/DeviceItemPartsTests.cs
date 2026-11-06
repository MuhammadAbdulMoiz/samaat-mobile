using Samaat.Core.Battery;
using Samaat.Handoff.Protocol;
using Samaat.Mobile.ViewModels;

namespace Samaat.Mobile.Tests;

/// <summary>
/// The decisions DeviceItemViewModel used to make inline, now plain functions of their inputs: what the battery panel
/// says (BatterySummary) and what the move-to-computer panel offers (ComputerMoveState). Written before the split, from
/// the behaviour the view model had, so the split can't change it.
/// </summary>
public sealed class DeviceItemPartsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static BatteryState State(params (BatteryComponent Component, int? Percent)[] readings) =>
        BatteryState.Empty.With(readings.Select(r => new BatteryReading(r.Component, r.Percent, Now)));

    private const string Address = "02:00:5E:10:00:01";

    [Fact]
    public void Battery_NoFeature_SaysTheDeviceReportsNone()
    {
        var summary = BatterySummary.From(null, isConnected: true);

        Assert.Empty(summary.Cells);
        Assert.False(summary.HasSummary);
        Assert.Equal("–", summary.Value);
        Assert.Equal("", summary.Unit);
        Assert.Equal("–", summary.Text);
        Assert.Equal("This device reports no battery level", summary.Caption);
        Assert.Null(summary.AlertText);
    }

    [Fact]
    public void Label_IsLowestBud_OnlyWhenABudHasALevel()
    {
        Assert.Equal("Lowest bud", BatterySummary.From(State((BatteryComponent.Left, 90), (BatteryComponent.Right, 80)), true).Label);

        var noBudLevels = BatterySummary.From(State((BatteryComponent.Left, null), (BatteryComponent.Right, null), (BatteryComponent.Combined, 70)), true);
        Assert.Equal("Battery", noBudLevels.Label);
        Assert.Equal("70", noBudLevels.Value);

        Assert.Equal("Battery", BatterySummary.From(State((BatteryComponent.Combined, 70)), true).Label);

        var none = BatterySummary.From(null, true);
        Assert.Equal("Battery", none.Label);
        Assert.Equal("–", none.Value);
    }

    [Theory]
    [InlineData(true, "Waiting for a reading")]
    [InlineData(false, "Connect the earbuds to read their battery")]
    public void Battery_NoReadingYet_SaysWhatToDo(bool connected, string caption)
    {
        Assert.Equal(caption, BatterySummary.From(BatteryState.Empty, connected).Caption);
    }

    [Fact]
    public void Battery_EveryPiece_IsInOrder_WithTheLowestBudAsTheSummary()
    {
        var summary = BatterySummary.From(State((BatteryComponent.Case, 64), (BatteryComponent.Right, 81), (BatteryComponent.Left, 93)), true);

        Assert.Equal(["Left bud", "Right bud", "Case"], summary.Cells.Select(c => c.Label));
        Assert.Equal("Each level is reported by the earbuds", summary.Caption);
        Assert.True(summary.HasSummary);
        Assert.Equal("81", summary.Value);
        Assert.Equal("%", summary.Unit);
        Assert.Equal("81%", summary.Text);
        Assert.Equal(81, summary.Percent);
        Assert.False(summary.IsLow || summary.IsCritical);
        Assert.Null(summary.AlertText);
    }

    [Fact]
    public void Battery_TheMostUrgentBud_DecidesTheAlert_AndTheCaseOnlyInforms()
    {
        var low = BatterySummary.From(State((BatteryComponent.Left, 90), (BatteryComponent.Right, 17), (BatteryComponent.Case, 5)), true);
        Assert.True(low.IsLow);
        Assert.False(low.IsCritical);
        Assert.Equal("Right bud low · 17%", low.AlertText);

        var critical = BatterySummary.From(State((BatteryComponent.Left, 8), (BatteryComponent.Right, 17)), true);
        Assert.True(critical.IsCritical);
        Assert.False(critical.IsLow);
        Assert.Equal("Left bud critically low · 8%", critical.AlertText);
    }

    [Fact]
    public void Battery_Caption_NamesEveryPieceWithoutALevel()
    {
        Assert.Equal("No level from the right bud yet, and the case isn't reported by this device",
            BatterySummary.From(State((BatteryComponent.Left, 0), (BatteryComponent.Right, null)), true).Caption);
        Assert.Equal("The case isn't reported by this device",
            BatterySummary.From(State((BatteryComponent.Left, 50), (BatteryComponent.Right, 50)), true).Caption);
        Assert.Equal("No level from the left bud and right bud yet",
            BatterySummary.From(State((BatteryComponent.Left, null), (BatteryComponent.Right, null), (BatteryComponent.Case, 40)), true).Caption);
    }

    [Theory]
    [InlineData(55, true, "Left, right and case aren't reported for this device")]
    [InlineData(null, true, "Waiting for a reading")]
    [InlineData(null, false, "Connect the earbuds to read their battery")]
    public void Battery_SingleLevel_Caption(int? percent, bool connected, string caption)
    {
        Assert.Equal(caption, BatterySummary.From(State((BatteryComponent.Combined, percent)), connected).Caption);
    }

    [Fact]
    public void Computer_NotPaired_OffersPairing()
    {
        var state = ComputerMoveState.From(null, online: false, [], Address, isConnectedHere: false, isMoving: false);

        Assert.Equal("Your computer", state.Title);
        Assert.Equal("Move to your computer", state.SendText);
        Assert.True(state.ShowPair);
        Assert.False(state.CanSend || state.CanBringHere || state.ComputerHasThem || state.IsOnline);
        Assert.StartsWith("Pair Samaat with your computer", state.StateText);
    }

    [Fact]
    public void Computer_Offline_SaysWhy()
    {
        var state = ComputerMoveState.From("Desk PC", online: false, [], Address, false, false);

        Assert.Equal("Desk PC", state.Title);
        Assert.Equal("Can't reach Desk PC. Samaat must be running there, on the same Wi-Fi.", state.StateText);
        Assert.False(state.ShowPair || state.CanSend || state.CanBringHere);
    }

    [Theory]
    [InlineData(null, false, false, "Samaat doesn't know these earbuds' Bluetooth address")]
    [InlineData(Address, false, false, "Desk PC doesn't have these earbuds paired")]
    public void Computer_Online_ButNoDevice_SaysWhy(string? address, bool connectedHere, bool moving, string text)
    {
        var state = ComputerMoveState.From("Desk PC", true, [], address, connectedHere, moving);

        Assert.Equal(text, state.StateText);
        Assert.False(state.CanSend || state.CanBringHere);
    }

    [Fact]
    public void Computer_CantConnectThem_SaysSo()
    {
        var state = ComputerMoveState.From("Desk PC", true, [new PeerDevice(Address, "Buds", false, CanConnect: false, CanDisconnect: false)], Address, true, false);

        Assert.Equal("Desk PC can't connect these earbuds from Samaat", state.StateText);
        Assert.False(state.CanSend || state.CanBringHere);
    }

    [Fact]
    public void Computer_HasThem_OffersBringHere()
    {
        var state = ComputerMoveState.From("Desk PC", true, [new PeerDevice(Address, "Buds", true, true, true)], Address, false, false);

        Assert.Equal("Playing on Desk PC", state.StateText);
        Assert.True(state.ComputerHasThem);
        Assert.True(state.CanBringHere);
        Assert.False(state.CanSend);
    }

    [Fact]
    public void Computer_PhoneHasThem_OffersMoveToComputer_ExceptDuringAMove()
    {
        PeerDevice[] there = [new PeerDevice(Address, "Buds", false, true, true)];

        var idle = ComputerMoveState.From("Desk PC", true, there, Address, isConnectedHere: true, isMoving: false);
        Assert.Equal("Desk PC can take these earbuds", idle.StateText);
        Assert.True(idle.CanSend);
        Assert.False(idle.CanBringHere);

        var moving = ComputerMoveState.From("Desk PC", true, there, Address, isConnectedHere: true, isMoving: true);
        Assert.False(moving.CanSend || moving.CanBringHere);

        var neither = ComputerMoveState.From("Desk PC", true, there, Address, isConnectedHere: false, isMoving: false);
        Assert.Equal("Not connected to Desk PC or this phone", neither.StateText);
        Assert.True(neither.CanSend && neither.CanBringHere);
    }
}
