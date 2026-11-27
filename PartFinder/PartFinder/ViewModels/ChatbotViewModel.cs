using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public sealed partial class ChatbotViewModel : ViewModelBase
{
    private readonly ChatbotStateService _state;
    private readonly IChatbotResponder _responder;
    private readonly INavigationService? _navigation;
    private readonly IAiExportSaveService? _exportSaveService;
    private CancellationTokenSource? _sendCts;

    [ObservableProperty]
    private string inputText = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public ChatbotViewModel(
        ChatbotStateService state,
        IChatbotResponder responder,
        INavigationService? navigation = null,
        IAiExportSaveService? exportSaveService = null)
    {
        _state = state;
        _responder = responder;
        _navigation = navigation;
        _exportSaveService = exportSaveService;
        _state.EnsureLoaded();
        EnsureWelcomeMessage();
    }

    public ObservableCollection<ChatMessageItem> Messages => _state.Messages;

    public bool IsOpen
    {
        get => _state.IsOpen;
        set
        {
            if (_state.IsOpen == value)
            {
                return;
            }

            _state.IsOpen = value;
            OnPropertyChanged();
            if (value)
            {
                EnsureWelcomeMessage();
            }
        }
    }

    public bool CanSend => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    partial void OnInputTextChanged(string value) => OnPropertyChanged(nameof(CanSend));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSend));

    [RelayCommand]
    private void ToggleOpen() => IsOpen = !IsOpen;

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void ClearChat()
    {
        _sendCts?.Cancel();
        _state.ResetConversation();
        EnsureWelcomeMessage();
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendMessageAsync()
    {
        var prompt = InputText.Trim();
        if (string.IsNullOrEmpty(prompt) || IsBusy)
        {
            return;
        }

        InputText = string.Empty;
        Messages.Add(new ChatMessageItem { Role = "user", Text = prompt });

        _sendCts?.Cancel();
        _sendCts?.Dispose();
        _sendCts = new CancellationTokenSource();
        var ct = _sendCts.Token;

        IsBusy = true;
        try
        {
            var history = Messages.ToList();
            var reply = await _responder
                .GenerateReplyWithActionsAsync(prompt, history, ct)
                .ConfigureAwait(true);

            Messages.Add(new ChatMessageItem
            {
                Role = "assistant",
                Text = string.IsNullOrWhiteSpace(reply.ReplyText)
                    ? "I could not generate a response. Please try again."
                    : reply.ReplyText.Trim(),
            });

            ExecuteAllowlistedUiActions(reply.UiActions);
            await ExecuteExportActionsAsync(reply.UiActions, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by user clearing chat or closing/replacing request.
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageItem
            {
                Role = "assistant",
                Text = $"Unable to respond right now: {ex.Message}",
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ExecuteAllowlistedUiActions(IReadOnlyList<AiUiActionDto>? actions)
    {
        if (_navigation is null || actions is null || actions.Count == 0)
        {
            return;
        }

        foreach (var action in actions)
        {
            if (action is null
                || !string.Equals(action.Type, "navigate", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(action.Page))
            {
                continue;
            }

            if (!Enum.TryParse<AppPage>(action.Page.Trim(), ignoreCase: false, out var page)
                || !Enum.IsDefined(typeof(AppPage), page))
            {
                continue;
            }

            object? parameter = null;
            if (!string.IsNullOrWhiteSpace(action.TemplateId))
            {
                var templateId = action.TemplateId.Trim();
                if (page == AppPage.TemplateData)
                {
                    parameter = new MasterDataNavigationArgs
                    {
                        Mode = MasterDataSurfaceMode.ReadOnlyCatalog,
                        TemplateId = templateId,
                    };
                }
                else if (page == AppPage.TemplateDataEdit)
                {
                    parameter = new MasterDataNavigationArgs
                    {
                        Mode = MasterDataSurfaceMode.TemplateEdit,
                        TemplateId = templateId,
                    };
                }
                else if (page == AppPage.MasterData)
                {
                    parameter = new MasterDataNavigationArgs
                    {
                        Mode = MasterDataSurfaceMode.ReadOnlyCatalog,
                        TemplateId = templateId,
                    };
                }
            }

            _navigation.Navigate(page, parameter);
            break; // Execute at most one navigation action per turn.
        }
    }

    public async Task ExecuteExportActionsAsync(
        IReadOnlyList<AiUiActionDto>? actions,
        CancellationToken cancellationToken = default)
    {
        if (_exportSaveService is null || actions is null || actions.Count == 0)
        {
            return;
        }

        foreach (var action in actions)
        {
            if (action is null
                || !string.Equals(action.Type, "export", StringComparison.OrdinalIgnoreCase)
                || action.ExportPayload is null)
            {
                continue;
            }

            var result = await _exportSaveService
                .SaveExportAsync(action.ExportPayload, cancellationToken: cancellationToken)
                .ConfigureAwait(true);

            var statusMessage = result.Status switch
            {
                AiExportSaveStatus.Saved =>
                    $"Exported {result.RowCount} row(s) to {result.FilePath}.",
                AiExportSaveStatus.Cancelled =>
                    "Export cancelled. No file was saved.",
                _ =>
                    $"Export failed: {result.ErrorMessage ?? "Unknown error."}",
            };

            Messages.Add(new ChatMessageItem
            {
                Role = "assistant",
                Text = statusMessage,
            });
            break; // Execute at most one export action per turn.
        }
    }

    private void EnsureWelcomeMessage()
    {
        if (Messages.Count > 0)
        {
            return;
        }

        Messages.Add(new ChatMessageItem
        {
            Role = "assistant",
            Text = "Hi! I'm the PartFinder AI Assistant (local preview). Ask me about Master Data, Analytics, or 2FA settings.",
        });
    }
}
