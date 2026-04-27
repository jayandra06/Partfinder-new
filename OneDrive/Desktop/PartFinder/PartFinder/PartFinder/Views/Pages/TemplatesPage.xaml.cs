using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PartFinder.Helpers;
using PartFinder.Models;
using PartFinder.Services;
using PartFinder.ViewModels;
using System.ComponentModel;
using WinRT.Interop;
using Windows.Storage.Pickers;

namespace PartFinder.Views.Pages;

public sealed partial class TemplatesPage : Page
{
    private readonly BackendApiClient _apiClient = App.Services.GetRequiredService<BackendApiClient>();
    private TemplatesViewModel? _boundVm;

    public TemplatesPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<TemplatesViewModel>();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ((TemplatesViewModel)DataContext).LoadAsync();
        BuildTemplateChipRow();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_boundVm is not null)
        {
            _boundVm.PropertyChanged -= OnTemplatesVmPropertyChanged;
            _boundVm = null;
        }

        if (DataContext is TemplatesViewModel newVm)
        {
            _boundVm = newVm;
            newVm.PropertyChanged += OnTemplatesVmPropertyChanged;
        }
    }

    private void OnTemplatesVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TemplatesViewModel.SelectedTemplate) ||
            e.PropertyName == nameof(TemplatesViewModel.ShowTemplatePreviewPanel))
        {
            BuildTemplateChipRow();
        }
    }

    private void OnRemoveColumnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ColumnLabelDraft draft })
        {
            return;
        }

        if (DataContext is TemplatesViewModel vm)
        {
            vm.RemoveColumnCommand.Execute(draft);
        }
    }

    private void OnInsertDraftColumnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ColumnLabelDraft draft })
        {
            return;
        }

        if (DataContext is TemplatesViewModel vm)
        {
            vm.InsertColumnAfterCommand.Execute(draft);
        }
    }

    private void OnAffordancePointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            AffordanceAnimationHelper.Fade(element, show: true, shownOpacity: 1, hiddenOpacity: 0.35);
        }
    }

    private void OnAffordancePointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            AffordanceAnimationHelper.Fade(element, show: false, shownOpacity: 1, hiddenOpacity: 0.35);
        }
    }

    private async void OnAddContextActionClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TemplatesViewModel vm
            || vm.SelectedTemplate is null
            || XamlRoot is null)
        {
            return;
        }

        var sourceTemplate = vm.SelectedTemplate;
        var rulesList = new List<RuleRowUi>();

        var sourceColCb = new ComboBox
        {
            DisplayMemberPath = "Label",
            SelectedValuePath = "Key",
            ItemsSource = sourceTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var menuLabelTb = new TextBox
        {
            PlaceholderText = "e.g. View suppliers",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var targetTemplateCb = new ComboBox
        {
            DisplayMemberPath = "Name",
            SelectedValuePath = "Id",
            ItemsSource = vm.Templates.ToList(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        PartTemplateDefinition? currentTarget = null;

        var rulesPanel = new StackPanel { Spacing = 8 };

        void RebindTargetFieldCombos()
        {
            var fields = currentTarget?.Fields.OrderBy(f => f.DisplayOrder).ToList()
                         ?? new List<TemplateFieldDefinition>();
            foreach (var row in rulesList)
            {
                row.TargetField.ItemsSource = fields;
                row.TargetField.SelectedItem = null;
            }
        }

        targetTemplateCb.SelectionChanged += (_, _) =>
        {
            currentTarget = targetTemplateCb.SelectedItem as PartTemplateDefinition;
            RebindTargetFieldCombos();
        };

        void AddRuleRow()
        {
            var targetField = new ComboBox
            {
                DisplayMemberPath = "Label",
                SelectedValuePath = "Key",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var sourceField = new ComboBox
            {
                DisplayMemberPath = "Label",
                SelectedValuePath = "Key",
                ItemsSource = sourceTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList(),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var literalTb = new TextBox
            {
                PlaceholderText = "Optional fixed value (overrides source column)",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var grid = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 6,
                Margin = new Thickness(0, 4, 0, 0),
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());

            var h1 = new TextBlock { Text = "Target field (other template)" };
            var h2 = new TextBlock { Text = "Source field (this row)" };
            var h3 = new TextBlock { Text = "Or literal" };
            Grid.SetColumn(h1, 0);
            Grid.SetRow(h1, 0);
            Grid.SetColumn(h2, 1);
            Grid.SetRow(h2, 0);
            Grid.SetColumn(h3, 2);
            Grid.SetRow(h3, 0);
            Grid.SetColumn(targetField, 0);
            Grid.SetRow(targetField, 1);
            Grid.SetColumn(sourceField, 1);
            Grid.SetRow(sourceField, 1);
            Grid.SetColumn(literalTb, 2);
            Grid.SetRow(literalTb, 1);
            grid.Children.Add(h1);
            grid.Children.Add(h2);
            grid.Children.Add(h3);
            grid.Children.Add(targetField);
            grid.Children.Add(sourceField);
            grid.Children.Add(literalTb);
            rulesPanel.Children.Add(grid);

            var rowUi = new RuleRowUi(targetField, sourceField, literalTb);
            rulesList.Add(rowUi);
            if (currentTarget is not null)
            {
                targetField.ItemsSource = currentTarget.Fields.OrderBy(f => f.DisplayOrder).ToList();
            }
        }

        var addRuleBtn = new Button
        {
            Content = "Add AND rule",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        addRuleBtn.Click += (_, _) => AddRuleRow();

        var displayTb = new TextBox
        {
            PlaceholderText = "Optional: comma-separated field keys to show in the popup (empty = all columns)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var root = new StackPanel { Spacing = 12 };
        root.Children.Add(new TextBlock { Text = "Column (menu appears on right-click here)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(sourceColCb);
        root.Children.Add(new TextBlock { Text = "Menu label", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(menuLabelTb);
        root.Children.Add(new TextBlock { Text = "Target template", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(targetTemplateCb);
        root.Children.Add(new TextBlock { Text = "Match rules (all must match)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(rulesPanel);
        root.Children.Add(addRuleBtn);
        root.Children.Add(new TextBlock { Text = "Display columns (optional)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(displayTb);

        var other = vm.Templates.FirstOrDefault(t => t.Id != sourceTemplate.Id);
        if (other is not null)
        {
            targetTemplateCb.SelectedItem = other;
        }

        AddRuleRow();

        var dlg = new ContentDialog
        {
            Title = "New cell action",
            Content = new ScrollViewer
            {
                MaxHeight = 520,
                Content = root,
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (sourceColCb.SelectedValue is not string sourceKey || string.IsNullOrWhiteSpace(sourceKey))
        {
            await ShowSimpleDialogAsync("Choose a column for this action.", XamlRoot).ConfigureAwait(true);
            return;
        }

        var menuLabel = menuLabelTb.Text.Trim();
        if (menuLabel.Length == 0)
        {
            await ShowSimpleDialogAsync("Enter a menu label.", XamlRoot).ConfigureAwait(true);
            return;
        }

        if (targetTemplateCb.SelectedValue is not string targetId || string.IsNullOrWhiteSpace(targetId))
        {
            await ShowSimpleDialogAsync("Choose a target template.", XamlRoot).ConfigureAwait(true);
            return;
        }

        var rules = new List<ContextActionMatchRule>();
        foreach (var row in rulesList)
        {
            if (row.TargetField.SelectedItem is not TemplateFieldDefinition tf)
            {
                continue;
            }

            var lit = row.Literal.Text.Trim();
            if (lit.Length > 0)
            {
                rules.Add(
                    new ContextActionMatchRule
                    {
                        TargetFieldKey = tf.Key,
                        LiteralValue = lit,
                        SourceFieldKey = null,
                    });
                continue;
            }

            if (row.SourceField.SelectedItem is not TemplateFieldDefinition sf)
            {
                continue;
            }

            rules.Add(
                new ContextActionMatchRule
                {
                    TargetFieldKey = tf.Key,
                    SourceFieldKey = sf.Key,
                    LiteralValue = null,
                });
        }

        if (rules.Count == 0)
        {
            await ShowSimpleDialogAsync("Add at least one complete rule (target field + source field, or target field + literal).", XamlRoot)
                .ConfigureAwait(true);
            return;
        }

        IReadOnlyList<string>? displayKeys = null;
        var rawDisplay = displayTb.Text.Trim();
        if (rawDisplay.Length > 0)
        {
            displayKeys = rawDisplay
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
                .ToList();
        }

        var action = new TemplateContextAction
        {
            Id = Guid.NewGuid().ToString("N"),
            SourceTemplateId = sourceTemplate.Id,
            SourceFieldKey = sourceKey,
            MenuLabel = menuLabel,
            TargetTemplateId = targetId,
            MatchRules = rules,
            DisplayFieldKeys = displayKeys,
        };

        try
        {
            await vm.SaveNewContextActionAsync(action).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await ShowSimpleDialogAsync(ex.Message, XamlRoot).ConfigureAwait(true);
        }
    }

    private static async Task ShowSimpleDialogAsync(string message, XamlRoot xamlRoot)
    {
        var err = new ContentDialog
        {
            Title = "Cell action",
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
            CloseButtonText = "OK",
            XamlRoot = xamlRoot,
        };
        _ = await err.ShowAsync();
    }

    private sealed class RuleRowUi
    {
        public RuleRowUi(ComboBox targetField, ComboBox sourceField, TextBox literalTb)
        {
            TargetField = targetField;
            SourceField = sourceField;
            Literal = literalTb;
        }

        public ComboBox TargetField { get; }
        public ComboBox SourceField { get; }
        public TextBox Literal { get; }
    }

    private void BuildTemplateChipRow()
    {
        TemplateChipRowPanel.Children.Clear();
        if (DataContext is not TemplatesViewModel vm || vm.SelectedTemplate is null || !vm.ShowTemplatePreviewPanel)
        {
            return;
        }

        var fields = vm.SelectedTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
        AddInsertButton(vm, 0);
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var card = new Border
            {
                MinWidth = 140,
                Height = 40,
                Margin = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(12, 0, 8, 0),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["AppBorderBrush"],
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = field.Label, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(
                new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 241, 251)),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(6, 2, 6, 2),
                    Child = new TextBlock
                    {
                        Text = field.Type.ToString(),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 24, 95, 165)),
                    },
                });
            row.Children.Add(
                new Button
                {
                    Content = "×",
                    Padding = new Thickness(6, 0, 6, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Tag = i,
                });
            ((Button)row.Children[^1]).Click += OnTemplateChipRemoveClick;
            card.Child = row;
            TemplateChipRowPanel.Children.Add(card);
            AddInsertButton(vm, i + 1);
        }
    }

    private void AddInsertButton(TemplatesViewModel vm, int insertIndex)
    {
        var btn = new Button
        {
            Content = "+",
            Width = 28,
            Height = 28,
            Margin = new Thickness(8, 6, 8, 6),
            Tag = insertIndex,
            Opacity = 0.35,
        };
        btn.PointerEntered += (_, _) => AffordanceAnimationHelper.Fade(btn, show: true, shownOpacity: 1, hiddenOpacity: 0.35);
        btn.PointerExited += (_, _) => AffordanceAnimationHelper.Fade(btn, show: false, shownOpacity: 1, hiddenOpacity: 0.35);
        btn.Click += async (_, _) =>
        {
            if (XamlRoot is null)
            {
                return;
            }

            var input = new TextBox { PlaceholderText = "Column name" };
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Add column",
                Content = input,
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary || btn.Tag is not int idx)
            {
                return;
            }

            await vm.InsertColumnIntoSelectedTemplateAsync(idx, input.Text).ConfigureAwait(true);
            BuildTemplateChipRow();
        };
        TemplateChipRowPanel.Children.Add(btn);
    }

    private async void OnTemplateChipRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index } || DataContext is not TemplatesViewModel vm)
        {
            return;
        }

        await vm.RemoveColumnFromSelectedTemplateAsync(index).ConfigureAwait(true);
        BuildTemplateChipRow();
    }

    private async void OnImportCsvClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TemplatesViewModel vm || vm.SelectedTemplate is null || XamlRoot is null || App.MainAppWindow is null)
        {
            return;
        }
        SetImportStatus(null);

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".csv");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var headers = await ReadCsvHeadersAsync(file.Path).ConfigureAwait(true);
        if (headers.Count == 0)
        {
            await ShowSimpleDialogAsync("Could not detect CSV headers.", XamlRoot).ConfigureAwait(true);
            return;
        }

        var headerMap = await ShowHeaderMappingDialogAsync(vm.SelectedTemplate, headers).ConfigureAwait(true);
        if (headerMap is null)
        {
            return;
        }

        var (ok, error, jobId) = await _apiClient
            .StartTemplateImportAsync(vm.SelectedTemplate.Id, file.Path, headerMap)
            .ConfigureAwait(true);
        if (!ok || string.IsNullOrWhiteSpace(jobId))
        {
            SetImportStatus("Import failed to start.", error ?? "Unknown startup error.", isActive: false);
            await ShowSimpleDialogAsync(error ?? "Failed to start import.", XamlRoot).ConfigureAwait(true);
            return;
        }

        SetImportStatus("Import started...", $"Job: {jobId}", isActive: true);
        var status = await PollImportStatusAsync(vm.SelectedTemplate.Id).ConfigureAwait(true);
        if (status is null)
        {
            SetImportStatus("Import status unavailable.", "The server did not return a final status in time.", isActive: false);
            await ShowSimpleDialogAsync("Import started but status could not be read.", XamlRoot).ConfigureAwait(true);
            return;
        }

        var summary = $"Status: {status.Status}\nProcessed: {status.ProcessedRows}/{status.TotalRows}\nFailed: {status.FailedRows}";
        if (status.Errors.Count > 0)
        {
            summary += $"\nErrors:\n- {string.Join("\n- ", status.Errors)}";
        }

        var finalHeadline = string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase)
            ? "Import completed."
            : "Import finished with issues.";
        SetImportStatus(finalHeadline, $"Processed {status.ProcessedRows}/{status.TotalRows}, failed {status.FailedRows}.", isActive: false);
        await ShowSimpleDialogAsync(summary, XamlRoot).ConfigureAwait(true);
    }

    private static async Task<List<string>> ReadCsvHeadersAsync(string path)
    {
        string? firstLine;
        using (var reader = new StreamReader(path))
        {
            firstLine = await reader.ReadLineAsync().ConfigureAwait(true);
        }

        firstLine ??= string.Empty;
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return [];
        }

        return firstLine
            .Split(',', StringSplitOptions.TrimEntries)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();
    }

    private async Task<Dictionary<string, string>?> ShowHeaderMappingDialogAsync(
        PartTemplateDefinition selectedTemplate,
        IReadOnlyList<string> headers)
    {
        if (XamlRoot is null)
        {
            return null;
        }

        var templateFields = selectedTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
        var rows = new List<(string Header, ComboBox Combo)>();
        var panel = new StackPanel { Spacing = 8 };
        foreach (var header in headers)
        {
            var combo = new ComboBox
            {
                Width = 260,
                ItemsSource = templateFields,
                DisplayMemberPath = "Label",
                SelectedValuePath = "Label",
                PlaceholderText = "Skip this header",
            };

            var auto = templateFields.FirstOrDefault(
                f => string.Equals(f.Label, header, StringComparison.OrdinalIgnoreCase));
            if (auto is not null)
            {
                combo.SelectedItem = auto;
            }

            rows.Add((header, combo));
            panel.Children.Add(
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Width = 240, Text = header, VerticalAlignment = VerticalAlignment.Center },
                        combo,
                    },
                });
        }

        var dialog = new ContentDialog
        {
            Title = "Map CSV headers",
            Content = new ScrollViewer { MaxHeight = 420, Content = panel },
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.Combo.SelectedItem is TemplateFieldDefinition field)
            {
                map[row.Header] = field.Label;
            }
        }

        return map;
    }

    private async Task<ImportStatusDto?> PollImportStatusAsync(string templateId)
    {
        for (var i = 0; i < 60; i++)
        {
            await Task.Delay(1000).ConfigureAwait(true);
            var (ok, _, status) = await _apiClient.GetTemplateImportStatusAsync(templateId).ConfigureAwait(true);
            if (!ok || status is null)
            {
                continue;
            }

            SetImportStatus(
                $"Import {status.Status}...",
                $"Processed {status.ProcessedRows}/{status.TotalRows}, failed {status.FailedRows}.",
                isActive: true);

            if (string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                return status;
            }
        }

        return null;
    }

    private void SetImportStatus(string? headline, string? detail = null, bool isActive = false)
    {
        if (ImportStatusPanel is null || ImportProgressRing is null || ImportStatusText is null || ImportStatusDetailText is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(headline))
        {
            ImportStatusPanel.Visibility = Visibility.Collapsed;
            ImportProgressRing.IsActive = false;
            ImportStatusText.Text = string.Empty;
            ImportStatusDetailText.Text = string.Empty;
            return;
        }

        ImportStatusPanel.Visibility = Visibility.Visible;
        ImportProgressRing.IsActive = isActive;
        ImportStatusText.Text = headline;
        ImportStatusDetailText.Text = detail ?? string.Empty;
    }
}
