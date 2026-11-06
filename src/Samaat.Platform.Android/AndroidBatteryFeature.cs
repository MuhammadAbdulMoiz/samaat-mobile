using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Samaat.Core.Battery;
using Samaat.Core.Capabilities;
using Samaat.Core.Diagnostics;

namespace Samaat.Platform.Android;

/// <summary>
/// The battery level Android itself keeps for a headset (from HFP/AVRCP). It's behind a hidden method, read through
/// reflection, and also pushed by the system's "battery level changed" broadcast; on phones that refuse both the device
/// simply reports no battery.
/// </summary>
internal sealed class AndroidBatteryFeature : IBatteryFeature, IDisposable
{
    // The hidden constants of BluetoothDevice (ACTION_BATTERY_LEVEL_CHANGED, EXTRA_BATTERY_LEVEL).
    private const string LevelAction = "android.bluetooth.device.action.BATTERY_LEVEL_CHANGED";
    private const string LevelExtra = "android.bluetooth.device.extra.BATTERY_LEVEL";

    private readonly BluetoothDevice _device;
    private readonly string _address;
    private readonly TimeProvider _time;
    private readonly Context _context;
    private BatteryLevelReceiver? _receiver;

    public AndroidBatteryFeature(Context context, BluetoothDevice device, string address, bool connected, TimeProvider time)
    {
        _context = context;
        _device = device;
        _address = address;
        _time = time;
        Register();
        if (connected)
        {
            ReadHiddenLevel();
        }
    }

    public IReadOnlySet<BatteryComponent> Components { get; } = new HashSet<BatteryComponent> { BatteryComponent.Combined };

    public bool ComponentsAreExhaustive => false;

    public CapabilitySource Source => CapabilitySource.OperatingSystem;

    public Verification Verification => Verification.Unverified;

    public BatteryState Current { get; private set; } = BatteryState.Empty;

    public event EventHandler<BatteryState>? Changed;

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ReadHiddenLevel();
        return Task.CompletedTask;
    }

    private void ReadHiddenLevel()
    {
        try
        {
            var method = _device.Class.GetMethod("getBatteryLevel");
            if (method.Invoke(_device) is Java.Lang.Integer level)
            {
                Publish(level.IntValue());
            }
        }
        catch (Java.Lang.Exception)
        {
            // Hidden method missing or refused on this phone.
        }
    }

    private void Publish(int percent)
    {
        if (percent is < 0 or > 100)
        {
            return;
        }

        Current = BatteryState.Empty.With(new BatteryReading(BatteryComponent.Combined, percent, _time.GetUtcNow()));
        Changed?.Invoke(this, Current);
    }

    private void Register()
    {
        try
        {
            _receiver = new BatteryLevelReceiver(this);
            var filter = new IntentFilter(LevelAction);
            // A system broadcast: from Android 14 (API 34) a receiver registered at run time must say whether it takes
            // broadcasts from other apps, and the system counts as one.
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                _context.RegisterReceiver(_receiver, filter, ReceiverFlags.Exported);
            }
            else
            {
                _context.RegisterReceiver(_receiver, filter);
            }
        }
        catch (Exception ex)
        {
            _receiver = null;
            SamaatLog.Warn("Battery", "Listening for Bluetooth battery broadcasts failed", ex);
        }
    }

    private void OnBroadcast(Intent intent)
    {
        if (intent.Action != LevelAction)
        {
            return;
        }

        var from = intent.GetParcelableExtra(BluetoothDevice.ExtraDevice) as BluetoothDevice;
        if (!string.Equals(from?.Address, _address, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // -1 means the device doesn't know its level.
        Publish(intent.GetIntExtra(LevelExtra, -1));
    }

    public void Dispose()
    {
        if (_receiver is null)
        {
            return;
        }

        try
        {
            _context.UnregisterReceiver(_receiver);
        }
        catch (Exception ex)
        {
            SamaatLog.Warn("Battery", "Stopping the Bluetooth battery listener failed", ex);
        }

        _receiver = null;
    }

    private sealed class BatteryLevelReceiver(AndroidBatteryFeature owner) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is null)
            {
                return;
            }

            try
            {
                owner.OnBroadcast(intent);
            }
            catch (Exception ex)
            {
                // A receiver must never throw into the system.
                SamaatLog.Warn("Battery", "Handling a Bluetooth battery broadcast failed", ex);
            }
        }
    }
}
