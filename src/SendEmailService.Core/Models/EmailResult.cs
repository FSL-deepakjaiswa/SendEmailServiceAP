namespace SendEmailService.Core.Models;

/// <summary>
/// The result of a single email send attempt.
/// </summary>
public class EmailResult
{
    /// <summary>Whether the email was delivered successfully.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// SparkPost transmission identifier returned on a successful send.
    /// <c>null</c> on failure.
    /// </summary>
    public string? TransmissionId { get; set; }

    /// <summary>
    /// Human-readable error message when <see cref="IsSuccess"/> is <c>false</c>.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Creates a successful result.</summary>
    public static EmailResult Success(string transmissionId) =>
        new() { IsSuccess = true, TransmissionId = transmissionId };

    /// <summary>Creates a failure result.</summary>
    public static EmailResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
