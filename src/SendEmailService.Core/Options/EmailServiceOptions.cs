using System.ComponentModel.DataAnnotations;

namespace SendEmailService.Core.Options;

/// <summary>
/// Configuration options for the email processing service.
/// Bound from <c>appsettings.json</c> section <c>"EmailServiceOptions"</c>.
/// </summary>
public class EmailServiceOptions
{
    /// <summary>
    /// How often (in seconds) the worker polls for pending emails.
    /// Default: 30 seconds.
    /// </summary>
    [Range(5, 3600)]
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of records to fetch per polling cycle.
    /// Default: 50.
    /// </summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Maximum number of send attempts before archiving as failed.
    /// Default: 3.
    /// </summary>
    [Range(1, 10)]
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Maximum number of emails to process in parallel per cycle.
    /// Default: 10.
    /// </summary>
    [Range(1, 100)]
    public int MaxParallelism { get; set; } = 10;

    /// <summary>
    /// Minutes after which a record stuck in 'P' (Processing) status is
    /// considered stale and reset to 'N' by the cleanup worker.
    /// Default: 5 minutes.
    /// </summary>
    [Range(1, 60)]
    public int StaleRecordTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Unique identifier for this service instance.
    /// Used for concurrency tracking in the <c>PickedBy</c> column.
    /// Defaults to the machine name when not configured.
    /// </summary>
    public string InstanceId { get; set; } = Environment.MachineName;
}
