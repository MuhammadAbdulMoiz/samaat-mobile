using Samaat.Core.Battery;
using Samaat.Core.Capabilities;
using Samaat.Core.Diagnostics;

namespace Samaat.Platform.Android;

/// <summary>
/// A driver's or GATT's battery with the operating system's level behind it: the primary's state wins once it has any
/// reading with a percentage, otherwise the OS level shows. Plain C# (no Android types) so tests can compile this file.
/// </summary>
internal sealed class FallbackBatteryFeature : IBatteryFeature, IAsyncDisposable
{
    private readonly IBatteryFeature _primary;
    private readonly IBatteryFeature _os;

    public FallbackBatteryFeature(IBatteryFeature primary, IBatteryFeature os)
    {
        _primary = primary;
        _os = os;
        _primary.Changed += OnInnerChanged;
        _os.Changed += OnInnerChanged;
    }

    private bool PrimaryHasLevel => _primary.Current.Readings.Any(r => r.Percent is not null);

    private IBatteryFeature Active => PrimaryHasLevel ? _primary : _os;

    public IReadOnlySet<Samaat.Core.Battery.BatteryComponent> Components => Active.Components;

    public bool ComponentsAreExhaustive => Active.ComponentsAreExhaustive;

    public CapabilitySource Source => Active.Source;

    public Verification Verification => Active.Verification;

    public BatteryState Current => Active.Current;

    public event EventHandler<BatteryState>? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await RefreshOneAsync(_primary, cancellationToken);
        await RefreshOneAsync(_os, cancellationToken);
    }

    private static async Task RefreshOneAsync(IBatteryFeature feature, CancellationToken cancellationToken)
    {
        try
        {
            await feature.RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SamaatLog.Warn("Battery", "Refreshing a battery source failed", ex);
        }
    }

    private void OnInnerChanged(object? sender, BatteryState state) => Changed?.Invoke(this, Current);

    public async ValueTask DisposeAsync()
    {
        _primary.Changed -= OnInnerChanged;
        _os.Changed -= OnInnerChanged;
        await DisposeOneAsync(_primary);
        await DisposeOneAsync(_os);
    }

    private static async ValueTask DisposeOneAsync(IBatteryFeature feature)
    {
        switch (feature)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
