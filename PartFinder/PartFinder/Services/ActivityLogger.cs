using System.Collections.Concurrent;

namespace PartFinder.Services;

/// <summary>
/// Fast, fire-and-forget activity logger.
/// Uses a background queue so UI is never blocked.
/// Logs are written to MongoDB partfinder_audit_logs collection.
/// </summary>
public class ActivityLogger
{
    private readonly MongoAuditService _auditService;
    private readonly AdminSessionStore _session;
    private readonly ConcurrentQueue<AuditDoc> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();

    public ActivityLogger(MongoAuditService auditService, AdminSessionStore session)
    {
        _auditService = auditService;
        _session = session;
        // Start background writer
        _ = Task.Run(ProcessQueueAsync);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Log any user/system activity. Never throws, never blocks UI.</summary>
    public virtual void Log(string eventType, string action, string details)
    {
        _session.Load();
        var user = _session.Email ?? "System";

        _queue.Enqueue(new AuditDoc
        {
            EventType = eventType,
            Action    = action,
            Details   = details,
            User      = user,
            Timestamp = DateTime.UtcNow,
        });

        // Signal background writer
        try { _signal.Release(); } catch { /* ignore */ }
    }

    // Convenience helpers
    public void LogStockChange(string action, string details)   => Log("Stock Change",  action, details);
    public void LogUserAction(string action, string details)    => Log("User Action",   action, details);
    public void LogSystemEvent(string action, string details)   => Log("System Event",  action, details);
    public void LogTemplateChange(string action, string details)=> Log("Template",      action, details);
    public void LogLogin(string userEmail)                      => Log("User Action",   "Login", $"User '{userEmail}' signed in");
    public void LogLogout(string userEmail)                     => Log("User Action",   "Logout", $"User '{userEmail}' signed out");

    // ── Background writer ─────────────────────────────────────────────────────

    private async Task ProcessQueueAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Wait for signal (with timeout so we don't hang forever)
                await _signal.WaitAsync(TimeSpan.FromSeconds(5), _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch { /* timeout is fine */ }

            // Drain the queue
            while (_queue.TryDequeue(out var doc))
            {
                await _auditService.LogAsync(doc).ConfigureAwait(false);
            }
        }
    }
}
