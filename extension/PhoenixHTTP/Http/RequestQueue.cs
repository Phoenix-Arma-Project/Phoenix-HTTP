namespace PhoenixHttp.Http;

/// <summary>
/// Bounds how many requests run concurrently. Work is handed off to the thread pool immediately so
/// the engine thread never blocks, then each task waits on a semaphore for a free slot before it
/// runs. The concurrency limit can be retuned at runtime by adding or quietly reclaiming permits.
/// </summary>
public sealed class RequestQueue
{
    /// <summary>One permit per allowed concurrent request; tasks acquire and release it.</summary>
    private readonly SemaphoreSlim slots;

    /// <summary>Guards <see cref="maxConcurrent"/> while it is being retuned.</summary>
    private readonly object gate = new object();

    /// <summary>Current concurrency target, mirrored by the semaphore's permit count.</summary>
    private int maxConcurrent;

    /// <summary>Creates the queue with an initial concurrency limit.</summary>
    /// <param name="maxConcurrent">Number of requests allowed to run at once.</param>
    public RequestQueue(int maxConcurrent)
    {
        this.maxConcurrent = maxConcurrent;
        slots = new SemaphoreSlim(maxConcurrent);
    }

    /// <summary>
    /// Schedules work to run as soon as a slot is free. Returns at once; the work runs on the thread
    /// pool, so the calling engine thread is never held.
    /// </summary>
    /// <param name="work">The asynchronous operation to run under the concurrency limit.</param>
    public void Enqueue(Func<Task> work) => _ = Task.Run(() => RunAsync(work));

    /// <summary>
    /// Retunes the concurrency limit at runtime. Raising it releases the extra permits; lowering it
    /// reclaims permits by waiting on them in the background, which lets in-flight requests drain
    /// naturally before the lower limit takes full effect.
    /// </summary>
    /// <param name="value">The new limit; values below one are treated as one.</param>
    public void SetMaxConcurrent(int value)
    {
        int target = Math.Max(1, value);
        lock (gate)
        {
            int delta = target - maxConcurrent;
            maxConcurrent = target;

            if (delta > 0)
            {
                slots.Release(delta);
            }
            else
            {
                for (int removed = 0; removed < -delta; removed++)
                {
                    _ = slots.WaitAsync();
                }
            }
        }
    }

    /// <summary>Acquires a slot, runs the work, logs any failure, and always releases the slot.</summary>
    /// <param name="work">The operation to run.</param>
    private async Task RunAsync(Func<Task> work)
    {
        await slots.WaitAsync();
        try
        {
            await work();
        }
        catch (Exception exception)
        {
            Extension.Logger.Error($"Queued work threw: {exception}");
        }
        finally
        {
            slots.Release();
        }
    }
}
