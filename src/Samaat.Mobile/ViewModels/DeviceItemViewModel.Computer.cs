using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Samaat.Handoff;
using Samaat.Handoff.Protocol;

namespace Samaat.Mobile.ViewModels;

// Moving to and from the computer: works with any earbuds (decided by ComputerMoveState).
public sealed partial class DeviceItemViewModel
{
    [ObservableProperty]
    public partial string ComputerTitle { get; set; } = "Your computer";

    [ObservableProperty]
    public partial string ComputerStateText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsComputerOnline { get; set; }

    [ObservableProperty]
    public partial bool ShowPairComputer { get; set; }

    [ObservableProperty]
    public partial bool CanSendToComputer { get; set; }

    [ObservableProperty]
    public partial bool CanBringHere { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnect))]
    public partial bool ComputerHasThem { get; set; }

    [ObservableProperty]
    public partial string SendText { get; set; } = "Move to computer";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProgress))]
    public partial string? ProgressText { get; set; }

    public bool ShowProgress => ProgressText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSuccess), nameof(ShowFailure))]
    public partial string? ResultText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSuccess), nameof(ShowFailure))]
    public partial bool ResultIsError { get; set; }

    public bool ShowSuccess => ResultText is not null && !ResultIsError;

    public bool ShowFailure => ResultText is not null && ResultIsError;

    [ObservableProperty]
    public partial bool IsMoving { get; set; }

    public Task PendingMove { get; private set; } = Task.CompletedTask;

    [RelayCommand]
    private void PairComputer() => _openComputer();

    [RelayCommand]
    private Task SendToComputerAsync() => PendingMove = MoveAsync(HandoffDirection.ToComputer);

    [RelayCommand]
    private Task BringHereAsync() => PendingMove = MoveAsync(HandoffDirection.ToPhone);

    private void ApplyComputerState()
    {
        var link = _runtime.Computer;
        var state = ComputerMoveState.From(link.Computer?.Name, link.State == ComputerLinkState.Online, link.ComputerDevices, Address,
            IsConnected, IsMoving);
        ComputerTitle = state.Title;
        SendText = state.SendText;
        IsComputerOnline = state.IsOnline;
        ShowPairComputer = state.ShowPair;
        ComputerStateText = state.StateText;
        ComputerHasThem = state.ComputerHasThem;
        CanSendToComputer = state.CanSend;
        CanBringHere = state.CanBringHere;
    }

    private async Task MoveAsync(HandoffDirection direction)
    {
        if (Address is null || IsMoving)
        {
            return;
        }

        IsMoving = true;
        ResultText = null;
        Refresh();
        var result = await _runtime.Computer.RequestMoveAsync(Address, direction);
        IsMoving = false;
        ProgressText = null;
        ResultText = result.Message;
        ResultIsError = !result.Succeeded;
        if (!result.Succeeded)
        {
            _onMessage(result.Message);
        }

        Refresh();
    }

    private void OnProgress(object? sender, HandoffProgressMessage progress)
    {
        if (MessageCodec.NormalizeAddress(progress.Address) != Address)
        {
            return;
        }

        _post(() =>
        {
            if (progress.Stage == HandoffStage.Done)
            {
                ProgressText = null;
                if (!IsMoving)
                {
                    // A move started on the computer.
                    ResultText = progress.Text;
                    ResultIsError = false;
                }
            }
            else
            {
                ProgressText = progress.Text;
            }
        });
    }
}
