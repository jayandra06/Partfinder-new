using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.Views.Components;

public sealed partial class TemplateComposerPanel : UserControl
{
    private const string SourcePrefix = MasterDataSourceFieldEncoding.SourcePrefix;
    private readonly ITemplateSchemaService _templates;
    private readonly IMasterDataRecordsService _records;

    private PartTemplateDefinition? _template;
    private readonly List<ColumnSlot> _columns = [];
    private readonly List<RowSlot> _rows = [];
    private readonly List<string> _removedRowIds = [];
    private readonly List<SourceCatalog> _sources = [];
    private string? _selectedColumnKey;
    private bool _busy;
    private bool _isCreating;
    private bool _viewOnly;
    private bool _applyingRender;
    private double _laidOutViewport;
    private string _masterDataSearchQuery = string.Empty;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _masterDataSearchTimer;
    private bool _masterDataSearchRenderQueued;
    private const int MinEditingColumns = 12;
    private const int MinEditingRows = 40;
    private const int TrailingRowBuffer = 25;
    private const int GrowRowsBatch = 20;

    public TemplateComposerPanel()
    {
        InitializeComponent();
        Background = (Brush)Application.Current.Resources["PageBackgroundBrush"];

        _templates = App.Services.GetRequiredService<ITemplateSchemaService>();
        _records = App.Services.GetRequiredService<IMasterDataRecordsService>();
    }

    public event EventHandler? Closed;

    public event EventHandler? Saved;

    public async Task OpenAsync(string templateId, bool isCreating = false, bool viewOnly = false)
    {
        _isCreating = isCreating;
        _viewOnly = viewOnly && !isCreating;
        CancelCreationButton.Visibility = isCreating ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.Visibility = _viewOnly ? Visibility.Collapsed : Visibility.Visible;
        MasterDataPanelBorder.Visibility = _viewOnly ? Visibility.Collapsed : Visibility.Visible;
        StatusText.Text = "Loading template…";
        var templateTask = _templates.GetTemplateAsync(templateId);
        var storedTask = _records.GetStoredRowsAsync(templateId);
        var template = await templateTask.ConfigureAwait(true);
        if (template is null)
        {
            StatusText.Text = "Template was not found.";
            return;
        }

        _template = template;
        _columns.Clear();
        foreach (var field in template.Fields.OrderBy(f => f.DisplayOrder))
        {
            ParseSource(field.ValidationPattern, out var sourceTemplateId, out var sourceFieldKey);
            _columns.Add(new ColumnSlot
            {
                Key = field.Key,
                Label = field.Label,
                SourceTemplateId = sourceTemplateId,
                SourceFieldKey = sourceFieldKey,
            });
        }

        if (_columns.Count == 0)
        {
            _columns.Add(new ColumnSlot { Key = NewKey(), Label = "Column 1" });
        }

        var stored = await storedTask.ConfigureAwait(true);
        _rows.Clear();
        _removedRowIds.Clear();
        foreach (var record in stored.OrderBy(r => r.RowOrder))
        {
            _rows.Add(new RowSlot
            {
                Id = record.Id,
                Values = new Dictionary<string, string>(record.Values, StringComparer.Ordinal),
            });
        }

        if (_viewOnly)
        {
            TrimEmptyTrailingRows();
        }
        else
        {
            EnsureEditingBuffer();
        }

        if (!_viewOnly)
        {
            _sources.Clear();
            var catalogs = TemplateCatalogFilters.MasterDataCatalogs(
                await _templates.GetTemplatesAsync().ConfigureAwait(true));
            foreach (var catalog in CatalogBrowseSessionStore.OrderForCatalogSidebar(catalogs))
            {
                if (string.Equals(catalog.Id, templateId, StringComparison.Ordinal) || catalog.Fields.Count == 0)
                {
                    continue;
                }

                _sources.Add(new SourceCatalog(catalog.Id, catalog.Name, catalog.Fields.OrderBy(f => f.DisplayOrder).ToList()));
            }
        }

        _selectedColumnKey = _columns[0].Key;
        TitleText.Text = _viewOnly ? $"{template.Name} (view)" : template.Name;
        MasterDataSearchBox.Text = string.Empty;
        _masterDataSearchQuery = string.Empty;
        Render();
        StatusText.Text = isCreating
            ? "Type in the grid like a spreadsheet. Use master data columns below to import. Cancel creation removes this new template."
            : _viewOnly
                ? "View-only workspace — browse template data here. Use Edit on Analytics to change structure or values."
                : "Type in the grid like a spreadsheet. Select a column header, then pick a master data column below to import values.";
    }

    private void TrimEmptyTrailingRows()
    {
        static bool IsEmptyRow(RowSlot row) =>
            row.Values.Values.All(string.IsNullOrWhiteSpace);

        while (_rows.Count > 1 && IsEmptyRow(_rows[^1]))
        {
            _rows.RemoveAt(_rows.Count - 1);
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_busy || _template is null)
        {
            return;
        }

        TrimGridToUsedExtents();
        await SaveAsync("Template saved.").ConfigureAwait(true);
        EnsureEditingBuffer();
        ApplyRender();
    }

    private void OnMasterDataSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
        {
            return;
        }

        _masterDataSearchQuery = sender.Text ?? string.Empty;
        ScheduleMasterDataSearchRender();
    }

    private void ScheduleMasterDataSearchRender()
    {
        if (_masterDataSearchRenderQueued)
        {
            return;
        }

        _masterDataSearchRenderQueued = true;
        _masterDataSearchTimer ??= DispatcherQueue.CreateTimer();
        _masterDataSearchTimer.Interval = TimeSpan.FromMilliseconds(300);
        _masterDataSearchTimer.IsRepeating = false;
        _masterDataSearchTimer.Stop();
        _masterDataSearchTimer.Tick -= OnMasterDataSearchRenderTick;
        _masterDataSearchTimer.Tick += OnMasterDataSearchRenderTick;
        _masterDataSearchTimer.Start();
    }

    private void OnMasterDataSearchRenderTick(
        Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
        object args)
    {
        _masterDataSearchRenderQueued = false;
        RenderSourcesOnly();
        MasterDataSearchBox.Focus(FocusState.Programmatic);
    }

    private void RenderSourcesOnly()
    {
        SourcesScroll.Content = BuildSourceTables();
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => Closed?.Invoke(this, EventArgs.Empty);

    private async void OnCancelCreationClick(object sender, RoutedEventArgs e)
    {
        if (!_isCreating || _template is null || _busy || XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Cancel creation",
            Content = $"Remove \"{_template.Name}\" and everything added in this template?",
            PrimaryButtonText = "Remove template",
            CloseButtonText = "Keep working",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _busy = true;
        try
        {
            foreach (var row in _rows.ToList())
            {
                if (!string.IsNullOrWhiteSpace(row.Id))
                {
                    await _records.DeleteRowAsync(_template.Id, row.Id).ConfigureAwait(true);
                }
            }

            var relationCleanup = App.Services.GetRequiredService<WorksheetRelationCleanupService>();
            await relationCleanup.DeleteAllRelationsInvolvingTemplateAsync(_template.Id).ConfigureAwait(true);
            await _templates.DeleteTemplateAsync(_template.Id).ConfigureAwait(true);
            _isCreating = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task DeleteColumnAsync(ColumnSlot column)
    {
        if (_busy || _template is null)
        {
            return;
        }

        if (_columns.Count <= 1)
        {
            StatusText.Text = "A template needs at least one column.";
            return;
        }

        var index = _columns.IndexOf(column);
        if (index < 0)
        {
            return;
        }

        _columns.RemoveAt(index);
        foreach (var row in _rows)
        {
            row.Values.Remove(column.Key);
        }

        if (_selectedColumnKey == column.Key)
        {
            _selectedColumnKey = _columns[Math.Clamp(index - 1, 0, _columns.Count - 1)].Key;
        }

        ApplyRender();
        await SaveAsync($"Deleted column \"{column.Label}\".").ConfigureAwait(true);
    }

    private async Task ImportSourceColumnAsync(SourceCatalog catalog, TemplateFieldDefinition sourceField)
    {
        if (_busy || _selectedColumnKey is null)
        {
            StatusText.Text = "Select a column in the template table first.";
            return;
        }

        if (IsSourceUsed(catalog.Id, sourceField.Key))
        {
            StatusText.Text = $"\"{sourceField.Label}\" was already imported.";
            return;
        }

        var destination = _columns.FirstOrDefault(c => c.Key == _selectedColumnKey);
        if (destination is null)
        {
            return;
        }

        _busy = true;
        try
        {
            var sourceRows = await _records.GetRowsAsync(catalog.Id).ConfigureAwait(true);
            while (_rows.Count < sourceRows.Count)
            {
                AddEmptyRows(Math.Min(GrowRowsBatch, sourceRows.Count - _rows.Count));
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var value = string.Empty;
                if (i < sourceRows.Count)
                {
                    sourceRows[i].Values.TryGetValue(sourceField.Key, out value);
                }

                _rows[i].Values[destination.Key] = value ?? string.Empty;
            }

            destination.Label = sourceField.Label;
            destination.SourceTemplateId = catalog.Id;
            destination.SourceFieldKey = sourceField.Key;
            ApplyRender();
            StatusText.Text = $"Imported \"{sourceField.Label}\" from {catalog.Name}.";
            await SaveAsync(StatusText.Text).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task DuplicateColumnAsync(ColumnSlot source)
    {
        if (_busy)
        {
            return;
        }

        var index = _columns.IndexOf(source);
        if (index < 0)
        {
            return;
        }

        var copy = new ColumnSlot
        {
            Key = NewKey(),
            Label = $"{source.Label} copy",
        };
        _columns.Insert(index + 1, copy);
        foreach (var row in _rows)
        {
            row.Values.TryGetValue(source.Key, out var value);
            row.Values[copy.Key] = value ?? string.Empty;
        }

        _selectedColumnKey = copy.Key;
        ApplyRender();
        await SaveAsync($"Copied column \"{source.Label}\".").ConfigureAwait(true);
    }

    private async Task SaveAsync(string successMessage)
    {
        if (_template is null)
        {
            return;
        }

        _busy = true;
        SaveButton.IsEnabled = false;
        try
        {
            var fields = _columns.Select((column, index) => new TemplateFieldDefinition
            {
                Key = column.Key,
                Label = column.Label,
                Type = TemplateFieldType.Text,
                IsRequired = false,
                DisplayOrder = index,
                ValidationPattern = EncodeSource(column.SourceTemplateId, column.SourceFieldKey),
            }).ToList();

            var def = new PartTemplateDefinition
            {
                Id = _template.Id,
                Name = _template.Name,
                Version = _template.Version + 1,
                IsPublished = true,
                IsMasterDataCatalog = false,
                BaseTemplateId = _template.BaseTemplateId,
                Fields = fields,
            };
            await _templates.SaveTemplateAsync(def).ConfigureAwait(true);
            _template = def;

            var payload = new List<(string? Id, int RowOrder, IReadOnlyDictionary<string, string> Values)>(_rows.Count);
            for (var i = 0; i < _rows.Count; i++)
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var column in _columns)
                {
                    _rows[i].Values.TryGetValue(column.Key, out var value);
                    values[column.Key] = value ?? string.Empty;
                }

                _rows[i].Values = values;
                payload.Add((_rows[i].Id, i, values));
            }

            const int rowBatchSize = 100;
            var deleteIds = _removedRowIds.ToList();
            _removedRowIds.Clear();
            if (payload.Count == 0)
            {
                if (deleteIds.Count > 0)
                {
                    _ = await _records.SaveRowsAsync(
                        _template.Id,
                        Array.Empty<(string? Id, int RowOrder, IReadOnlyDictionary<string, string> Values)>(),
                        deleteIds).ConfigureAwait(true);
                }
            }
            else
            {
                var idBuffer = new string[payload.Count];
                for (var offset = 0; offset < payload.Count; offset += rowBatchSize)
                {
                    var batch = payload.Skip(offset).Take(rowBatchSize).ToList();
                    IReadOnlyList<string> deletes = offset == 0 ? deleteIds : Array.Empty<string>();
                    var ids = await _records.SaveRowsAsync(_template.Id, batch, deletes).ConfigureAwait(true);
                    for (var i = 0; i < ids.Count; i++)
                    {
                        idBuffer[offset + i] = ids[i];
                    }
                }

                for (var i = 0; i < idBuffer.Length && i < _rows.Count; i++)
                {
                    _rows[i].Id = idBuffer[i];
                }
            }

            StatusText.Text = successMessage;
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            SaveButton.IsEnabled = true;
        }
    }

    private void OnTemplateScrollSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_applyingRender || _columns.Count == 0)
        {
            return;
        }

        var width = TemplateScroll.ViewportWidth;
        if (width < 1 || Math.Abs(width - _laidOutViewport) < 8)
        {
            return;
        }

        ApplyRender();
    }

    private void OnTemplateViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (Math.Abs(HeaderScroll.HorizontalOffset - TemplateScroll.HorizontalOffset) > 1)
        {
            HeaderScroll.ChangeView(TemplateScroll.HorizontalOffset, null, null, true);
        }

        if (e.IsIntermediate || _busy)
        {
            return;
        }

        var viewport = TemplateScroll.ViewportHeight;
        var extent = TemplateScroll.ExtentHeight;
        if (viewport > 0 && extent > viewport)
        {
            var remaining = extent - (TemplateScroll.VerticalOffset + viewport);
            if (remaining < 140)
            {
                GrowRowsNearEnd();
            }
        }
    }

    private void Render() => ApplyRender();

    private void ApplyRender()
    {
        if (_applyingRender || _columns.Count == 0)
        {
            return;
        }

        _applyingRender = true;
        try
        {
            var columnWidth = ColumnWidth();
            _laidOutViewport = TemplateScroll.ViewportWidth;
            HeaderScroll.Content = BuildHeader(columnWidth);
            TemplateScroll.Content = BuildVirtualRows(columnWidth);
            SourcesScroll.Content = BuildSourceTables();
        }
        finally
        {
            _applyingRender = false;
        }
    }

    private double ColumnWidth()
    {
        const double minColumnWidth = 168;
        var available = TemplateScroll.ViewportWidth;
        if (available < 1)
        {
            available = _laidOutViewport;
        }

        return _columns.Count == 0 ? minColumnWidth : Math.Max(minColumnWidth, available / _columns.Count);
    }

    private UIElement BuildHeader(double columnWidth)
    {
        var grid = new Grid
        {
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = columnWidth * _columns.Count,
        };
        for (var c = 0; c < _columns.Count; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(columnWidth) });
            var column = _columns[c];
            var selected = column.Key == _selectedColumnKey;
            var cell = new Border
            {
                Background = selected ? Brush("RowSelectedBackgroundBrush") : Brush("GridHeaderBackgroundBrush"),
                BorderBrush = selected ? Brush("AccentPrimaryBrush") : Brush("BorderDefaultBrush"),
                BorderThickness = new Thickness(selected ? 2 : 1),
                Child = new Grid(),
            };
            var inner = (Grid)cell.Child;
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

            var captured = column;
            var columnIndex = c;
            var labelBox = new TextBox
            {
                Text = column.Label,
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Foreground = Brush("TextPrimaryBrush"),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 6, 4, 6),
                PlaceholderText = "Column name",
                IsEnabled = !_viewOnly,
                IsReadOnly = _viewOnly,
            };
            labelBox.GotFocus += (_, _) =>
            {
                if (!string.Equals(_selectedColumnKey, captured.Key, StringComparison.Ordinal))
                {
                    _selectedColumnKey = captured.Key;
                    ApplyRender();
                }

                StatusText.Text = _viewOnly
                    ? "View-only workspace — browse template data here. Use Edit on Analytics to change structure or values."
                    : "Select a column header, then choose a master data column below to import values.";
                if (!_viewOnly)
                {
                    MaybeGrowColumnsForIndex(columnIndex);
                }
            };
            if (!_viewOnly)
            {
                labelBox.TextChanged += (_, _) => captured.Label = labelBox.Text ?? string.Empty;
            }

            Grid.SetColumn(labelBox, 0);
            inner.Children.Add(labelBox);

            if (!_viewOnly)
            {
                var more = new Button
                {
                    Width = 32,
                    Height = 32,
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Content = new FontIcon
                    {
                        Glyph = "\uE712",
                        FontSize = 14,
                        Foreground = Brush("TextSecondaryBrush"),
                    },
                };
                ToolTipService.SetToolTip(more, "Column actions");
                var menu = new MenuFlyout();
                var copyItem = new MenuFlyoutItem { Text = "Copy a new column" };
                copyItem.Click += async (_, _) => await DuplicateColumnAsync(captured).ConfigureAwait(true);
                var deleteItem = new MenuFlyoutItem { Text = "Delete" };
                deleteItem.Click += async (_, _) => await DeleteColumnAsync(captured).ConfigureAwait(true);
                menu.Items.Add(copyItem);
                menu.Items.Add(deleteItem);
                more.Flyout = menu;
                Grid.SetColumn(more, 1);
                inner.Children.Add(more);
            }

            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        return grid;
    }

    private UIElement BuildVirtualRows(double columnWidth)
    {
        return new ItemsRepeater
        {
            ItemsSource = _rows,
            ItemTemplate = new TemplateRowFactory(this, columnWidth),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
    }

    private sealed class TemplateRowFactory : IElementFactory
    {
        private readonly TemplateComposerPanel _owner;
        private readonly double _columnWidth;

        public TemplateRowFactory(TemplateComposerPanel owner, double columnWidth)
        {
            _owner = owner;
            _columnWidth = columnWidth;
        }

        public UIElement GetElement(ElementFactoryGetArgs args)
        {
            var row = (RowSlot)args.Data;
            var grid = args.Parent as Grid ?? CreateGrid();
            grid.Tag = row;
            var index = _owner._rows.IndexOf(row);
            for (var c = 0; c < _owner._columns.Count && c < grid.Children.Count; c++)
            {
                var column = _owner._columns[c];
                var selected = column.Key == _owner._selectedColumnKey;
                row.Values.TryGetValue(column.Key, out var text);
                var cell = (Border)grid.Children[c];
                cell.Background = selected
                    ? TemplateComposerPanel.Brush("RowSelectedBackgroundBrush")
                    : TemplateComposerPanel.Brush(index % 2 == 0 ? "CardBackgroundBrush" : "GridRowAltBackgroundBrush");
                cell.BorderBrush = selected ? TemplateComposerPanel.Brush("AccentPrimaryBrush") : TemplateComposerPanel.Brush("BorderDefaultBrush");
                cell.BorderThickness = new Thickness(selected ? 2 : 1, 0, selected ? 2 : 1, 1);
                if (cell.Child is TextBox box)
                {
                    var next = text ?? string.Empty;
                    if (!string.Equals(box.Text, next, StringComparison.Ordinal))
                    {
                        box.Text = next;
                    }
                }
            }

            return grid;
        }

        public void RecycleElement(ElementFactoryRecycleArgs args)
        {
        }

        private Grid CreateGrid()
        {
            var grid = new Grid
            {
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = _columnWidth * Math.Max(1, _owner._columns.Count),
            };
            for (var c = 0; c < _owner._columns.Count; c++)
            {
                var column = _owner._columns[c];
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_columnWidth) });
                var cell = new Border
                {
                    Padding = new Thickness(4, 0, 4, 0),
                };
                var box = new TextBox
                {
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    BorderThickness = new Thickness(0),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Foreground = TemplateComposerPanel.Brush("TextPrimaryBrush"),
                    PlaceholderText = "—",
                    IsEnabled = !_owner._viewOnly,
                    IsReadOnly = _owner._viewOnly,
                };
                if (!_owner._viewOnly)
                {
                    var columnKey = column.Key;
                    box.TextChanged += (_, _) =>
                    {
                        if (grid.Tag is RowSlot rowSlot)
                        {
                            rowSlot.Values[columnKey] = box.Text ?? string.Empty;
                            var rowIndex = _owner._rows.IndexOf(rowSlot);
                            if (rowIndex >= 0)
                            {
                                _owner.MaybeGrowRowsForIndex(rowIndex);
                            }
                        }
                    };
                }
                cell.Child = box;
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }

            return grid;
        }
    }

    private UIElement BuildSourceTables()
    {
        var stack = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Stretch };
        var query = _masterDataSearchQuery.Trim();
        var catalogs = string.IsNullOrWhiteSpace(query)
            ? _sources
            : _sources
                .Select(source => FilterSourceCatalog(source, query))
                .Where(source => source is not null)
                .Cast<SourceCatalog>()
                .ToList();

        if (catalogs.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(query)
                    ? "No other master data catalogs are available yet."
                    : "No master data columns match your search.",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("TextSecondaryBrush"),
            });
            return stack;
        }

        foreach (var source in catalogs)
        {
            var block = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
            block.Children.Add(new TextBlock
            {
                Text = source.Name,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("TextPrimaryBrush"),
            });

            const double chipWidth = 148;
            var row = new Grid { Height = 40 };
            for (var i = 0; i < source.Fields.Count; i++)
            {
                var field = source.Fields[i];
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(chipWidth) });
                var used = IsSourceUsed(source.Id, field.Key);
                var chip = new Border
                {
                    Height = 40,
                    MinWidth = chipWidth,
                    Width = chipWidth,
                    IsHitTestVisible = !used,
                    Opacity = used ? 0.4 : 1,
                    Background = Brush("GridHeaderBackgroundBrush"),
                    BorderBrush = Brush("BorderDefaultBrush"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 0, 10, 0),
                    Child = new TextBlock
                    {
                        Text = field.Label,
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Foreground = Brush("TextPrimaryBrush"),
                    },
                };
                ToolTipService.SetToolTip(
                    chip,
                    used ? $"{field.Label} is already used" : $"Import \"{field.Label}\" into the selected column");
                var catalog = source;
                var sourceField = field;
                chip.AddHandler(
                    UIElement.PointerPressedEvent,
                    new PointerEventHandler((_, args) =>
                    {
                        if (!args.GetCurrentPoint(chip).Properties.IsLeftButtonPressed || used)
                        {
                            return;
                        }

                        args.Handled = true;
                        _ = ImportSourceColumnAsync(catalog, sourceField);
                    }),
                    true);
                Grid.SetColumn(chip, i);
                row.Children.Add(chip);
            }

            block.Children.Add(new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Auto,
                VerticalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                IsTabStop = false,
                Content = row,
            });
            stack.Children.Add(block);
        }

        return stack;
    }

    private static SourceCatalog? FilterSourceCatalog(SourceCatalog source, string query)
    {
        if (source.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        var fields = source.Fields
            .Where(f => f.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || f.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return fields.Count == 0 ? null : source with { Fields = fields };
    }

    private bool IsSourceUsed(string templateId, string fieldKey) =>
        _columns.Any(c =>
            string.Equals(c.SourceTemplateId, templateId, StringComparison.Ordinal)
            && string.Equals(c.SourceFieldKey, fieldKey, StringComparison.Ordinal));

    private void AddEmptyRows(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _rows.Add(new RowSlot { Values = new Dictionary<string, string>(StringComparer.Ordinal) });
        }
    }

    private void EnsureEditingBuffer()
    {
        while (_columns.Count < MinEditingColumns)
        {
            var number = _columns.Count + 1;
            _columns.Add(new ColumnSlot { Key = NewKey(), Label = $"Column {number}" });
        }

        var targetRows = Math.Max(_rows.Count + TrailingRowBuffer, MinEditingRows);
        if (_rows.Count < targetRows)
        {
            AddEmptyRows(targetRows - _rows.Count);
        }

        if (_selectedColumnKey is null || _columns.All(c => c.Key != _selectedColumnKey))
        {
            _selectedColumnKey = _columns[0].Key;
        }
    }

    private void GrowRowsNearEnd()
    {
        if (_busy)
        {
            return;
        }

        AddEmptyRows(GrowRowsBatch);
        ApplyRender();
    }

    private void MaybeGrowRowsForIndex(int rowIndex)
    {
        if (_busy || rowIndex < _rows.Count - 3)
        {
            return;
        }

        AddEmptyRows(GrowRowsBatch);
        ApplyRender();
    }

    private void MaybeGrowColumnsForIndex(int columnIndex)
    {
        if (_busy || columnIndex < _columns.Count - 1)
        {
            return;
        }

        var number = _columns.Count + 1;
        _columns.Add(new ColumnSlot { Key = NewKey(), Label = $"Column {number}" });
        ApplyRender();
    }

    private void TrimGridToUsedExtents()
    {
        var lastRow = -1;
        for (var r = 0; r < _rows.Count; r++)
        {
            if (RowHasContent(_rows[r]))
            {
                lastRow = r;
            }
        }

        var lastCol = -1;
        for (var c = 0; c < _columns.Count; c++)
        {
            if (ColumnHasContent(_columns[c], c))
            {
                lastCol = c;
            }
        }

        lastRow = Math.Max(lastRow, 0);
        lastCol = Math.Max(lastCol, 0);

        while (_rows.Count > lastRow + 1)
        {
            var tail = _rows[^1];
            if (!string.IsNullOrWhiteSpace(tail.Id))
            {
                _removedRowIds.Add(tail.Id!);
            }

            _rows.RemoveAt(_rows.Count - 1);
        }

        while (_columns.Count > lastCol + 1)
        {
            var removed = _columns[^1];
            _columns.RemoveAt(_columns.Count - 1);
            foreach (var row in _rows)
            {
                row.Values.Remove(removed.Key);
            }
        }

        if (_selectedColumnKey is null || _columns.All(c => c.Key != _selectedColumnKey))
        {
            _selectedColumnKey = _columns[0].Key;
        }
    }

    private static bool RowHasContent(RowSlot row) =>
        row.Values.Values.Any(static value => !string.IsNullOrWhiteSpace(value));

    private bool ColumnHasContent(ColumnSlot column, int columnIndex)
    {
        if (!string.IsNullOrWhiteSpace(column.SourceTemplateId) || !string.IsNullOrWhiteSpace(column.SourceFieldKey))
        {
            return true;
        }

        if (!IsDefaultColumnLabel(column.Label, columnIndex + 1))
        {
            return true;
        }

        return _rows.Any(row =>
            row.Values.TryGetValue(column.Key, out var value) && !string.IsNullOrWhiteSpace(value));
    }

    private static bool IsDefaultColumnLabel(string label, int number) =>
        string.Equals(label.Trim(), $"Column {number}", StringComparison.OrdinalIgnoreCase);

    private static string NewKey() => "col_" + Guid.NewGuid().ToString("N")[..8];

    private static string? EncodeSource(string? templateId, string? fieldKey) =>
        string.IsNullOrWhiteSpace(templateId) || string.IsNullOrWhiteSpace(fieldKey)
            ? null
            : $"{SourcePrefix}{templateId}|{fieldKey}";

    private static void ParseSource(string? pattern, out string? templateId, out string? fieldKey)
    {
        templateId = null;
        fieldKey = null;
        if (MasterDataSourceFieldEncoding.TryParse(pattern, out var catalogId, out var sourceFieldKey))
        {
            templateId = catalogId;
            fieldKey = sourceFieldKey;
        }
    }

    private static Brush Brush(string key) =>
        Application.Current.Resources[key] as Brush
        ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private sealed class ColumnSlot
    {
        public required string Key { get; init; }
        public string Label { get; set; } = string.Empty;
        public string? SourceTemplateId { get; set; }
        public string? SourceFieldKey { get; set; }
    }

    private sealed class RowSlot
    {
        public string? Id { get; set; }
        public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed record SourceCatalog(string Id, string Name, IReadOnlyList<TemplateFieldDefinition> Fields);
}
