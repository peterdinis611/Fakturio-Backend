namespace Fakturio.Api.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>None | Virtual | Smtp | Resend</summary>
    public string Provider { get; set; } = "None";

    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Fakturio";
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Absolute frontend origin for public invoice links in email bodies
    /// (e.g. https://app.example.com). Optional.
    /// </summary>
    public string? PublicAppBaseUrl { get; set; }

    public EmailSmtpOptions Smtp { get; set; } = new();
    public EmailResendOptions Resend { get; set; } = new();

    public bool IsConfigured
    {
        get
        {
            var provider = Provider?.Trim() ?? string.Empty;
            if (string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(provider))
                return false;

            if (string.Equals(provider, "Virtual", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "Log", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.IsNullOrWhiteSpace(FromAddress))
                return false;

            if (string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase))
                return !string.IsNullOrWhiteSpace(Smtp.Host);

            if (string.Equals(provider, "Resend", StringComparison.OrdinalIgnoreCase))
                return !string.IsNullOrWhiteSpace(Resend.ApiKey);

            return false;
        }
    }
}

public sealed class EmailSmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class EmailResendOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.resend.com/";
}
