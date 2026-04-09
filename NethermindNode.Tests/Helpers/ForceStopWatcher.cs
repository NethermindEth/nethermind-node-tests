using NLog;

namespace NethermindNode.Tests.Helpers;

/// <summary>
/// Watches for a force-stop.json file. When the file appears (e.g. created by the
/// health monitor), sets a cancellation token that tests can check to stop gracefully,
/// allowing NUnit to generate the test report before exiting.
/// </summary>
public static class ForceStopWatcher
{
    private static readonly CancellationTokenSource _cts = new();
    private static bool _started;
    private static readonly object _lock = new();
    private static string? _reason;

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
            logger.Info("ForceStopWatcher started, polling for force-stop.json");

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
                            _reason = content;
                            logger.Warn($"Force stop file found at {path}: {content}");
                            _cts.Cancel();
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
    /// Throws Assert.Fail if the force stop has been triggered.
    /// Call this in test loops to break out gracefully.
    /// </summary>
    public static void ThrowIfStopRequested()
    {
        if (_cts.IsCancellationRequested)
        {
            Assert.Fail($"Test stopped by force-stop.json: {_reason ?? "no reason provided"}");
        }
    }
}
