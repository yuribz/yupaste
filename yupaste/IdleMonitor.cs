namespace Yupaste;

/// <summary>Tracks when the server last did real work, for the idle shutdown.</summary>
public sealed class ActivityTracker
{
    private readonly long? _timeoutMs;
    private long _lastActivityMs = Environment.TickCount64;
    private int _activeRequests;

    public ActivityTracker(StartupOptions options)
    {
        _timeoutMs = options.IdleTimeoutMinutes > 0 ? options.IdleTimeoutMinutes * 60_000L : null;
    }

    public bool Enabled => _timeoutMs is not null;

    public void RequestStarted() => Interlocked.Increment(ref _activeRequests);

    /// <summary>The timer restarts when a request ends, so a long download counts up to its last byte.</summary>
    public void RequestEnded()
    {
        Interlocked.Exchange(ref _lastActivityMs, Environment.TickCount64);
        Interlocked.Decrement(ref _activeRequests);
    }

    /// <summary>Null when the idle shutdown is disabled.</summary>
    public int? SecondsUntilShutdown()
    {
        if (_timeoutMs is not long timeoutMs) return null;
        if (Volatile.Read(ref _activeRequests) > 0) return (int)(timeoutMs / 1000);

        long remainingMs = timeoutMs - (Environment.TickCount64 - Interlocked.Read(ref _lastActivityMs));
        return remainingMs > 0 ? (int)Math.Ceiling(remainingMs / 1000.0) : 0;
    }
}

/// <summary>Stops the application once the tracker's idle timeout has passed.</summary>
public sealed class IdleShutdownService(
    ActivityTracker tracker,
    StartupOptions options,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!tracker.Enabled) return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (tracker.SecondsUntilShutdown() == 0)
            {
                Console.WriteLine($"No activity for {options.IdleTimeoutMinutes} min, shutting down.");
                lifetime.StopApplication();
                return;
            }
        }
    }
}
