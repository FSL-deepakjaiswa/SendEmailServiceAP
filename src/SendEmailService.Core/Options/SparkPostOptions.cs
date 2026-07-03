using System.ComponentModel.DataAnnotations;

namespace SendEmailService.Core.Options;

/// <summary>
/// Configuration for the SparkPost email provider.
/// Bound from <c>appsettings.json</c> section <c>"SparkPostOptions"</c>.
/// </summary>
public class SparkPostOptions
{
    /// <summary>SparkPost API key.</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Sender email address (e.g., <c>noreply@yourdomain.com</c>).</summary>
    [Required]
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>Sender display name (e.g., <c>Your Company</c>).</summary>
    [Required]
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// SparkPost API base URL.
    /// Default: <c>https://api.sparkpost.com/api/v1</c>.
    /// EU customers should use <c>https://api.eu.sparkpost.com/api/v1</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.sparkpost.com/api/v1";
}
