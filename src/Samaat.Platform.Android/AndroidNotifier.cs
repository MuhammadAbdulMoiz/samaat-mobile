using Android.App;
using Android.Content;
using Samaat.Core.Diagnostics;
using Samaat.Core.Platform;

namespace Samaat.Platform.Android;

/// <summary>
/// Shows battery alerts as Android notifications on the "Battery alerts" channel. Never throws: without the
/// notification permission Android drops the notification, and any failure is logged instead of reaching the caller.
/// </summary>
public sealed class AndroidNotifier(Context context, int smallIcon) : INotifier
{
    public const string ChannelId = "battery-alerts";
    private const int FirstNotificationId = 3000;

    private readonly Context _context = context.ApplicationContext ?? context;
    private int _lastId = FirstNotificationId;
    private bool _channelCreated;

    public void Show(string title, string body)
    {
        try
        {
            if (_context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
            {
                return;
            }

            EnsureChannel(manager);
            var open = _context.PackageManager?.GetLaunchIntentForPackage(_context.PackageName ?? "");
            var pending = open is null ? null : PendingIntent.GetActivity(_context, 0, open, PendingIntentFlags.Immutable);
            var notification = new Notification.Builder(_context, ChannelId)
                .SetSmallIcon(smallIcon)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetContentIntent(pending)
                .SetAutoCancel(true)
                .Build();
            manager.Notify(Interlocked.Increment(ref _lastId), notification);
        }
        catch (Exception ex)
        {
            SamaatLog.Error("Notifications", "Showing a battery alert", ex);
        }
    }

    // Channels exist from Android 8 (API 26), the lowest this app supports.
    private void EnsureChannel(NotificationManager manager)
    {
        if (_channelCreated)
        {
            return;
        }

        manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Battery alerts", NotificationImportance.Default)
        {
            Description = "Warns you when your earbuds' battery is low.",
        });
        _channelCreated = true;
    }
}
