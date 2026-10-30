using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using PartFinder.Models;
using PartFinder.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace PartFinder.Views.Components;

public sealed partial class FavouritesSubPage : UserControl
{
    private FavouritesViewModel? _vm;
    private TemplatesViewModel? _templatesVm;
    private readonly List<Border> _cardElements = new();

    // Per-card column scroll offset (0 = first 4 cols visible)
    private readonly Dictionary<Border, int> _cardColOffset = new();

    // Peek width: how much of a side card is visible on each edge
    // Center card fills the rest: canvasWidth - 2 * PEEK_WIDTH
    private const double PEEK_WIDTH_MIN = 60.0;
    private const double PEEK_WIDTH_MAX = 130.0;
    private const double PEEK_WIDTH_RATIO = 0.13; // 13 % of canvas width

    // Theme colors — pulled from Colors.xaml
    private static readonly Color _cardBg = Color.FromArgb(224, 20, 29, 42);
    private static readonly Color _elevatedBg = Color.FromArgb(236, 26, 38, 55);
    private static readonly Color _accentPrimary = Color.FromArgb(255, 94, 162, 255);
    private static readonly Color _borderDefault = Color.FromArgb(176, 104, 132, 168);
    private static readonly Color _textPrimary = Color.FromArgb(255, 244, 248, 255);
    private static readonly Color _textSecondary = Color.FromArgb(255, 196, 208, 223);
    private static readonly Color _textTertiary = Color.FromArgb(255, 147, 166, 192);

    // Accent gradient colors for card decorative elements
    private static readonly Color _accentCyan = Color.FromArgb(255, 56, 189, 248);    // cyan glow
    private static readonly Color _accentPurple = Color.FromArgb(255, 168, 85, 247);   // purple glow
    private static readonly Color _accentPink = Color.FromArgb(255, 236, 72, 153);     // pink glow

    // Raised when the user clicks "Back to Templates"
    public event EventHandler? BackRequested;

    public FavouritesSubPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task ShowAsync(TemplatesViewModel templatesVm)
    {
        _templatesVm = templatesVm;

        _vm = App.Services.GetRequiredService<FavouritesViewModel>();
        DataContext = _vm;

        _vm.PropertyChanged -= OnVmPropertyChanged; // Avoid duplicates
        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.EditTemplateRequested -= OnEditTemplateRequested;
        _vm.EditTemplateRequested += OnEditTemplateRequested;
        _vm.FavouriteTemplates.CollectionChanged -= OnFavouriteTemplatesCollectionChanged;
        _vm.FavouriteTemplates.CollectionChanged += OnFavouriteTemplatesCollectionChanged;

        await _vm.LoadAsync(templatesVm.Templates).ConfigureAwait(true);

        BuildCarouselCards();

        // Use DispatcherQueue to ensure layout is ready before positioning cards
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
        {
            await Task.Delay(50); // Small delay for Canvas to measure
            UpdateCarouselLayout(animate: false);
        });

        // Entrance animation
        if (Resources["EntranceSb"] is Storyboard entrance)
        {
            entrance.Begin();
        }
    }

    public Task PlayExitAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        if (Resources["ExitSb"] is Storyboard exit)
        {
            exit.Completed += (_, _) => tcs.TrySetResult(true);
            exit.Begin();
        }
        else
        {
            tcs.SetResult(true);
        }

        return tcs.Task;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Update layout and clip when canvas size changes
        CarouselCanvas.SizeChanged += (s, args) =>
        {
            CarouselClip.Rect = new Windows.Foundation.Rect(0, 0, args.NewSize.Width, args.NewSize.Height);
            UpdateCarouselLayout(animate: false);
        };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.EditTemplateRequested -= OnEditTemplateRequested;
            _vm.FavouriteTemplates.CollectionChanged -= OnFavouriteTemplatesCollectionChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FavouritesViewModel.ActiveIndex))
        {
            UpdateCarouselLayout(animate: true);
        }
    }

    private void OnFavouriteTemplatesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Rebuild carousel smoothly whenever a card is added or removed
        BuildCarouselCards();
        UpdateCarouselLayout(animate: true);
    }

    // ── Carousel Building ─────────────────────────────────────────────────────

    private void BuildCarouselCards()
    {
        if (_vm is null)
            return;

        CarouselCanvas.Children.Clear();
        _cardElements.Clear();
        _cardColOffset.Clear();

        for (int i = 0; i < _vm.FavouriteTemplates.Count; i++)
        {
            var template = _vm.FavouriteTemplates[i];
            var card = CreateTemplateCard(template, i + 1);
            _cardElements.Add(card);
            _cardColOffset[card] = 0;
            CarouselCanvas.Children.Add(card);
        }
    }

    private Border CreateTemplateCard(FavouriteCardViewModel template, int number)
    {
        var (accentColor, accentColorAlt) = GetCardAccentColors(number);

        var card = new Border
        {
            Width = 300,   // initial width, overridden in UpdateCarouselLayout
            Height = 400,  // initial height, overridden in UpdateCarouselLayout
            CornerRadius = new CornerRadius(24),
            Background = new SolidColorBrush(_cardBg),
            BorderBrush = new SolidColorBrush(Color.FromArgb(140, _borderDefault.R, _borderDefault.G, _borderDefault.B)),
            BorderThickness = new Thickness(1.5),
            Tag = template,
            Padding = new Thickness(0),
            Translation = new System.Numerics.Vector3(0, 0, 32),
        };
        card.Shadow = new ThemeShadow();

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });         // top: name
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // mid: fields preview
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });         // bottom: buttons

        // ── Glassmorphism top edge ──
        var glassEdge = new Border
        {
            CornerRadius = new CornerRadius(24, 24, 0, 0),
            Height = 1,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Opacity = 0.16,
            Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            Margin = new Thickness(1, 0, 1, 0),
        };
        mainGrid.Children.Add(glassEdge);

        // ── Row 0: centered title + top-right actions ──
        var topSection = new Grid
        {
            Name = "TopSection",
            Margin = new Thickness(20, 24, 20, 0),
        };
        topSection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
        topSection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topSection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });

        var titleStack = new StackPanel
        {
            Name = "TitleStack",
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleStack.Children.Add(new FontIcon
        {
            Glyph = "\uE8A5",
            FontSize = 20,
            Foreground = new SolidColorBrush(accentColor),
            VerticalAlignment = VerticalAlignment.Center,
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = template.Name,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(_textPrimary),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220,
        });
        Grid.SetColumn(titleStack, 1);
        topSection.Children.Add(titleStack);

        var topActions = new StackPanel
        {
            Name = "ButtonStack",
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        var editBtn = new Button
        {
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(28, _accentPrimary.R, _accentPrimary.G, _accentPrimary.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, _accentPrimary.R, _accentPrimary.G, _accentPrimary.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Tag = template,
            Content = new FontIcon
            {
                Glyph = "\uE70F",
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            },
        };
        ToolTipService.SetToolTip(editBtn, "Edit template");
        editBtn.Click += OnCardEditClick;

        var unstarBtn = new Button
        {
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(10, 255, 193, 7)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 193, 7)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Tag = template,
            Content = new FontIcon
            {
                Glyph = "\uE735",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)),
            },
        };
        ToolTipService.SetToolTip(unstarBtn, "Remove from favourites");
        unstarBtn.Click += OnCardUnstarClick;

        topActions.Children.Add(editBtn);
        topActions.Children.Add(unstarBtn);
        Grid.SetColumn(topActions, 2);
        topSection.Children.Add(topActions);

        Grid.SetRow(topSection, 0);
        mainGrid.Children.Add(topSection);

        // ── Row 1: Unified table — headers + data in one Grid ──
        var fieldsSection = new StackPanel
        {
            Name = "FieldsSection",
            Spacing = 0,
            Margin = new Thickness(16, 16, 16, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };

        var fields = template.Template.Fields
            .OrderBy(f => f.DisplayOrder)
            .ToList();

        int visibleCols = Math.Min(fields.Count, 4);

        // Build one unified table grid — all rows, scroll handles overflow
        var tableGrid = BuildTableGrid(fields, 0, accentColor, template.Records, template.Records.Count);

        // Clipped container for slide animation
        var headersClip = new Border
        {
            Name = "HeadersClip",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        headersClip.SizeChanged += (s, _) =>
        {
            if (s is Border b && b.ActualWidth > 0 && b.ActualHeight > 0)
                b.Clip = new RectangleGeometry
                    { Rect = new Windows.Foundation.Rect(0, 0, b.ActualWidth, b.ActualHeight + 4) };
        };
        headersClip.Child = tableGrid;
        fieldsSection.Children.Add(headersClip);

        Grid.SetRow(fieldsSection, 1);
        mainGrid.Children.Add(fieldsSection);

        // ── Row 2: Bottom — [<]  [field count]  [>] ──
        var bottomGrid = new Grid
        {
            Name = "BottomSection",
            Margin = new Thickness(16, 0, 16, 28),
        };
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // col 0: [<]
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // col 1: center
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // col 2: [>]

        // Field count label + buttons stacked in center column
        var centerStack = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var fieldText = new TextBlock
        {
            Text = template.FieldCountLabel,
            FontSize = 14,
            Foreground = new SolidColorBrush(_textTertiary),
            HorizontalAlignment = HorizontalAlignment.Center,
            CharacterSpacing = 20,
        };
        centerStack.Children.Add(fieldText);

        Grid.SetColumn(centerStack, 1);
        bottomGrid.Children.Add(centerStack);

        // ── Col-nav arrows (only when fields > 4) ──
        bool hasMoreCols = fields.Count > 4;

        var colPrevBtn = new Button
        {
            Name = "ColPrevBtn",
            Width = 32, Height = 32, Padding = new Thickness(0),
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 0),
            Visibility = hasMoreCols ? Visibility.Visible : Visibility.Collapsed,
            IsEnabled = false, // offset=0 at start, can't go back
        };
        colPrevBtn.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        colPrevBtn.Resources["ButtonBackgroundPressed"]     = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
        colPrevBtn.Content = new FontIcon { Glyph = "\uE76B", FontSize = 14, Foreground = new SolidColorBrush(_textPrimary) };
        Grid.SetColumn(colPrevBtn, 0);
        bottomGrid.Children.Add(colPrevBtn);

        var colNextBtn = new Button
        {
            Name = "ColNextBtn",
            Width = 32, Height = 32, Padding = new Thickness(0),
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 0),
            Visibility = hasMoreCols ? Visibility.Visible : Visibility.Collapsed,
            IsEnabled = hasMoreCols, // can go forward if more cols
        };
        colNextBtn.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        colNextBtn.Resources["ButtonBackgroundPressed"]     = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
        colNextBtn.Content = new FontIcon { Glyph = "\uE76C", FontSize = 14, Foreground = new SolidColorBrush(_textPrimary) };
        Grid.SetColumn(colNextBtn, 2);
        bottomGrid.Children.Add(colNextBtn);

        // Wire up col-nav click handlers
        colPrevBtn.Click += (_, _) => ShiftCardColumns(card, fields, accentColor, headersClip, colPrevBtn, colNextBtn, -1, template.Records, template.Records.Count);
        colNextBtn.Click += (_, _) => ShiftCardColumns(card, fields, accentColor, headersClip, colPrevBtn, colNextBtn, +1, template.Records, template.Records.Count);

        Grid.SetRow(bottomGrid, 2);
        mainGrid.Children.Add(bottomGrid);

        card.Child = mainGrid;
        card.Tapped += OnCardTapped;
        return card;
    }



    /// <summary>
    /// Returns accent color pair for each card based on index — gives each card a unique gradient feel.
    /// </summary>
    private (Color primary, Color secondary) GetCardAccentColors(int index)
    {
        return (index % 4) switch
        {
            0 => (_accentCyan, _accentPrimary),
            1 => (_accentPurple, _accentPink),
            2 => (_accentPrimary, _accentCyan),
            3 => (_accentPink, _accentPurple),
            _ => (_accentPrimary, _accentCyan),
        };
    }

    // ── Column-scroll helpers ─────────────────────────────────────────────────

    /// <summary>Builds a 4-column headers Grid starting at <paramref name="offset"/>.</summary>
    private Grid BuildHeadersGrid(List<TemplateFieldDefinition> fields, int offset, Color accent)
    {
        int visibleCols = Math.Min(fields.Count - offset, 4);
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        for (int c = 0; c < visibleCols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int fi = 0; fi < visibleCols; fi++)
        {
            var field = fields[offset + fi];
            var typeIcon = field.Type switch
            {
                Models.TemplateFieldType.Text       => "\uE8D2", // Document — text content
                Models.TemplateFieldType.Number     => "\uE8EF", // # Symbol — whole number
                Models.TemplateFieldType.Decimal    => "\uEB50", // Decimal point — decimal values
                Models.TemplateFieldType.Date       => "\uE787", // Calendar — date picker
                Models.TemplateFieldType.Dropdown   => "\uE8B5", // List — dropdown selection
                Models.TemplateFieldType.Boolean    => "\uE73E", // Checkmark — true/false
                Models.TemplateFieldType.RecordLink => "\uE71B", // Link — record reference
                _                                   => "\uE8D2",
            };

            var cell = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(100, accent.R, accent.G, accent.B)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(6, 10, 6, 10),
                Margin          = new Thickness(fi == 0 ? 0 : 3, 0, fi == visibleCols - 1 ? 0 : 3, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var content = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new FontIcon { Glyph = typeIcon, FontSize = 13, Foreground = new SolidColorBrush(accent), HorizontalAlignment = HorizontalAlignment.Center });
            content.Children.Add(new TextBlock
            {
                Text = field.Label, FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(_textPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            cell.Child = content;
            Grid.SetColumn(cell, fi);
            grid.Children.Add(cell);
        }
        return grid;
    }

    private Grid BuildTableGrid(
        List<TemplateFieldDefinition> fields,
        int offset,
        Color accent,
        IReadOnlyList<MasterDataRowRecord> records,
        int maxRows)
    {
        int visibleCols = Math.Min(fields.Count - offset, 4);

        // One column per field — each column is a self-contained bordered box
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, ColumnSpacing = 8 };
        for (int c = 0; c < visibleCols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int fi = 0; fi < visibleCols; fi++)
        {
            var field = fields[offset + fi];

            // Outer column border — wraps header + all data for this column
            var colBorder = new Border
            {
                BorderBrush     = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var colStack = new StackPanel { Spacing = 0 };

            // ── Fixed header ──
            var headerCell = new Border
            {
                Background  = new SolidColorBrush(Color.FromArgb(50, accent.R, accent.G, accent.B)),
                Padding     = new Thickness(6, 12, 6, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var typeIcon = field.Type switch
            {
                Models.TemplateFieldType.Text       => "\uE8D2", // Document/Text
                Models.TemplateFieldType.Number     => "\uE8EF", // Number symbol
                Models.TemplateFieldType.Decimal    => "\uEB50", // Decimal
                Models.TemplateFieldType.Date       => "\uE787", // Calendar
                Models.TemplateFieldType.Dropdown   => "\uE8FD", // List
                Models.TemplateFieldType.Boolean    => "\uE73E", // Checkmark
                Models.TemplateFieldType.RecordLink => "\uE71B", // Link
                _                                   => "\uE8D2",
            };

            // Icon + label — icon left, text centered, spacer right for true centering
            var headerContent = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconEl = new FontIcon
            {
                Glyph = typeIcon, FontSize = 14,
                Foreground = new SolidColorBrush(accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 6, 0),
            };
            Grid.SetColumn(iconEl, 0);
            headerContent.Children.Add(iconEl);

            var labelEl = new TextBlock
            {
                Text = field.Label.ToUpperInvariant(),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1,
            };
            Grid.SetColumn(labelEl, 1);
            headerContent.Children.Add(labelEl);

            var spacer = new Border { Width = 22 };
            Grid.SetColumn(spacer, 2);
            headerContent.Children.Add(spacer);

            headerCell.Child = headerContent;
            colStack.Children.Add(headerCell);

            // Divider below header
            colStack.Children.Add(new Border
            {
                Height     = 1,
                Background = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B)),
            });

            // ── Scrollable data rows ──
            var dataStack = new StackPanel { Spacing = 0 };

            for (int ri = 0; ri < records.Count; ri++)
            {
                if (ri > 0)
                    dataStack.Children.Add(new Border
                    {
                        Height     = 1,
                        Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                    });

                var record = records[ri];
                record.Values.TryGetValue(field.Key, out var cellValue);

                dataStack.Children.Add(new Border
                {
                    Background  = new SolidColorBrush(Color.FromArgb(ri % 2 == 0 ? (byte)10 : (byte)4, 255, 255, 255)),
                    Padding     = new Thickness(6, 7, 6, 7),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Child = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(cellValue) ? "—" : cellValue,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1,
                    },
                });
            }

            colStack.Children.Add(new ScrollViewer
            {
                Content = dataStack,
                VerticalScrollMode            = ScrollMode.Auto,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Hidden,
                HorizontalScrollMode          = ScrollMode.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 245,
            });

            colBorder.Child = colStack;
            Grid.SetColumn(colBorder, fi);
            grid.Children.Add(colBorder);
        }

        return grid;
    }

    /// <summary>
    /// Slides the column headers left or right with animation, then updates data rows.
    /// direction: +1 = next cols, -1 = prev cols
    /// </summary>
    private void ShiftCardColumns(
        Border card,
        List<TemplateFieldDefinition> fields,
        Color accent,
        Border headersClip,
        Button prevBtn, Button nextBtn,
        int direction,
        IReadOnlyList<MasterDataRowRecord> records,
        int maxRows)
    {
        if (!_cardColOffset.TryGetValue(card, out var currentOffset)) return;

        int newOffset = currentOffset + direction * 4;
        newOffset = Math.Max(0, Math.Min(newOffset, fields.Count - 1));
        if (newOffset == currentOffset) return;

        _cardColOffset[card] = newOffset;

        prevBtn.IsEnabled = newOffset > 0;
        nextBtn.IsEnabled = newOffset + 4 < fields.Count;

        // Build new unified table (headers + data)
        var newGrid = BuildTableGrid(fields, newOffset, accent, records, maxRows);

        double clipWidth = headersClip.ActualWidth > 0 ? headersClip.ActualWidth : 300;
        double slideFrom = direction > 0 ? clipWidth : -clipWidth;

        // Position new grid off-screen
        newGrid.RenderTransform = new TranslateTransform { X = slideFrom };
        var oldGrid = headersClip.Child as FrameworkElement;
        headersClip.Child = null;

        if (oldGrid != null)
            oldGrid.RenderTransform ??= new TranslateTransform();

        var container = new Grid();
        if (oldGrid != null) container.Children.Add(oldGrid);
        container.Children.Add(newGrid);
        headersClip.Child = container;
        var duration = TimeSpan.FromMilliseconds(300);
        var easing   = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var sb = new Storyboard();

        if (oldGrid?.RenderTransform is TranslateTransform oldTt)
        {
            var outAnim = new DoubleAnimation
            {
                To = -slideFrom, Duration = duration,
                EasingFunction = easing, EnableDependentAnimation = true,
            };
            Storyboard.SetTarget(outAnim, oldTt);
            Storyboard.SetTargetProperty(outAnim, "X");
            sb.Children.Add(outAnim);
        }

        if (newGrid.RenderTransform is TranslateTransform newTt)
        {
            var inAnim = new DoubleAnimation
            {
                From = slideFrom, To = 0, Duration = duration,
                EasingFunction = easing, EnableDependentAnimation = true,
            };
            Storyboard.SetTarget(inAnim, newTt);
            Storyboard.SetTargetProperty(inAnim, "X");
            sb.Children.Add(inAnim);
        }

        sb.Completed += (_, _) =>
        {
            // After animation: detach newGrid from container, then set as direct child
            if (headersClip.Child is Grid cont && cont.Children.Contains(newGrid))
                cont.Children.Remove(newGrid);
            newGrid.RenderTransform = null;
            headersClip.Child = newGrid;
        };

        sb.Begin();
    }

    private string GetIconForTemplate(int index)
    {
        // Use a consistent star icon for all templates as requested
        return "\uE734"; // Empty star (\uE735 is filled star)
    }

    private void OnCardTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border card && card.Tag is FavouriteCardViewModel template && _vm is not null)
        {
            var index = _vm.FavouriteTemplates.IndexOf(template);
            if (index >= 0 && index != _vm.ActiveIndex)
            {
                _vm.ActiveIndex = index;
            }
        }
    }

    // ── Carousel Layout & Animation ───────────────────────────────────────────

    private void UpdateCarouselLayout(bool animate)
    {
        if (_vm is null || _cardElements.Count == 0)
            return;

        var canvasWidth = CarouselCanvas.ActualWidth;
        var canvasHeight = CarouselCanvas.ActualHeight;

        // If canvas hasn't measured yet, schedule a retry
        if (canvasWidth <= 0)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                UpdateCarouselLayout(animate);
            });
            return;
        }

        if (canvasHeight <= 0) canvasHeight = 400;

        // Update clip so side cards don't overflow
        CarouselClip.Rect = new Windows.Foundation.Rect(0, 0, canvasWidth, canvasHeight);

        var activeIndex = _vm.ActiveIndex;

        // Peek width = how much of a side card is visible on each edge
        var peekWidth = Math.Clamp(canvasWidth * PEEK_WIDTH_RATIO, PEEK_WIDTH_MIN, PEEK_WIDTH_MAX);

        // All cards share the same width and height — same size, side by side
        var cardWidth  = canvasWidth - 2.0 * peekWidth;
        var cardHeight = canvasHeight;

        // Gap between cards (small gap so borders are clearly separate)
        const double cardGap = 12.0;

        for (int i = 0; i < _cardElements.Count; i++)
        {
            var card = _cardElements[i];
            var distance = i - activeIndex;
            var isCenter = distance == 0;
            var isLeft   = distance == -1;
            var isRight  = distance == 1;
            var isVisible = Math.Abs(distance) <= 1;

            double finalX, finalY, opacity, zIndex;
            byte borderAlpha;
            Color glowColor;
            var (accentColor, _) = GetCardAccentColors(i + 1);

            if (isCenter)
            {
                // Center card: sits in the middle, full opacity, accent border
                finalX      = peekWidth;
                finalY      = 0;
                opacity     = 1.0;
                zIndex      = 100;
                borderAlpha = 200;
                glowColor   = accentColor;
            }
            else if (isLeft)
            {
                // Left card: positioned so its right edge peeks out from the left
                finalX      = peekWidth - cardWidth - cardGap;
                finalY      = 0;
                opacity     = 1.0;
                zIndex      = 90;
                borderAlpha = 160;
                glowColor   = _borderDefault;
            }
            else if (isRight)
            {
                // Right card: positioned so its left edge peeks out from the right
                finalX      = peekWidth + cardWidth + cardGap;
                finalY      = 0;
                opacity     = 1.0;
                zIndex      = 90;
                borderAlpha = 160;
                glowColor   = _borderDefault;
            }
            else
            {
                // All other cards: hidden off-screen
                finalX      = distance < 0 ? -(cardWidth + 40) : canvasWidth + 40;
                finalY      = 0;
                opacity     = 0.0;
                zIndex      = 5;
                borderAlpha = 0;
                glowColor   = _borderDefault;
            }

            // All visible cards show full content — no hiding on side cards
            ApplyCardContentVisibility(card, isCenter, isLeft, isRight);

            card.IsHitTestVisible = isVisible;

            // Border: center gets accent glow, sides get same-style border
            card.BorderBrush = new SolidColorBrush(
                Color.FromArgb(borderAlpha, glowColor.R, glowColor.G, glowColor.B));

            // No scale or blur — all cards same size, flat layout
            card.RenderTransform = null;

            if (animate)
                AnimateCard(card, finalX, finalY, cardWidth, cardHeight, opacity, zIndex);
            else
                ApplyCardTransform(card, finalX, finalY, cardWidth, cardHeight, opacity, zIndex);
        }
    }

    /// <summary>
    /// Shows full content on center card.
    /// Side cards show everything EXCEPT the Edit/Unstar buttons (those only make sense on focused card).
    /// </summary>
    private static void ApplyCardContentVisibility(Border card, bool isCenter, bool isLeft, bool isRight)
    {
        if (card.Child is not Grid grid) return;

        foreach (var child in grid.Children)
        {
            if (child is not FrameworkElement fe) continue;

            switch (fe.Name)
            {
                case "TopSection":
                    fe.Opacity = 1.0;
                    if (fe is Grid topGrid)
                    {
                        foreach (var topChild in topGrid.Children)
                        {
                            if (topChild is FrameworkElement topElement && topElement.Name == "ButtonStack")
                                topElement.Visibility = isCenter ? Visibility.Visible : Visibility.Collapsed;
                            else if (topChild is StackPanel titleStack && titleStack.Name == "TitleStack")
                            {
                                titleStack.HorizontalAlignment = isCenter
                                    ? HorizontalAlignment.Center
                                    : isLeft
                                        ? HorizontalAlignment.Right
                                        : HorizontalAlignment.Left;

                                if (titleStack.Children.Count > 0 && titleStack.Children[0] is FontIcon icon)
                                    icon.Visibility = isCenter ? Visibility.Visible : Visibility.Collapsed;

                                if (titleStack.Children.Count > 1 && titleStack.Children[1] is TextBlock title)
                                {
                                    title.FontSize = isCenter ? 15 : 13;
                                    title.MaxWidth = isCenter ? 220 : 86;
                                    title.TextAlignment = isLeft ? TextAlignment.Right : TextAlignment.Left;
                                }
                            }
                        }
                    }
                    break;
                case "FieldsSection":
                    fe.Visibility = Visibility.Visible;
                    fe.Opacity = 1.0;
                    break;
                case "BottomSection" when fe is Grid bottomGrid:
                    foreach (var bChild in bottomGrid.Children)
                    {
                        if (bChild is StackPanel centerStack)
                        {
                            foreach (var cc in centerStack.Children)
                            {
                                if (cc is TextBlock txt) txt.Opacity = 1.0;
                            }
                        }
                    }
                    break;
            }
        }
    }

    private void AnimateCard(Border card, double x, double y, double width, double height, double opacity, double zIndex)
    {
        Canvas.SetZIndex(card, (int)zIndex);

        var storyboard = new Storyboard();
        var duration = TimeSpan.FromMilliseconds(450);
        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

        // Position animations (EnableDependentAnimation required for layout properties)
        var xAnim = new DoubleAnimation { To = x, Duration = duration, EasingFunction = easing, EnableDependentAnimation = true };
        var yAnim = new DoubleAnimation { To = y, Duration = duration, EasingFunction = easing, EnableDependentAnimation = true };
        Storyboard.SetTarget(xAnim, card);
        Storyboard.SetTarget(yAnim, card);
        Storyboard.SetTargetProperty(xAnim, "(Canvas.Left)");
        Storyboard.SetTargetProperty(yAnim, "(Canvas.Top)");

        // Size animations
        var widthAnim = new DoubleAnimation { To = width, Duration = duration, EasingFunction = easing, EnableDependentAnimation = true };
        var heightAnim = new DoubleAnimation { To = height, Duration = duration, EasingFunction = easing, EnableDependentAnimation = true };
        Storyboard.SetTarget(widthAnim, card);
        Storyboard.SetTarget(heightAnim, card);
        Storyboard.SetTargetProperty(widthAnim, "Width");
        Storyboard.SetTargetProperty(heightAnim, "Height");

        // Opacity animation
        var opacityAnim = new DoubleAnimation { To = opacity, Duration = duration, EasingFunction = easing };
        Storyboard.SetTarget(opacityAnim, card);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");

        storyboard.Children.Add(xAnim);
        storyboard.Children.Add(yAnim);
        storyboard.Children.Add(widthAnim);
        storyboard.Children.Add(heightAnim);
        storyboard.Children.Add(opacityAnim);

        storyboard.Begin();
    }

    private void ApplyCardTransform(Border card, double x, double y, double width, double height, double opacity, double zIndex)
    {
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
        Canvas.SetZIndex(card, (int)zIndex);
        card.Width = width;
        card.Height = height;
        card.Opacity = opacity;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || !_vm.CanGoPrevious)
            return;

        _vm.GoPreviousCommand.Execute(null);
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || !_vm.CanGoNext)
            return;

        _vm.GoNextCommand.Execute(null);
    }

    // ── Card Actions ──────────────────────────────────────────────────────────

    private void OnCardEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FavouriteCardViewModel template && _vm is not null)
        {
            _vm.BeginEditTemplateCommand.Execute(template.Template.Id);
        }
    }

    private async void OnCardUnstarClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FavouriteCardViewModel template && _vm is not null)
        {
            // Use existing unstar logic - this will remove from favourites and update UI
            await _vm.ToggleFavouriteAsyncCommand.ExecuteAsync(template.Template.Id);
            
            // Force UI refresh after unstar
            BuildCarouselCards();
            UpdateCarouselLayout(animate: true);
        }
    }

    // ── New Template ──────────────────────────────────────────────────────────

    private void OnNewTemplateClick(object sender, RoutedEventArgs e)
    {
        _templatesVm?.StartNewCustomTemplateCommand.Execute(null);
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnBackToTemplatesClick(object sender, RoutedEventArgs e)
    {
        _templatesVm?.HideFavouritesCommand.Execute(null);
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    // ── Edit Template Event ───────────────────────────────────────────────────

    private void OnEditTemplateRequested(object? sender, string templateId)
    {
        if (_templatesVm is null) return;

        // Set the correct template before calling BeginEdit so it edits the right one
        var target = _templatesVm.Templates.FirstOrDefault(
            t => string.Equals(t.Id, templateId, StringComparison.Ordinal));

        if (target is not null)
        {
            _templatesVm.SelectedTemplate = target;
        }

        _templatesVm.BeginEditSelectedTemplateCommand.Execute(null);
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_vm is null)
            return;

        if (e.Key == Windows.System.VirtualKey.Left && _vm.CanGoPrevious)
        {
            OnPreviousClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Right && _vm.CanGoNext)
        {
            OnNextClick(sender, e);
            e.Handled = true;
        }
    }
}
