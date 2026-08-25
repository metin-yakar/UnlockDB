namespace AxarDB.Definitions
{
    public class DatabaseSettings
    {
        public double MemoryLimitPercentage { get; set; } = 0.4;
        public long BulkStoreMaxCacheBytes { get; set; } = 50L * 1024 * 1024;
        public int MaxRecursionDepth { get; set; } = 100;
        public int QueryTimeoutMinutes { get; set; } = 10;
        public double QueuePollIntervalSeconds { get; set; } = 1.0;
        public double TimezoneOffset { get; set; } = 3.0;
    }
}
