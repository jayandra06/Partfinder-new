using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace PartFinder.Services;

public sealed class InviteEmailConfig
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "PartFinder";
    public string DownloadLink { get; init; } = "https://shipspan.com";
}

public static class InviteEmailClient
{
    private static readonly InviteEmailConfig Config = LoadConfig();

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Config.Host)
        && !string.IsNullOrWhiteSpace(Config.FromEmail)
        && !string.IsNullOrWhiteSpace(Config.Username)
        && !string.IsNullOrWhiteSpace(Config.Password);

    public static void SendInvite(
        string toEmail,
        string recipientName,
        string orgCode,
        string role,
        string temporaryPassword)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "InviteEmail is not configured in appsettings.json (Host, Username, Password, FromEmail).");
        }

        using var client = new SmtpClient(Config.Host, Config.Port)
        {
            EnableSsl = Config.UseSsl,
            Credentials = new NetworkCredential(Config.Username, Config.Password),
        };

        using var msg = new MailMessage
        {
            From = new MailAddress(Config.FromEmail, Config.FromName),
            Subject = "PartFinder Invite",
            IsBodyHtml = false,
            Body =
                $"Hello {recipientName}," + Environment.NewLine + Environment.NewLine +
                "You have been invited to PartFinder." + Environment.NewLine +
                $"Organization code: {orgCode}" + Environment.NewLine +
                $"Email: {toEmail}" + Environment.NewLine +
                $"Temporary password: {temporaryPassword}" + Environment.NewLine +
                $"Role: {role}" + Environment.NewLine + Environment.NewLine +
                $"Download app: {Config.DownloadLink}" + Environment.NewLine + Environment.NewLine +
                "After installing, enter your organization code, then sign in with your invited email and temporary password." + Environment.NewLine +
                "You can change your password after first sign-in.",
        };

        msg.To.Add(new MailAddress(toEmail));
        client.Send(msg);
    }

    private static InviteEmailConfig LoadConfig()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return new InviteEmailConfig();
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("InviteEmail", out var root))
            {
                return new InviteEmailConfig();
            }

            var host = root.TryGetProperty("Host", out var hostNode) ? hostNode.GetString() : null;
            var port = root.TryGetProperty("Port", out var portNode) && portNode.TryGetInt32(out var pv) ? pv : 587;
            var useSsl = root.TryGetProperty("UseSsl", out var sslNode) && sslNode.ValueKind == JsonValueKind.True;
            var username = root.TryGetProperty("Username", out var userNode) ? userNode.GetString() : null;
            var password = root.TryGetProperty("Password", out var passNode) ? passNode.GetString() : null;
            var fromEmail = root.TryGetProperty("FromEmail", out var fromNode) ? fromNode.GetString() : null;
            var fromName = root.TryGetProperty("FromName", out var fromNameNode) ? fromNameNode.GetString() : null;
            var downloadLink = root.TryGetProperty("DownloadLink", out var linkNode) ? linkNode.GetString() : null;

            return new InviteEmailConfig
            {
                Host = host?.Trim() ?? string.Empty,
                Port = port > 0 ? port : 587,
                UseSsl = useSsl,
                Username = username?.Trim() ?? string.Empty,
                Password = password ?? string.Empty,
                FromEmail = fromEmail?.Trim() ?? string.Empty,
                FromName = string.IsNullOrWhiteSpace(fromName) ? "PartFinder" : fromName.Trim(),
                DownloadLink = string.IsNullOrWhiteSpace(downloadLink) ? "https://shipspan.com" : downloadLink.Trim(),
            };
        }
        catch
        {
            return new InviteEmailConfig();
        }
    }
}
