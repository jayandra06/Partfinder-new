using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.Models;
using PartFinder.Services;
using PartFinder.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace PartFinder.Views.Components;

public sealed partial class AllTemplatesCarousel : UserControl
{
    private readonly List<Border> _cards = new();
    private int _activeIndex = 0;
    private TemplatesViewModel? _vm;
    private readonly IMasterDataRecordsService _records;

    // Per-card column scroll offset
    private readonly Dictionary<Border, int> _cardColOffset = new();

    // Same peek constants as FavouritesSubPage
    private const double PEEK_RATIO = 0.13;
    private const double PEEK_MIN   = 60.0;
    private const double PEEK_MAX   = 130.0;
    private const double CARD_GAP   = 12.0;

    private static readonly Color[] _accents = new[]
    {
        Color.FromArgb(255, 94, 162, 255),
        Color.FromArgb(255, 176, 108, 255),
        Color.FromArgb(255, 88, 214, 255),
        Color.FromArgb(255, 244, 114, 182),
    };

    public AllTemplatesCarousel()
    {
        InitializeComponent();
        _records = App.Services.GetRequiredService<IMasterDataRecordsService>();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async void Load(TemplatesViewModel vm)
    {
        _vm = vm;
        _activeIndex = 0;

        CarouselCanvas.Children.Clear();
        _cards.Clear();
        _cardColOffset.Clear();

        var templates = vm.Templates.ToList();
        
        // Create cards immediately without data (fast UI response)
        for (int i = 0; i < templates.Count; i++)
        {
            var card = BuildCard(templates[i], vm, i, Array.Empty<MasterDataRowRecord>());
            _cards.Add(card);
            // Set initial position off-screen to prevent flicker
            Canvas.SetLeft(card, -500);
            Canvas.SetTop(card, 0);
            card.Opacity = 0;
            CarouselCanvas.Children.Add(card);
        }

        UpdateNav();
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
        {
            await Task.Delay(50);
            UpdateLayout(animate: false);
            
            // Load data asynchronously in background without blocking UI
            _ = LoadTemplateDataAsync(templates);
        });
    }

    private async Task LoadTemplateDataAsync(List<PartTemplateDefinition> templates)
    {
        for (int i = 0; i < templates.Count && i < _cards.Count; i++)
        {
            var template = templates[i];
            IReadOnlyList<MasterDataRowRecord> rows;
            try 
            { 
                rows = await _records.GetRowsAsync(template.Id).ConfigureAwait(true); 
            }
            catch 
            { 
                rows = Array.Empty<MasterDataRowRecord>(); 
            }

            // Update card with data on UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                if (i < _cards.Count)
                {
                    var card = _cards[i];
                    UpdateCardWithData(card, template, rows);
                }
            });
        }
    }

    private void UpdateCardWithData(Border card, PartTemplateDefinition template, IReadOnlyList<MasterDataRowRecord> rows)
    {
        if (card.Child is not Grid mainGrid) return;

        var index     = _cards.IndexOf(card);
        var accent    = _accents[index % _accents.Length];
        var fieldList = template.Fields.OrderBy(f => f.DisplayOrder).ToList();

        foreach (var child in mainGrid.Children)
        {
            if (child is StackPanel sp && sp.Name == "FieldsSection")
            {
                sp.Children.Clear();

                var tableGrid = BuildTableGrid(fieldList, 0, accent, rows, rows.Count);
                tableGrid.Name = "TableGrid";

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
                sp.Children.Add(headersClip);

                _cardColOffset[card] = 0;
                UpdateColNavArrows(card, fieldList.Count);
                break;
            }
        }
    }

    private void UpdateColNavArrows(Border card, int totalFields)
    {
        if (card.Child is not Grid mainGrid) return;
        foreach (var child in mainGrid.Children)
        {
            if (child is Grid g && g.Name == "BottomSection")
            {
                foreach (var gc in g.Children)
                {
                    if (gc is Button btn)
                    {
                        if (btn.Name == "ColPrevBtn") { btn.IsEnabled = false; btn.Visibility = totalFields > 4 ? Visibility.Visible : Visibility.Collapsed; }
                        if (btn.Name == "ColNextBtn") { btn.IsEnabled = totalFields > 4; btn.Visibility = totalFields > 4 ? Visibility.Visible : Visibility.Collapsed; }
                    }
                }
                break;
            }
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CarouselCanvas.SizeChanged += (_, args) =>
        {
            CarouselClip.Rect = new Windows.Foundation.Rect(0, 0, args.NewSize.Width, args.NewSize.Height);
            UpdateLayout(animate: false);
        };
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        if (_cards.Count < 2) return;
        _activeIndex = (_activeIndex - 1 + _cards.Count) % _cards.Count;
        UpdateLayout(animate: true);
        UpdateNav();
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_cards.Count < 2) return;
        _activeIndex = (_activeIndex + 1) % _cards.Count;
        UpdateLayout(animate: true);
        UpdateNav();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Left && _cards.Count > 1)
        {
            OnPreviousClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Right && _cards.Count > 1)
        {
            OnNextClick(sender, e);
            e.Handled = true;
        }
    }

    private void UpdateNav()
    {
        var count = _cards.Count;
        IndexLabel.Text = count == 0 ? "0 / 0" : $"{_activeIndex + 1} / {count}";
        PrevButton.IsEnabled = count > 1;
        NextButton.IsEnabled = count > 1;
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private void UpdateLayout(bool animate)
    {
        if (_cards.Count == 0) return;

        var canvasWidth  = CarouselCanvas.ActualWidth;
        var canvasHeight = CarouselCanvas.ActualHeight;

        if (canvasWidth <= 0)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => UpdateLayout(animate));
            return;
        }
        if (canvasHeight <= 0) canvasHeight = 400;

        CarouselClip.Rect = new Windows.Foundation.Rect(0, 0, canvasWidth, canvasHeight);

        var peekWidth  = Math.Clamp(canvasWidth * PEEK_RATIO, PEEK_MIN, PEEK_MAX);
        var cardWidth  = canvasWidth - 2.0 * peekWidth;
        var cardHeight = canvasHeight;

        for (int i = 0; i < _cards.Count; i++)
        {
            var card     = _cards[i];
            var distance = i - _activeIndex;
            var isCenter = distance == 0;
            var isLeft   = distance == -1;
            var isRight  = distance == 1;

            double finalX, finalY, opacity, zIndex;
            var accent = _accents[i % _accents.Length];

            if (isCenter)
            {
                finalX = peekWidth; finalY = 0;
                opacity = 1.0; zIndex = 100;
                card.BorderBrush = new SolidColorBrush(accent);
            }
            else if (isLeft)
            {
                finalX = peekWidth - cardWidth - CARD_GAP; finalY = 0;
                opacity = 1.0; zIndex = 90;
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 42, 61, 88));
            }
            else if (isRight)
            {
                finalX = peekWidth + cardWidth + CARD_GAP; finalY = 0;
                opacity = 1.0; zIndex = 90;
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 42, 61, 88));
            }
            else
            {
                finalX = distance < 0 ? -(cardWidth + 40) : canvasWidth + 40;
                finalY = 0; opacity = 0.0; zIndex = 5;
                card.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }

            ApplyCardContent(card, isCenter, isLeft, isRight);
            card.IsHitTestVisible = Math.Abs(distance) <= 1;
            card.RenderTransform = null;

            if (animate)
                AnimateCard(card, finalX, finalY, cardWidth, cardHeight, opacity, zIndex);
            else
                ApplyCardTransform(card, finalX, finalY, cardWidth, cardHeight, opacity, zIndex);
        }
    }

    private static void ApplyCardContent(Border card, bool isCenter, bool isLeft, bool isRight)
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
                                    title.MaxWidth = isCenter ? 240 : 90;
                                    title.TextAlignment = isLeft ? TextAlignment.Right : TextAlignment.Left;
                                }
                            }
                        }
                    }
                    break;
                case "FieldsSection": fe.Visibility = Visibility.Visible; fe.Opacity = 1.0; break;
                case "BottomSection" when fe is Grid bottomGrid:
                    foreach (var gc in bottomGrid.Children)
                    {
                        if (gc is StackPanel centerStack)
                        {
                            foreach (var cc in centerStack.Children)
                            {
                                if (cc is TextBlock t) t.Opacity = 1.0;
                            }
                        }
                    }
                    break;
            }
        }
    }

    private static void AnimateCard(Border card, double x, double y,
        double width, double height, double opacity, double zIndex)
    {
        Canvas.SetZIndex(card, (int)zIndex);
        var sb       = new Storyboard();
        var duration = TimeSpan.FromMilliseconds(450);
        var easing   = new CubicEase { EasingMode = EasingMode.EaseInOut };

        void Anim(string prop, double to, bool dep = false)
        {
            var a = new DoubleAnimation
                { To = to, Duration = duration, EasingFunction = easing, EnableDependentAnimation = dep };
            Storyboard.SetTarget(a, card);
            Storyboard.SetTargetProperty(a, prop);
            sb.Children.Add(a);
        }

        Anim("(Canvas.Left)", x,      true);
        Anim("(Canvas.Top)",  y,      true);
        Anim("Width",         width,  true);
        Anim("Height",        height, true);
        Anim("Opacity",       opacity);
        sb.Begin();
    }

    private static void ApplyCardTransform(Border card, double x, double y,
        double width, double height, double opacity, double zIndex)
    {
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
        Canvas.SetZIndex(card, (int)zIndex);
        card.Width   = width;
        card.Height  = height;
        card.Opacity = opacity;
    }

    // ── Card Builder ──────────────────────────────────────────────────────────

    private Border BuildCard(PartTemplateDefinition template, TemplatesViewModel vm, int index,
        IReadOnlyList<MasterDataRowRecord>? records = null)
    {
        var isFav   = vm.IsFavouriteFor(template.Id);
        var accent  = _accents[index % _accents.Length];

        var card = new Border
        {
            Width = 300, Height = 400,
            CornerRadius    = new CornerRadius(24),
            Background      = new SolidColorBrush(Color.FromArgb(224, 20, 29, 42)),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(156, 104, 132, 168)),
            BorderThickness = new Thickness(1.5),
            Padding         = new Thickness(0),
            Translation     = new System.Numerics.Vector3(0, 0, 32),
        };
        card.Shadow = new ThemeShadow();
        card.Tag    = index;
        card.Tapped += (s, _) =>
        {
            if (s is Border b && b.Tag is int idx && idx != _activeIndex)
            {
                _activeIndex = idx;
                UpdateLayout(animate: true);
                UpdateNav();
            }
        };

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Glass top edge
        mainGrid.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(24, 24, 0, 0), Height = 1,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
            Opacity = 0.16, Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            Margin = new Thickness(1, 0, 1, 0),
        });

        // Row 0: centered title + top-right actions
        var topSection = new Grid
        {
            Name = "TopSection",
            Margin = new Thickness(20, 24, 20, 0),
        };
        topSection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
        topSection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topSection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });

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
            Glyph = "\uE8A5", FontSize = 20,
            Foreground = new SolidColorBrush(accent),
            VerticalAlignment = VerticalAlignment.Center,
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = template.Name, FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 234, 242, 255)),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 240,
        });
        Grid.SetColumn(titleStack, 1);
        topSection.Children.Add(titleStack);

        var buttonStack = new StackPanel
        {
            Name = "ButtonStack",
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        var starIcon = new FontIcon { Glyph = isFav ? "\uE735" : "\uE734", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)) };
        var starBtn = new Button
        {
            Width = 34, Height = 34, Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(10, 255, 193, 7)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 193, 7)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Content = starIcon,
        };
        ToolTipService.SetToolTip(starBtn, isFav ? "Remove from favourites" : "Add to favourites");
        starBtn.Click += async (_, _) =>
        {
            starBtn.IsEnabled = false;
            try
            {
                await vm.ToggleFavouritePublicAsync(template.Id);
                var nowFav = vm.IsFavouriteFor(template.Id);
                starIcon.Glyph = nowFav ? "\uE735" : "\uE734";
                ToolTipService.SetToolTip(starBtn, nowFav ? "Remove from favourites" : "Add to favourites");
            }
            finally { starBtn.IsEnabled = true; }
        };

        var editBtn = new Button
        {
            Width = 34, Height = 34, Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(28, 31, 122, 224)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 31, 122, 224)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Content = new FontIcon { Glyph = "\uE70F", FontSize = 14, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) },
        };
        ToolTipService.SetToolTip(editBtn, "Edit template");
        editBtn.Click += (_, _) => { vm.SelectedTemplate = template; vm.BeginEditSelectedTemplateCommand.Execute(null); };

        var deleteBtn = new Button
        {
            Width = 34, Height = 34, Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(14, 224, 82, 82)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(170, 224, 82, 82)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Content = new FontIcon { Glyph = "\uE74D", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(255, 224, 82, 82)) },
        };
        ToolTipService.SetToolTip(deleteBtn, "Delete template");
        deleteBtn.Click += async (_, _) =>
        {
            var xamlRoot = XamlRoot;
            if (xamlRoot is null) return;
            var dlg = new ContentDialog
            {
                Title = "Delete Template",
                Content = $"Are you sure you want to delete \"{template.Name}\"? This cannot be undone.",
                PrimaryButtonText = "Delete", CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close, XamlRoot = xamlRoot,
            };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            try
            {
                await vm.DeleteTemplateCommand.ExecuteAsync(template.Id);
                if (_cards.Contains(card))
                {
                    _cards.Remove(card);
                    _cardColOffset.Remove(card);
                    CarouselCanvas.Children.Remove(card);
                    if (_activeIndex >= _cards.Count && _activeIndex > 0) _activeIndex--;
                    UpdateLayout(animate: true);
                    UpdateNav();
                }
            }
            catch { /* ignore */ }
        };

        buttonStack.Children.Add(starBtn);
        buttonStack.Children.Add(editBtn);
        buttonStack.Children.Add(deleteBtn);
        Grid.SetColumn(buttonStack, 2);
        topSection.Children.Add(buttonStack);
        Grid.SetRow(topSection, 0);
        mainGrid.Children.Add(topSection);

        // Row 1: Unified table — headers + data in one Grid
        var fieldsSection = new StackPanel
        {
            Name = "FieldsSection", Spacing = 0,
            Margin = new Thickness(16, 16, 16, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };

        var fieldList   = template.Fields.OrderBy(f => f.DisplayOrder).ToList();
        var allRecords  = records ?? Array.Empty<MasterDataRowRecord>();

        var tableGrid = BuildTableGrid(fieldList, 0, accent, allRecords, allRecords.Count);
        tableGrid.Name = "TableGrid";

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

        // Row 2: [<]  [field count]  [>]
        var bottomGrid = new Grid
        {
            Name = "BottomSection",
            Margin = new Thickness(16, 0, 16, 20),
        };
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Center: field count only
        var centerStack = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        centerStack.Children.Add(new TextBlock
        {
            Text = $"{template.Fields.Count} column{(template.Fields.Count == 1 ? "" : "s")}",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 123, 141, 168)),
            HorizontalAlignment = HorizontalAlignment.Center, CharacterSpacing = 20,
        });
        Grid.SetColumn(centerStack, 1);
        bottomGrid.Children.Add(centerStack);

        // Col-nav arrows
        bool hasMoreCols = fieldList.Count > 4;

        var colPrevBtn = new Button
        {
            Name = "ColPrevBtn",
            Width = 32, Height = 32, Padding = new Thickness(0),
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Bottom,
            Visibility = hasMoreCols ? Visibility.Visible : Visibility.Collapsed,
            IsEnabled = false,
        };
        colPrevBtn.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        colPrevBtn.Resources["ButtonBackgroundPressed"]     = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
        colPrevBtn.Content = new FontIcon { Glyph = "\uE76B", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(255, 234, 242, 255)) };
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
            Visibility = hasMoreCols ? Visibility.Visible : Visibility.Collapsed,
            IsEnabled = hasMoreCols,
        };
        colNextBtn.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        colNextBtn.Resources["ButtonBackgroundPressed"]     = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
        colNextBtn.Content = new FontIcon { Glyph = "\uE76C", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(255, 234, 242, 255)) };
        Grid.SetColumn(colNextBtn, 2);
        bottomGrid.Children.Add(colNextBtn);

        colPrevBtn.Click += (_, _) => ShiftCardColumns(card, fieldList, accent, headersClip, colPrevBtn, colNextBtn, -1, allRecords, allRecords.Count);
        colNextBtn.Click += (_, _) => ShiftCardColumns(card, fieldList, accent, headersClip, colPrevBtn, colNextBtn, +1, allRecords, allRecords.Count);

        Grid.SetRow(bottomGrid, 2);
        mainGrid.Children.Add(bottomGrid);

        card.Child = mainGrid;
        _cardColOffset[card] = 0;
        return card;
    }

    // ── Column-scroll helpers ─────────────────────────────────────────────────

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
                Foreground = new SolidColorBrush(Color.FromArgb(255, 234, 242, 255)),
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

        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, ColumnSpacing = 8 };
        for (int c = 0; c < visibleCols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int fi = 0; fi < visibleCols; fi++)
        {
            var field = fields[offset + fi];

            var colBorder = new Border
            {
                BorderBrush     = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var colStack = new StackPanel { Spacing = 0 };

            // Header
            var headerCell = new Border
            {
                Background  = new SolidColorBrush(Color.FromArgb(50, accent.R, accent.G, accent.B)),
                Padding     = new Thickness(6, 12, 6, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var headerTypeIcon = field.Type switch
            {
                Models.TemplateFieldType.Text       => "\uE8D2", // Document
                Models.TemplateFieldType.Number     => "\uE8EF", // Number
                Models.TemplateFieldType.Decimal    => "\uEB50", // Decimal
                Models.TemplateFieldType.Date       => "\uE787", // Calendar
                Models.TemplateFieldType.Dropdown   => "\uE8FD", // List
                Models.TemplateFieldType.Boolean    => "\uE73E", // Checkmark
                Models.TemplateFieldType.RecordLink => "\uE71B", // Link
                _                                   => "\uE8D2",
            };

            // Icon left, label center, same vertical alignment
            var headerContent = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconEl = new FontIcon
            {
                Glyph = headerTypeIcon, FontSize = 14,
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

            // Spacer same width as icon to keep text truly centered
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

        // Build new unified table (headers + data aligned)
        var newGrid = BuildTableGrid(fields, newOffset, accent, records, maxRows);
        double clipWidth = headersClip.ActualWidth > 0 ? headersClip.ActualWidth : 300;
        double slideFrom = direction > 0 ? clipWidth : -clipWidth;
        newGrid.RenderTransform = new TranslateTransform { X = slideFrom };

        var oldChild = headersClip.Child as FrameworkElement;
        headersClip.Child = null;
        if (oldChild != null) oldChild.RenderTransform ??= new TranslateTransform();

        var container = new Grid();
        if (oldChild != null) container.Children.Add(oldChild);
        container.Children.Add(newGrid);
        headersClip.Child = container;

        var duration = TimeSpan.FromMilliseconds(300);
        var easing   = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var sb = new Storyboard();

        if (oldChild?.RenderTransform is TranslateTransform oldTt)
        {
            var outAnim = new DoubleAnimation { To = -slideFrom, Duration = duration, EasingFunction = easing, EnableDependentAnimation = true };
            Storyboard.SetTarget(outAnim, oldTt);
            Storyboard.SetTargetProperty(outAnim, "X");
            sb.Children.Add(outAnim);
        }
        if (newGrid.RenderTransform is TranslateTransform newTt)
        {
            var inAnim = new DoubleAnimation { From = slideFrom, To = 0, Duration = duration, EasingFunction = easing, EnableDependentAnimation = true };
            Storyboard.SetTarget(inAnim, newTt);
            Storyboard.SetTargetProperty(inAnim, "X");
            sb.Children.Add(inAnim);
        }

        sb.Completed += (_, _) =>
        {
            if (headersClip.Child is Grid cont && cont.Children.Contains(newGrid))
                cont.Children.Remove(newGrid);
            newGrid.RenderTransform = null;
            headersClip.Child = newGrid;
        };
        sb.Begin();
    }
}
