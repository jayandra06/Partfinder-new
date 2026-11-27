namespace PartFinder.Services;

public sealed class ChatMessageItem
{
    public required string Role { get; init; } // "user" or "assistant"
    public required string Text { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => !IsUser;
    public string TimestampDisplay => TimestampUtc.ToLocalTime().ToString("HH:mm");
}

public sealed class AiExportColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class AiStructuredExportPayloadDto
{
    public string ExportType { get; set; } = string.Empty;
    public string Format { get; set; } = "json_table";
    public string FileNameSuggestion { get; set; } = "partfinder-export.xlsx";
    public List<AiExportColumnDto> Columns { get; set; } = [];
    public List<Dictionary<string, string>> Rows { get; set; } = [];
    public string? CsvContent { get; set; }
    public int RowCount { get; set; }
    public bool Truncated { get; set; }
}

public sealed class AiUiActionDto
{
    public string Type { get; set; } = string.Empty;
    public string Page { get; set; } = string.Empty;
    public string? TemplateId { get; set; }
    public AiStructuredExportPayloadDto? ExportPayload { get; set; }
}

public sealed record ChatbotReply(
    string ReplyText,
    IReadOnlyList<AiUiActionDto>? UiActions = null);

/// <summary>
/// Abstraction for chatbot responses. Implemented by <see cref="BackendChatbotResponder"/>
/// for authenticated backend communication and <see cref="LocalMockChatbotResponder"/> for local preview/fallback.
/// </summary>
public interface IChatbotResponder
{
    Task<string> GenerateReplyAsync(
        string userMessage,
        IReadOnlyList<ChatMessageItem> history,
        CancellationToken cancellationToken = default);

    async Task<ChatbotReply> GenerateReplyWithActionsAsync(
        string userMessage,
        IReadOnlyList<ChatMessageItem> history,
        CancellationToken cancellationToken = default)
    {
        var text = await GenerateReplyAsync(userMessage, history, cancellationToken).ConfigureAwait(false);
        return new ChatbotReply(text);
    }
}

public sealed class LocalMockChatbotResponder : IChatbotResponder
{
    public async Task<string> GenerateReplyAsync(
        string userMessage,
        IReadOnlyList<ChatMessageItem> history,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(250, cancellationToken).ConfigureAwait(false);

        var text = (userMessage ?? string.Empty).Trim();
        var lower = text.ToLowerInvariant();

        if (lower.Contains("help") || lower.Contains("what can you do"))
        {
            return "I am the PartFinder AI Assistant (local preview). Once connected to your in-house LLM, I can help search parts, summarize inventory, and answer questions across templates.";
        }

        if (lower.Contains("master data") || lower.Contains("import") || lower.Contains("excel") || lower.Contains("csv"))
        {
            return "To import Excel or CSV files, open Master Data, select a catalog, and click Import data. Column headers and delimiters are detected automatically.";
        }

        if (lower.Contains("analytics") || lower.Contains("template"))
        {
            return "In Analytics, you can browse template rows or click Edit data to update columns and values, and use column search bars to filter results.";
        }

        if (lower.Contains("2fa") || lower.Contains("security") || lower.Contains("password"))
        {
            return "You can manage Two-Factor Authentication (2FA) and password settings under Settings → Security & 2FA. Your 2FA status is linked to your email account.";
        }

        return $"[Local Preview] Received: \"{text}\". Connect your in-house LLM implementation of IChatbotResponder to enable live AI responses.";
    }
}
