namespace STailor.Api.Sync;

public sealed class SyncWorkerOptions
{
    public bool Enabled { get; set; }

    public bool DryRun { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 30;

    public int MaxBatchSize { get; set; } = 25;
}
