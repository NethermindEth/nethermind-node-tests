using NLog;

namespace NethermindNode.Tests.Helpers;

/// <summary>
/// Watches for a force-stop.json file. When the file appears (e.g. created by the
/// health monitor), or a background check calls <see cref="RequestStop"/>, sets a
/// cancellation token that tests can check to stop gracefully, allowing NUnit to
/// generate the test report before exiting.
/// </summary>
public static class ForceStopWatcher
{
    private static readonly CancellationTokenSource _cts = new();
    private static bool _started;
    private static readonly object _lock = new();
    private static string? _reason;
    private static string? _stopMessage;

    public static CancellationToken Token => _cts.Token;
    public static string? StopReason => _reason;

    /// <summary>
    /// Starts the file watcher (idempotent — only starts once).
    /// Polls for force-stop.json in the working directory every 5 seconds.
    /// </summary>
    public static void Start()
    {
        lock (_lock)
        {
            if (_started) return;
            _started = true;
        }

        Task.Run(() =>
        {
            var logger = LogManager.GetLogger("ForceStopWatcher");
            logger.Info("[FORCESTOP] Watcher active \u2014 polling for force-stop.json");

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    // Check in working directory and parent (health monitor may write to either)
                    var paths = new[]
                    {
                        Path.Combine(Directory.GetCurrentDirectory(), "force-stop.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "force-stop.json"),
                        "/tmp/force-stop.json",
                    };

                    foreach (var path in paths)
                    {
                        if (File.Exists(path))
                        {
                            var content = File.ReadAllText(path);
                            logger.Warn($"[FORCESTOP] \u26a0 Stop requested: {content}");
                            Stop(content, $"Test stopped by force-stop.json: {content}");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Debug($"ForceStopWatcher poll error: {ex.Message}");
                }

                Thread.Sleep(5000);
            }
        });
    }

    /// <summary>
    /// Stops the running test from inside the process, for checks that run beside it and so cannot fail it
    /// directly. The next <see cref="ThrowIfStopRequested"/> fails the test with <paramref name="reason"/>.
    /// </summary>
    public static void RequestStop(string reason)
    {
        LogManager.GetLogger("ForceStopWatcher").Warn($"[FORCESTOP] \u26a0 Stop requested: {reason}");
        Stop(reason, reason);
    }

    private static void Stop(string reason, string stopMessage)
    {
        lock (_lock)
        {
            if (_cts.IsCancellationRequested) return;
            _reason = reason;
            _stopMessage = stopMessage;
            _cts.Cancel();
        }
    }

    /// <summary>
    /// Throws Assert.Fail if the force stop has been triggered.
    /// Call this in test loops to break out gracefully.
    /// </summary>
    public static void ThrowIfStopRequested()
    {
        if (_cts.IsCancellationRequested)
        {
            Assert.Fail(_stopMessage ?? "Test stopped: no reason provided");
        }
    }
}
