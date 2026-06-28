namespace Aprs.Desktop.Configuration;

/// <summary>
/// A named, reusable APRS message template. The operator selects a template
/// from a dropdown in the Message Center compose area and it pre-fills the
/// recipient and/or body fields.
/// </summary>
public sealed record MessageTemplate(
    string Id,
    string Name,
    string Recipient,
    string Body)
{
    /// <summary>Creates a new template with a generated ID.</summary>
    public static MessageTemplate Create(string name, string recipient, string body)
        => new(Guid.NewGuid().ToString("N")[..8], name, recipient, body);
}
