using System.Net;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Models;
using PartFinder.Services;
using PartFinder.ViewModels;

namespace PartFinder.Tests.ViewModels;

[TestClass]
public sealed class ChatbotViewModelTests
{
    private sealed class FakeBackendAiClient : IBackendAiClient
    {
        public Func<AiChatRequestDto, CancellationToken, Task<BackendAiChatResult>> Handler { get; set; }
            = (_, _) => Task.FromResult(new BackendAiChatResult(true, HttpStatusCode.OK, null, new AiChatResponseDto
            {
                ConversationId = "conv-1",
                ReplyText = "Hello from backend",
            }));

        public List<AiChatRequestDto> SentRequests { get; } = [];

        public Task<BackendAiChatResult> PostAiChatAsync(AiChatRequestDto request, CancellationToken ct = default)
        {
            SentRequests.Add(request);
            return Handler(request, ct);
        }
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public AppPage? CurrentPage { get; set; } = AppPage.Dashboard;
        public bool IsFrameReady => true;
        public bool CanGoBack => false;
        public List<(AppPage Page, object? Parameter)> Navigations { get; } = [];

        public void Initialize(Frame frame) { }
        public Task WaitForFrameReadyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool Navigate(AppPage page, object? parameter = null)
        {
            CurrentPage = page;
            Navigations.Add((page, parameter));
            return true;
        }
        public bool ResetTo(AppPage page) => Navigate(page);
        public void Clear() => CurrentPage = null;
        public void GoBack() { }
    }

    private static (BackendChatbotResponder Responder, ChatbotStateService State, FakeBackendAiClient Api, FakeNavigationService Nav)
        CreateBackendResponder(bool withToken = true)
    {
        var api = new FakeBackendAiClient();
        var state = new ChatbotStateService();
        var nav = new FakeNavigationService();
        var browse = new CatalogBrowseSessionStore();
        browse.MasterDataGrid.TemplateId = "cat-42";
        var session = new AdminSessionStore();
        var responder = new BackendChatbotResponder(
            api,
            state,
            nav,
            browse,
            session,
            new LocalMockChatbotResponder(),
            hasAuthenticatedSession: () => withToken);

        return (responder, state, api, nav);
    }

    [TestMethod]
    public void ToggleOpen_PersistsAcrossStateReads()
    {
        var state = new ChatbotStateService();
        var vm = new ChatbotViewModel(state, new LocalMockChatbotResponder());

        Assert.IsFalse(vm.IsOpen);
        vm.ToggleOpenCommand.Execute(null);
        Assert.IsTrue(vm.IsOpen);
        Assert.IsTrue(state.IsOpen);
        Assert.IsTrue(vm.Messages.Count >= 1);

        vm.CloseCommand.Execute(null);
        Assert.IsFalse(vm.IsOpen);
        Assert.IsFalse(state.IsOpen);
    }

    [TestMethod]
    public async Task SendMessage_AppendsUserMessageAndMockReply()
    {
        var state = new ChatbotStateService();
        var vm = new ChatbotViewModel(state, new LocalMockChatbotResponder());
        var initialCount = vm.Messages.Count;

        vm.InputText = "How do I import Master Data?";
        Assert.IsTrue(vm.CanSend);

        await vm.SendMessageCommand.ExecuteAsync(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(initialCount + 2, vm.Messages.Count);
        Assert.AreEqual("user", vm.Messages[^2].Role);
        Assert.AreEqual("assistant", vm.Messages[^1].Role);
        Assert.IsFalse(string.IsNullOrWhiteSpace(vm.Messages[^1].Text));
    }

    [TestMethod]
    public void SavePosition_ClampsRatiosBetweenZeroAndOne()
    {
        var state = new ChatbotStateService();
        state.SavePosition(1.5, -0.25);

        Assert.AreEqual(1.0, state.ButtonXRatio);
        Assert.AreEqual(0.0, state.ButtonYRatio);
    }

    [TestMethod]
    public async Task BackendChatbotResponder_PreservesConversationIdAcrossTurns()
    {
        var (responder, state, api, _) = CreateBackendResponder();
        api.Handler = (req, _) => Task.FromResult(new BackendAiChatResult(
            true,
            HttpStatusCode.OK,
            null,
            new AiChatResponseDto
            {
                ConversationId = req.ConversationId ?? "conv-new-123",
                ReplyText = $"Echo: {req.Message}",
            }));

        var first = await responder.GenerateReplyWithActionsAsync("First question", []);
        Assert.AreEqual("Echo: First question", first.ReplyText);
        Assert.IsNull(api.SentRequests[0].ConversationId);
        Assert.AreEqual("conv-new-123", state.ConversationId);

        var second = await responder.GenerateReplyWithActionsAsync("Follow-up", []);
        Assert.AreEqual("Echo: Follow-up", second.ReplyText);
        Assert.AreEqual("conv-new-123", api.SentRequests[1].ConversationId);
        Assert.AreEqual("Dashboard", api.SentRequests[1].AppContext?.CurrentPage);
        Assert.AreEqual("cat-42", api.SentRequests[1].AppContext?.SelectedCatalogId);
    }

    [TestMethod]
    public void AiChatRequestDto_SerializesCamelCaseAndOmitsNulls()
    {
        var dto = new AiChatRequestDto
        {
            ConversationId = null,
            Message = "Check stock",
            AppContext = new AiAppContextDto { CurrentPage = "Inventory" },
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.IsTrue(json.Contains("\"message\":\"Check stock\""));
        Assert.IsTrue(json.Contains("\"currentPage\":\"Inventory\""));
        Assert.IsFalse(json.Contains("conversationId"));
        Assert.IsFalse(json.Contains("selectedTemplateId"));
    }

    [TestMethod]
    public async Task BackendChatbotResponder_HandlesUnauthorizedAndBackendErrorsGracefully()
    {
        var (responder, _, api, _) = CreateBackendResponder();

        api.Handler = (_, _) => Task.FromResult(
            new BackendAiChatResult(false, HttpStatusCode.Unauthorized, "Unauthorized", null));
        var unauthReply = await responder.GenerateReplyWithActionsAsync("hi", []);
        Assert.IsTrue(unauthReply.ReplyText.Contains("expired", StringComparison.OrdinalIgnoreCase));

        api.Handler = (_, _) => Task.FromResult(
            new BackendAiChatResult(false, HttpStatusCode.InternalServerError, "Server busy", null));
        var errReply = await responder.GenerateReplyWithActionsAsync("hi", []);
        Assert.AreEqual("Server busy", errReply.ReplyText);
    }

    [TestMethod]
    public async Task BackendChatbotResponder_PropagatesCancellation()
    {
        var (responder, _, api, _) = CreateBackendResponder();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        api.Handler = (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new BackendAiChatResult(true, HttpStatusCode.OK, null, null));
        };

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            responder.GenerateReplyWithActionsAsync("cancel me", [], cts.Token));
    }

    [TestMethod]
    public async Task ChatbotViewModel_ExecutesAllowlistedNavigateAndIgnoresInvalidActions()
    {
        var (responder, state, api, nav) = CreateBackendResponder();
        api.Handler = (_, _) => Task.FromResult(new BackendAiChatResult(
            true,
            HttpStatusCode.OK,
            null,
            new AiChatResponseDto
            {
                ConversationId = "conv-nav",
                ReplyText = "Opening Master Data.",
                UiActions =
                [
                    new AiUiActionDto { Type = "exec", Page = "MasterData" },
                    new AiUiActionDto { Type = "navigate", Page = "UnknownEvilPage" },
                    new AiUiActionDto { Type = "navigate", Page = "MasterData", TemplateId = "tpl-99" },
                ],
            }));

        var vm = new ChatbotViewModel(state, responder, nav);
        vm.InputText = "Open Master Data";
        await vm.SendMessageCommand.ExecuteAsync(null);

        Assert.AreEqual(1, nav.Navigations.Count);
        Assert.AreEqual(AppPage.MasterData, nav.Navigations[0].Page);
        var navArgs = nav.Navigations[0].Parameter as MasterDataNavigationArgs;
        Assert.IsNotNull(navArgs);
        Assert.AreEqual("tpl-99", navArgs.TemplateId);

        vm.ClearChatCommand.Execute(null);
        Assert.IsNull(state.ConversationId);
    }
}
