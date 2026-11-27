using System.Net;
using PartFinder.Models;

namespace PartFinder.Services;

/// <summary>
/// Connects the global floating chatbot to the PartFinder NestJS backend (<c>POST /api/ai/chat</c>),
/// preserving <see cref="ChatbotStateService.ConversationId"/> across turns and attaching safe UI metadata.
/// </summary>
public sealed class BackendChatbotResponder : IChatbotResponder
{
    private readonly IBackendAiClient _api;
    private readonly ChatbotStateService _state;
    private readonly INavigationService _navigation;
    private readonly CatalogBrowseSessionStore _browseSession;
    private readonly AdminSessionStore _session;
    private readonly LocalMockChatbotResponder _fallback;
    private readonly Func<bool>? _hasAuthenticatedSession;

    public BackendChatbotResponder(
        IBackendAiClient api,
        ChatbotStateService state,
        INavigationService navigation,
        CatalogBrowseSessionStore browseSession,
        AdminSessionStore session,
        LocalMockChatbotResponder fallback,
        Func<bool>? hasAuthenticatedSession = null)
    {
        _api = api;
        _state = state;
        _navigation = navigation;
        _browseSession = browseSession;
        _session = session;
        _fallback = fallback;
        _hasAuthenticatedSession = hasAuthenticatedSession;
    }

    public async Task<string> GenerateReplyAsync(
        string userMessage,
        IReadOnlyList<ChatMessageItem> history,
        CancellationToken cancellationToken = default)
    {
        var result = await GenerateReplyWithActionsAsync(userMessage, history, cancellationToken)
            .ConfigureAwait(false);
        return result.ReplyText;
    }

    public async Task<ChatbotReply> GenerateReplyWithActionsAsync(
        string userMessage,
        IReadOnlyList<ChatMessageItem> history,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isAuthenticated = _hasAuthenticatedSession is not null
            ? _hasAuthenticatedSession()
            : CheckSessionAuthenticated();

        if (!isAuthenticated)
        {
            var localText = await _fallback
                .GenerateReplyAsync(userMessage, history, cancellationToken)
                .ConfigureAwait(false);
            return new ChatbotReply(localText);
        }

        var request = new AiChatRequestDto
        {
            ConversationId = string.IsNullOrWhiteSpace(_state.ConversationId)
                ? null
                : _state.ConversationId,
            Message = (userMessage ?? string.Empty).Trim(),
            AppContext = BuildAppContext(),
        };

        var response = await _api.PostAiChatAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.Ok)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new ChatbotReply("Your session has expired or is not authorized. Please sign in again to use the AI assistant.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new ChatbotReply("You do not have permission to use the AI assistant.");
            }

            return new ChatbotReply(
                string.IsNullOrWhiteSpace(response.Error)
                    ? "Unable to reach the PartFinder AI service right now. Please try again."
                    : response.Error);
        }

        var data = response.Data;
        if (data is null)
        {
            return new ChatbotReply("The AI service returned an empty response.");
        }

        if (!string.IsNullOrWhiteSpace(data.ConversationId))
        {
            _state.ConversationId = data.ConversationId.Trim();
        }

        var replyText = string.IsNullOrWhiteSpace(data.ReplyText)
            ? "No response text was returned by the AI service."
            : data.ReplyText.Trim();

        return new ChatbotReply(replyText, data.UiActions);
    }

    private bool CheckSessionAuthenticated()
    {
        _session.Load();
        return _session.HasSession && !string.IsNullOrWhiteSpace(_session.AccessToken);
    }

    private AiAppContextDto BuildAppContext()
    {
        var currentPage = _navigation.CurrentPage;
        var pageName = currentPage?.ToString();

        string? selectedTemplateId = null;
        string? selectedCatalogId = null;

        if (currentPage is AppPage.MasterData or AppPage.TemplateDataEdit)
        {
            selectedCatalogId = _browseSession.MasterDataGrid.TemplateId;
        }
        else if (currentPage is AppPage.LinkedTemplates)
        {
            selectedTemplateId = _browseSession.LinkedTemplatesBrowseTemplateId
                ?? _browseSession.LinkedTemplatesGrid.TemplateId;
        }
        else
        {
            selectedCatalogId = _browseSession.MasterDataGrid.TemplateId;
            selectedTemplateId = _browseSession.LinkedTemplatesBrowseTemplateId;
        }

        return new AiAppContextDto
        {
            CurrentPage = pageName,
            SelectedTemplateId = string.IsNullOrWhiteSpace(selectedTemplateId) ? null : selectedTemplateId,
            SelectedCatalogId = string.IsNullOrWhiteSpace(selectedCatalogId) ? null : selectedCatalogId,
        };
    }
}
