using System.Collections.Concurrent;
using Grpc.Core;
using Trace = System.Diagnostics.Trace;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Serializes all UI Automation / global-input work onto a single long-lived
    /// dedicated worker thread.
    ///
    /// Why a dedicated thread:
    /// <list type="bullet">
    /// <item>UIA element access, simulated mouse/keyboard, and the static
    /// <see cref="ElementCache"/> are a single shared resource. Running gRPC calls
    /// concurrently lets clients fight over the one mouse cursor / keyboard focus and
    /// races the cached COM references. One worker makes every operation atomic w.r.t.
    /// every other.</item>
    /// <item>It keeps slow UIA tree walks off Kestrel's request threads.</item>
    /// </list>
    ///
    /// Why <b>MTA</b> (not STA): UI Automation <i>client</i> code (observing other
    /// processes, which is what this server does) is documented to run on MTA threads;
    /// STA is for UIA <i>providers</i> and invites cross-apartment reentrancy deadlocks.
    ///
    /// The queue is bounded: when the backlog exceeds the configured depth, new work is
    /// rejected with <see cref="StatusCode.ResourceExhausted"/> rather than queueing
    /// unbounded latency. (Designed for a single active client; the cap is a safety net.)
    /// </summary>
    public sealed class UiaExecutor : IDisposable
    {
        private sealed class WorkItem
        {
            public Action Run = static () => { };
        }

        private readonly BlockingCollection<WorkItem> _queue =
            new(new ConcurrentQueue<WorkItem>());
        private readonly Thread _worker;
        private readonly int _maxQueueDepth;

        // queued + currently running. Used as the backpressure signal.
        private int _pending;

        /// <summary>Queued + currently running work items (diagnostics).</summary>
        public int Pending => Volatile.Read(ref _pending);

        /// <summary>Configured backlog cap before new work is rejected (diagnostics).</summary>
        public int MaxQueueDepth => _maxQueueDepth;

        // True only on the worker thread, so re-entrant RunAsync calls (a handler that
        // marshals while already running marshaled work) execute inline instead of
        // enqueueing onto the thread they are running on — which would deadlock.
        [ThreadStatic] private static bool _onWorkerThread;

        public UiaExecutor(int maxQueueDepth = 32)
        {
            _maxQueueDepth = maxQueueDepth < 1 ? 1 : maxQueueDepth;
            _worker = new Thread(WorkLoop)
            {
                IsBackground = true,
                Name = "UIA-Worker"
            };
            _worker.SetApartmentState(ApartmentState.MTA);
            _worker.Start();
        }

        /// <summary>
        /// Marshals <paramref name="work"/> onto the worker thread and returns a task
        /// that completes with its result. Throws <see cref="RpcException"/>
        /// (<see cref="StatusCode.ResourceExhausted"/>) synchronously when the queue is full.
        /// </summary>
        public Task<T> RunAsync<T>(Func<T> work, CancellationToken ct = default)
        {
            // Re-entrancy: already on the worker thread → run inline, no enqueue.
            if (_onWorkerThread)
            {
                try { return Task.FromResult(work()); }
                catch (Exception ex) { return Task.FromException<T>(ex); }
            }

            if (Interlocked.Increment(ref _pending) > _maxQueueDepth)
            {
                Interlocked.Decrement(ref _pending);
                throw new RpcException(new Status(
                    StatusCode.ResourceExhausted,
                    "UI automation worker is busy; retry shortly."));
            }

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var item = new WorkItem
            {
                Run = () =>
                {
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        tcs.TrySetResult(work());
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.TrySetCanceled(ct);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _pending);
                    }
                }
            };

            try
            {
                _queue.Add(item);
            }
            catch (InvalidOperationException)
            {
                // Queue completed (shutting down).
                Interlocked.Decrement(ref _pending);
                throw new RpcException(new Status(
                    StatusCode.Unavailable, "Server is shutting down."));
            }

            return tcs.Task;
        }

        /// <summary>
        /// Marshals an action with no return value onto the worker thread.
        /// </summary>
        public Task RunAsync(Action work, CancellationToken ct = default)
            => RunAsync(() => { work(); return true; }, ct);

        private void WorkLoop()
        {
            _onWorkerThread = true;
            try
            {
                foreach (var item in _queue.GetConsumingEnumerable())
                {
                    item.Run();
                }
            }
            catch (Exception ex)
            {
                // The loop itself should never throw (item.Run swallows). Log defensively.
                Trace.WriteLine($"[UiaExecutor] Worker loop terminated unexpectedly: {ex}");
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _worker.Join(TimeSpan.FromSeconds(2));
            _queue.Dispose();
        }
    }
}
