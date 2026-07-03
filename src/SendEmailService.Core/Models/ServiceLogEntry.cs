namespace SendEmailService.Core.Models;

/// <summary>
/// Represents an operational log entry written to <c>tbl_EmailServiceLog</c>.
/// </summary>
public class ServiceLogEntry
{
    /// <summary>Log level (Info, Warning, Error, Critical).</summary>
    public string LogLevel { get; set; } = string.Empty;

    /// <summary>Main log message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Full exception detail / stack trace (optional).</summary>
    public string? ExceptionDetail { get; set; }

    /// <summary>Related mail queue item Sno (optional).</summary>
    public int? Sno { get; set; }

    /// <summary>Name of the machine / service instance that generated the log.</summary>
    public string MachineName { get; set; } = Environment.MachineName;
}
