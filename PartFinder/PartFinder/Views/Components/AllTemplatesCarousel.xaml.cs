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

    private readonly Dictionary<Border, string> _cardTemplateIds = new();
    private readonly Dictionary<Border, IReadOnlyList<MasterDataRowRecord>> _cardRecords = new();
    private int _dataLoadGeneration;

    // Same peek constants as FavouritesSubPage
    private const double PEEK_RATIO = 0.13;
    private const double PEEK_MIN   = 60.0;
    private const double PEEK_MAX   = 130.0;
    private const double CARD_GAP   = 12.0;

    /// <summary>Data cells in the all-templates preview table (ID, BRAND, etc.).</summary>
    private const double DataRowMinHeight = 46;
    private const double DataRowFontSize = 13;
    /// <summary>Minimum column width before the preview switches to horizontal scrolling.</summary>
    private const double PreviewColumnMinWidth = 88;

    private static readonly Color[] _accents = new[]
    {
        Color.FromArgb(255, 94, 162, 255),
        Color.FromArgb(255, 176, 108, 255),
        Color.FromArgb(255, 88, 214, 255),
        Color.FromArgb(255, 244, 114, 182),
    };

    public event EventHandler<string>? TemplateDoubleTapped;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _cardFocusTimer;
    private int _pendingCardIndex = -1;

    public AllTemplatesCarousel()
    {
        InitializeComponent();
        _records = App.Services.GetRequiredService<IMasterDataRecordsService>();
    }

    private void QueueCardFocus(int index)
    {
        _pendingCardIndex = index;
        var queue = DispatcherQueue;
        _cardFocusTimer ??= queue.CreateTimer();
        _cardFocusTimer.Interval = TimeSpan.FromMilliseconds(280);
        _cardFocusTimer.IsRepeating = false;
        _cardFocusTimer.Tick -= OnCardFocusTimerTick;
        _cardFocusTimer.Tick += OnCardFocusTimerTick;
        _cardFocusTimer.Start();
    }

    private void OnCardFocusTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        if (_pendingCardIndex < 0 || _pendingCardIndex == _activeIndex)
        {
            return;
        }

        _activeIndex = _pendingCardIndex;
        _pendingCardIndex = -1;
        UpdateLayout(animate: true);
        UpdateNav();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async void Load(TemplatesViewModel vm)
    {
        _vm = vm;
        _activeIndex = 0;

        CarouselCanvas.Children.Clear();
        _cards.Clear();
        _cardTemplateIds.Clear();
        _cardRecords.Clear();
        var loadGeneration = ++_dataLoadGeneration;

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
            _ = LoadTemplateDataAsync(templates, loadGeneration);
        });
    }

    private async Task LoadTemplateDataAsync(List<PartTemplateDefinition> templates, int loadGeneration)
    {
        foreach (var template in templates)
        {
            if (loadGeneration != _dataLoadGeneration)
            {
                return;
            }

            IReadOnlyList<MasterDataRowRecord> rows;
            try
            {
                rows = await _records.GetRowsAsync(template.Id).ConfigureAwait(true);
            }
            catch
            {
                rows = Array.Empty<MasterDataRowRecord>();
            }

            if (loadGeneration != _dataLoadGeneration)
            {
                return;
            }

            var templateId = template.Id;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (loadGeneration != _dataLoadGeneration)
                {
                    return;
                }

                var card = FindCardForTemplate(templateId);
                if (card is not null)
                {
                    UpdateCardWithData(card, template, rows);
                }
            });
        }
    }

    private Border? FindCardForTemplate(string templateId)
    {
        foreach (var card in _cards)
        {
            if (_cardTemplateIds.TryGetValue(card, out var id)
                && string.Equals(id, templateId, StringComparison.Ordinal))
            {
                return card;
            }
        }

        return null;
    }

    private void UpdateCardWithData(Border card, PartTemplateDefinition template, IReadOnlyList<MasterDataRowRecord> rows)
    {
        if (!_cardTemplateIds.TryGetValue(card, out var cardTemplateId)
            || !string.Equals(cardTemplateId, template.Id, StringComparison.Ordinal))
        {
            return;
        }

        _cardRecords[card] = rows;
        if (card.Child is not Grid mainGrid) return;

        var index     = _cards.IndexOf(card);
        var accent    = _accents[index % _accents.Length];
        var fieldList = template.Fields.OrderBy(f => f.DisplayOrder).ToList();

        foreach (var child in mainGrid.Children)
        {
            if (child is Panel sp && sp.Name == "FieldsSection")
            {
                sp.Children.Clear();

                sp.Children.Add(CreateHorizontalTableHost(BuildTableGrid(fieldList, accent, rows)));
                break;
            }
        }
    }

    private static ScrollViewer CreateHorizontalTableHost(Grid tableGrid)
    {
        tableGrid.Name = "TableGrid";
        var scroll = new ScrollViewer
        {
            Name = "TableScroll",
            Content = tableGrid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ZoomMode = ZoomMode.Disabled,
        };
        scroll.Loaded += (_, _) => ApplyPreviewTableLayout(scroll, tableGrid);
        scroll.SizeChanged += (_, _) => ApplyPreviewTableLayout(scroll, tableGrid);
        return scroll;
    }

    private static void ApplyPreviewTableLayout(ScrollViewer scroll, Grid grid)
    {
        var viewport = scroll.ActualWidth;
        if (viewport <= 0 || grid.ColumnDefinitions.Count == 0)
        {
            return;
        }

        var colCount = grid.ColumnDefinitions.Count;
        var spacing = grid.ColumnSpacing * Math.Max(0, colCount - 1);
        var equalWidth = (viewport - spacing) / colCount;
        var useScroll = equalWidth < PreviewColumnMinWidth;

        if (useScroll)
        {
            scroll.HorizontalScrollMode = ScrollMode.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            grid.HorizontalAlignment = HorizontalAlignment.Left;
            grid.Width = colCount * PreviewColumnMinWidth + spacing;

            for (var i = 0; i < colCount; i++)
            {
                grid.ColumnDefinitions[i].Width = new GridLength(PreviewColumnMinWidth);
            }

            foreach (var child in grid.Children)
            {
                if (child is not Border colBorder)
                {
                    continue;
                }

                colBorder.Width = PreviewColumnMinWidth;
                colBorder.MinWidth = PreviewColumnMinWidth;
                colBorder.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }
        else
        {
            scroll.HorizontalScrollMode = ScrollMode.Disabled;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            grid.HorizontalAlignment = HorizontalAlignment.Stretch;
            grid.Width = viewport;

            for (var i = 0; i < colCount; i++)
            {
                grid.ColumnDefinitions[i].Width = new GridLength(1, GridUnitType.Star);
            }

            foreach (var child in grid.Children)
            {
                if (child is not Border colBorder)
                {
                    continue;
                }

                colBorder.ClearValue(FrameworkElement.WidthProperty);
                colBorder.ClearValue(FrameworkElement.MinWidthProperty);
                colBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
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

        // Multi-tier cascading fan layout (matching reference 1, 2, 3)
        // Center card is largest, side cards stagger outwards with decreasing height, width, and z-index
        var baseCenterWidth = Math.Clamp(canvasWidth * 0.42, 320.0, 560.0);
        var baseCenterHeight = Math.Max(260.0, canvasHeight - 20.0);
        var centerX = (canvasWidth - baseCenterWidth) / 2.0;

        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            var distance = i - _activeIndex;
            var absDist = Math.Abs(distance);
            var isCenter = distance == 0;
            var accent = _accents[i % _accents.Length];

            double cardW, cardH, finalX, finalY, opacity, zIndex;

            if (isCenter)
            {
                cardW = baseCenterWidth;
                cardH = baseCenterHeight;
                finalX = centerX;
                finalY = (canvasHeight - cardH) / 2.0;
                opacity = 1.0;
                zIndex = 100;
                card.BorderBrush = new SolidColorBrush(accent);
            }
            else if (absDist <= 4)
            {
                // Each step outward reduces scale and offsets horizontally in a cascading fan
                var scaleFactor = Math.Pow(0.88, absDist);
                cardW = baseCenterWidth * scaleFactor;
                cardH = baseCenterHeight * scaleFactor;
                finalY = (canvasHeight - cardH) / 2.0;

                // Horizontal step offset: allows cards behind to partially peek out
                double stepOffset = 0.0;
                for (int s = 1; s <= absDist; s++)
                {
                    stepOffset += (baseCenterWidth * Math.Pow(0.88, s - 1)) * 0.32;
                }

                if (distance < 0)
                {
                    // Left flank
                    finalX = centerX - stepOffset;
                }
                else
                {
                    // Right flank
                    finalX = centerX + baseCenterWidth - (cardW * 0.68) + (stepOffset - (baseCenterWidth * 0.32));
                }

                opacity = 1.0;
                zIndex = 100 - (absDist * 10);
                card.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(160 - absDist * 25), 42, 61, 88));
            }
            else
            {
                cardW = baseCenterWidth * 0.6;
                cardH = baseCenterHeight * 0.6;
                finalX = distance < 0 ? -cardW - 100 : canvasWidth + 100;
                finalY = (canvasHeight - cardH) / 2.0;
                opacity = 0.0;
                zIndex = 5;
                card.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }

            ApplyCardContent(card, isCenter, distance < 0, distance > 0);
            card.IsHitTestVisible = absDist <= 3;
            card.RenderTransform = null;

            if (animate)
                AnimateCard(card, finalX, finalY, cardW, cardH, opacity, zIndex);
            else
                ApplyCardTransform(card, finalX, finalY, cardW, cardH, opacity, zIndex);

            RefreshPreviewTableLayoutForCard(card);
        }
    }

    private static void RefreshPreviewTableLayoutForCard(Border card)
    {
        if (card.Child is not Grid mainGrid)
        {
            return;
        }

        foreach (var child in mainGrid.Children)
        {
            if (child is not Panel { Name: "FieldsSection" } fieldsSection)
            {
                continue;
            }

            foreach (var host in fieldsSection.Children)
            {
                if (host is ScrollViewer scroll && scroll.Content is Grid grid)
                {
                    ApplyPreviewTableLayout(scroll, grid);
                }
            }
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
        sb.Completed += (_, _) => RefreshPreviewTableLayoutForCard(card);
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
            Background      = new SolidColorBrush(Color.FromArgb(255, 20, 29, 42)),
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
                    QueueCardFocus(idx);
                }
            };
            card.DoubleTapped += (_, args) =>
            {
                args.Handled = true;
                _cardFocusTimer?.Stop();
                _pendingCardIndex = -1;
                TemplateDoubleTapped?.Invoke(this, template.Id);
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
                    _cardTemplateIds.Remove(card);
                    _cardRecords.Remove(card);
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
        var fieldsSection = new Grid
        {
            Name = "FieldsSection",
            Margin = new Thickness(16, 16, 16, 0),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var fieldList   = template.Fields.OrderBy(f => f.DisplayOrder).ToList();
        var allRecords  = records ?? Array.Empty<MasterDataRowRecord>();

        fieldsSection.Children.Add(CreateHorizontalTableHost(BuildTableGrid(fieldList, accent, allRecords)));

        Grid.SetRow(fieldsSection, 1);
        mainGrid.Children.Add(fieldsSection);

        // Row 2: row / column summary
        var bottomGrid = new Grid
        {
            Name = "BottomSection",
            Margin = new Thickness(16, 8, 16, 20),
        };
        var rowCount = allRecords.Count;
        bottomGrid.Children.Add(new TextBlock
        {
            Text = rowCount > 0
                ? $"{rowCount} row{(rowCount == 1 ? "" : "s")} · {template.Fields.Count} column{(template.Fields.Count == 1 ? "" : "s")}"
                : $"{template.Fields.Count} column{(template.Fields.Count == 1 ? "" : "s")}",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 123, 141, 168)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CharacterSpacing = 20,
        });

        Grid.SetRow(bottomGrid, 2);
        mainGrid.Children.Add(bottomGrid);

        card.Child = mainGrid;
        _cardTemplateIds[card] = template.Id;
        _cardRecords[card] = allRecords;
        return card;
    }

    // ── Preview table ─────────────────────────────────────────────────────────

    private Grid BuildTableGrid(
        List<TemplateFieldDefinition> fields,
        Color accent,
        IReadOnlyList<MasterDataRowRecord> records)
    {
        var colCount = fields.Count;
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = 8,
        };
        for (int c = 0; c < colCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (int fi = 0; fi < colCount; fi++)
        {
            var field = fields[fi];

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
                Padding     = new Thickness(10, 14, 10, 14),
                MinHeight   = 48,
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

            var headerLabel = field.Label;
            var labelEl = new TextBlock
            {
                Text = headerLabel.ToUpperInvariant(),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1,
            };
            ToolTipService.SetToolTip(labelEl, headerLabel);
            ToolTipService.SetToolTip(headerCell, headerLabel);
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
                    Padding     = new Thickness(10, 14, 10, 14),
                    MinHeight   = DataRowMinHeight,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Child = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(cellValue) ? "—" : cellValue,
                        FontSize = DataRowFontSize,
                        LineHeight = 20,
                        Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 2,
                    },
                });
            }

            colStack.Children.Add(dataStack);

            colBorder.Child = colStack;
            Grid.SetColumn(colBorder, fi);
            grid.Children.Add(colBorder);
        }

        return grid;
    }
}
