using Samaat.Handoff.Protocol;

namespace Samaat.Mobile.ViewModels;

/// <summary>
/// What the move-to-computer panel offers for one pair of earbuds: the computer's name, a line saying where the
/// earbuds are (or why they can't move), and which of "Move to …" and "Bring to this phone" is possible.
/// A plain function of its inputs.
/// </summary>
public sealed record ComputerMoveState(
    string Title,
    string SendText,
    string StateText,
    bool IsOnline,
    bool ShowPair,
    bool ComputerHasThem,
    bool CanSend,
    bool CanBringHere)
{
    /// <param name="computerName">The paired computer's name, or null when the phone isn't paired.</param>
    /// <param name="online">Whether the computer link is up.</param>
    /// <param name="computerDevices">The earbuds the computer reports.</param>
    /// <param name="address">These earbuds' normalised Bluetooth address, or null when unknown.</param>
    /// <param name="isConnectedHere">Whether the earbuds are connected to this phone.</param>
    /// <param name="isMoving">Whether a move is already running.</param>
    public static ComputerMoveState From(string? computerName, bool online, IReadOnlyList<PeerDevice> computerDevices, string? address,
        bool isConnectedHere, bool isMoving)
    {
        var name = computerName ?? "your computer";
        var there = address is null ? null : computerDevices.FirstOrDefault(d => d.Address == address);
        var text =
            computerName is null ? "Pair Samaat with your computer to move these earbuds between it and this phone, even if they connect to one device at a time"
            : !online ? $"Can't reach {name}. Samaat must be running there, on the same Wi-Fi."
            : address is null ? "Samaat doesn't know these earbuds' Bluetooth address"
            : there is null ? $"{name} doesn't have these earbuds paired"
            : !there.CanConnect ? $"{name} can't connect these earbuds from Samaat"
            : there.Connected ? $"Playing on {name}"
            : isConnectedHere ? $"{name} can take these earbuds"
            : $"Not connected to {name} or this phone";
        var ready = online && there is { CanConnect: true } && !isMoving;
        return new ComputerMoveState(
            Title: computerName ?? "Your computer",
            SendText: $"Move to {name}",
            StateText: text,
            IsOnline: online,
            ShowPair: computerName is null,
            ComputerHasThem: online && there?.Connected == true,
            CanSend: ready && !there!.Connected,
            CanBringHere: ready && !isConnectedHere);
    }
}
