using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DWSIM.Automation.DynamicRunner;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Dynamics;

namespace DWSIM.MCPServer.Sessions
{
    /// <summary>Where a job is in its life.</summary>
    public enum DynamicsJobState
    {
        /// <summary>Queued but not started.</summary>
        Pending,
        /// <summary>Running now.</summary>
        Running,
        /// <summary>Finished; results are available.</summary>
        Completed,
        /// <summary>Stopped by an abort request.</summary>
        Aborted,
        /// <summary>Stopped by an error.</summary>
        Failed
    }

    /// <summary>One dynamic run or tuning search, and whatever it has produced so far.</summary>
    public sealed class DynamicsJob
    {
        internal DynamicsJob(string id, string flowsheetId, string kind)
        {
            Id = id;
            FlowsheetId = flowsheetId;
            Kind = kind;
            StartedAt = DateTime.UtcNow;
        }

        /// <summary>Handle the caller polls with.</summary>
        public string Id { get; }

        /// <summary>The flowsheet this job runs on.</summary>
        public string FlowsheetId { get; }

        /// <summary>"run" or "tune".</summary>
        public string Kind { get; }

        /// <summary>Where the job is.</summary>
        public volatile DynamicsJobState State = DynamicsJobState.Pending;

        /// <summary>Simulated seconds reached.</summary>
        public double CurrentSeconds;

        /// <summary>Simulated seconds the run is aiming for.</summary>
        public double TotalSeconds;

        /// <summary>Integration steps solved so far.</summary>
        public int Steps;

        /// <summary>When the job started.</summary>
        public DateTime StartedAt { get; }

        /// <summary>When the job stopped, or null while it runs.</summary>
        public DateTime? FinishedAt;

        /// <summary>The run's results, once it has any.</summary>
        public DynamicsResult Result;

        /// <summary>The tuning results, for a tuning job.</summary>
        public PidTuningResult Tuning;

        /// <summary>What went wrong, or null.</summary>
        public Exception Error;

        internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();

        private readonly object _logLock = new object();
        private readonly Queue<string> _log = new Queue<string>();

        /// <summary>How far along the job is, from 0 to 1. Null when there is nothing to measure against.</summary>
        public double? Progress
        {
            get
            {
                if (TotalSeconds <= 0 || double.IsInfinity(TotalSeconds) || TotalSeconds > int.MaxValue) return null;
                var fraction = CurrentSeconds / TotalSeconds;
                return fraction < 0 ? 0 : fraction > 1 ? 1 : fraction;
            }
        }

        /// <summary>Wall-clock time the job has taken so far.</summary>
        public TimeSpan Elapsed => (FinishedAt ?? DateTime.UtcNow) - StartedAt;

        /// <summary>True once the job has stopped, whatever the reason.</summary>
        public bool IsFinished =>
            State == DynamicsJobState.Completed || State == DynamicsJobState.Aborted || State == DynamicsJobState.Failed;

        /// <summary>Appends a log line, keeping only the most recent ones.</summary>
        public void Log(string line)
        {
            lock (_logLock)
            {
                _log.Enqueue(line);
                while (_log.Count > 200) _log.Dequeue();
            }
        }

        /// <summary>The most recent log lines, oldest first.</summary>
        public IReadOnlyList<string> RecentLog(int count = 20)
        {
            lock (_logLock)
            {
                return _log.Skip(Math.Max(0, _log.Count - count)).ToList();
            }
        }
    }

    /// <summary>
    /// Runs dynamic integrations and tuning searches in the background, so a tool call can start
    /// one and return a handle instead of holding a connection open for minutes.
    /// </summary>
    /// <remarks>
    /// Only one job per flowsheet at a time, and — because integration drives process-wide solver
    /// state — only one integration in the process at a time. A caller asking for a second gets a
    /// clear refusal rather than a silent queue or a corrupted run.
    /// </remarks>
    public sealed class DynamicsJobManager
    {
        private const int MaxRecords = 8;
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

        private readonly ConcurrentDictionary<string, DynamicsJob> _jobs =
            new ConcurrentDictionary<string, DynamicsJob>();

        /// <summary>Starts a job, or throws when this flowsheet or the process is already busy.</summary>
        /// <param name="flowsheetId">The flowsheet the job runs on.</param>
        /// <param name="kind">"run" or "tune", for reporting.</param>
        /// <param name="work">The job body. It receives the record so it can report progress.</param>
        public DynamicsJob Start(string flowsheetId, string kind, Action<DynamicsJob> work)
        {
            Prune();

            var existing = _jobs.Values.FirstOrDefault(j => j.FlowsheetId == flowsheetId && !j.IsFinished);
            if (existing != null)
            {
                throw new InvalidOperationException(
                    "Flowsheet " + flowsheetId + " already has a dynamics job running (" + existing.Id +
                    "). Wait for it, or abort it with dwsim_dynamics_abort.");
            }

            if (IntegratorRunner.IsRunning)
            {
                throw new InvalidOperationException(
                    "Another dynamic integration is already running in this process. Integration uses " +
                    "process-wide solver state, so runs cannot overlap.");
            }

            var job = new DynamicsJob("dyn_" + Guid.NewGuid().ToString("N").Substring(0, 8), flowsheetId, kind);
            _jobs[job.Id] = job;

            Task.Run(() =>
            {
                job.State = DynamicsJobState.Running;
                try
                {
                    work(job);
                    if (job.State == DynamicsJobState.Running)
                    {
                        // A run that reports errors through its result has failed just as surely as
                        // one that threw; only the reporting channel differs.
                        if (job.Error != null || (job.Result != null && job.Result.Errors.Count > 0))
                            job.State = DynamicsJobState.Failed;
                        else if (job.Result != null && job.Result.Aborted)
                            job.State = DynamicsJobState.Aborted;
                        else
                            job.State = DynamicsJobState.Completed;
                    }
                }
                catch (Exception ex)
                {
                    job.Error = ex;
                    job.State = DynamicsJobState.Failed;
                    job.Log("failed: " + ex.Message);
                }
                finally
                {
                    job.FinishedAt = DateTime.UtcNow;
                }
            });

            return job;
        }

        /// <summary>Looks a job up by handle.</summary>
        public DynamicsJob Get(string runId)
        {
            DynamicsJob job;
            if (_jobs.TryGetValue(runId, out job)) return job;
            throw new InvalidOperationException("No dynamics run with id '" + runId + "'. It may have expired.");
        }

        /// <summary>Asks a running job to stop at the next step boundary.</summary>
        public DynamicsJob Abort(string runId)
        {
            var job = Get(runId);
            if (!job.IsFinished) job.Cancellation.Cancel();
            return job;
        }

        /// <summary>Every job still held, newest first.</summary>
        public IReadOnlyList<DynamicsJob> List()
        {
            return _jobs.Values.OrderByDescending(j => j.StartedAt).ToList();
        }

        private void Prune()
        {
            var now = DateTime.UtcNow;

            foreach (var job in _jobs.Values.Where(j => j.IsFinished && now - j.StartedAt > Ttl).ToList())
                _jobs.TryRemove(job.Id, out _);

            var finished = _jobs.Values.Where(j => j.IsFinished).OrderBy(j => j.StartedAt).ToList();
            for (var i = 0; i < finished.Count - MaxRecords; i++)
                _jobs.TryRemove(finished[i].Id, out _);
        }
    }
}
