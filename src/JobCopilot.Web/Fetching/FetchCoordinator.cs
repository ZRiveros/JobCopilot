using JobCopilot.Application.JobAds;

namespace JobCopilot.Web.Fetching;

/// <summary>
/// The meeting point between the background worker and the UI: the UI can ask for a fetch right
/// now and observe the state, while the worker stays the only one that ever runs a fetch – so two
/// fetches can never overlap.
/// </summary>
public sealed class FetchCoordinator : IDisposable
{
    private readonly SemaphoreSlim _trigger = new(0, 1);
    private readonly Lock _gate = new();

    public bool IsRunning { get; private set; }
    public DateTimeOffset? LastCompletedAt { get; private set; }
    public IngestionSummary? LastSummary { get; private set; }

    /// <summary>True when the last run crashed outright (as opposed to individual searches failing).</summary>
    public bool LastRunFailed { get; private set; }

    /// <summary>Raised on a background thread whenever the state changes.</summary>
    public event Action? Changed;

    /// <summary>Asks the worker to fetch as soon as possible. Repeated requests collapse into one.</summary>
    public void RequestRun()
    {
        lock (_gate)
        {
            if (_trigger.CurrentCount == 0)
            {
                _trigger.Release();
            }
        }
    }

    /// <summary>Completes when a fetch has been requested or the timeout has passed.</summary>
    internal Task WaitForNextRunAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _trigger.WaitAsync(timeout, cancellationToken);

    internal void MarkStarted()
    {
        IsRunning = true;
        Changed?.Invoke();
    }

    internal void MarkCompleted(IngestionSummary summary, DateTimeOffset now)
    {
        IsRunning = false;
        LastRunFailed = false;
        LastSummary = summary;
        LastCompletedAt = now;
        Changed?.Invoke();
    }

    internal void MarkFailed(DateTimeOffset now)
    {
        IsRunning = false;
        LastRunFailed = true;
        LastCompletedAt = now;
        Changed?.Invoke();
    }

    public void Dispose() => _trigger.Dispose();
}
