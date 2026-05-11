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

    // Peek width: how much of a side card is visible on each edge
    // Center card fills the rest: canvasWidth - 2 * PEEK_WIDTH
    private const double PEEK_WIDTH_MIN = 60.0;
    private const double PEEK_WIDTH_MAX = 130.0;
    private const double PEEK_WIDTH_RATIO = 0.13; // 13 % of canvas width

    // Theme colors — pulled from Colors.xaml
    private static readonly Color _cardBg = Color.FromArgb(255, 17, 26, 38);         // #111A26
    private static readonly Color _elevatedBg = Color.FromArgb(255, 23, 35, 52);     // #172334
    private static readonly Color _accentPrimary = Color.FromArgb(255, 31, 122, 224); // #1F7AE0
    private static readonly Color _borderDefault = Color.FromArgb(255, 42, 61, 88);  // #2A3D58
    private static readonly Color _textPrimary = Color.FromArgb(255, 234, 242, 255);  // #EAF2FF
    private static readonly Color _textSecondary = Color.FromArgb(255, 170, 184, 202);// #AAB8CA
    private static readonly Color _textTertiary = Color.FromArgb(255, 123, 141, 168); // #7B8DA8

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

        for (int i = 0; i < _vm.FavouriteTemplates.Count; i++)
        {
            var template = _vm.FavouriteTemplates[i];
            var card = CreateTemplateCard(template, i + 1);
            _cardElements.Add(card);
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
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(_cardBg),
            BorderBrush = new SolidColorBrush(Color.FromArgb(100, _borderDefault.R, _borderDefault.G, _borderDefault.B)),
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
            CornerRadius = new CornerRadius(20, 20, 0, 0),
            Height = 1,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Opacity = 0.08,
            Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            Margin = new Thickness(1, 0, 1, 0),
        };
        mainGrid.Children.Add(glassEdge);

        // ── Row 0: small template name top-left ──
        var topSection = new Grid
        {
            Name = "TopSection",
            Margin = new Thickness(24, 18, 24, 0),
        };

        var nameLabel = new TextBlock
        {
            Text = template.Name,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(_textSecondary),
            HorizontalAlignment = HorizontalAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220,
        };
        topSection.Children.Add(nameLabel);

        Grid.SetRow(topSection, 0);
        mainGrid.Children.Add(topSection);

        // ── Row 1: Fields preview — only shown on focused card ──
        var fieldsSection = new StackPanel
        {
            Name = "FieldsSection",
            Spacing = 10,
            Margin = new Thickness(24, 20, 24, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };

        var previewScroller = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            ZoomMode = ZoomMode.Disabled,
        };

        var previewRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
        };

        const double previewColumnWidth = 108;
        const double previewColumnHeight = 62;
        const double previewOverflowWidth = 84;

        var dividerBrush = new SolidColorBrush(Color.FromArgb(132, _borderDefault.R, _borderDefault.G, _borderDefault.B));
        var previewSurface = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)),
            BorderBrush = dividerBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
            Child = previewRow,
        };

        var fields = template.Template.Fields
            .OrderBy(f => f.DisplayOrder)
            .Take(6)
            .ToList();

        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var typeCaption = field.Type switch
            {
                Models.TemplateFieldType.Number => "Number",
                Models.TemplateFieldType.Decimal => "Decimal",
                Models.TemplateFieldType.Date => "Date",
                Models.TemplateFieldType.Boolean => "Yes / No",
                Models.TemplateFieldType.Dropdown => "Dropdown",
                Models.TemplateFieldType.RecordLink => "Link",
                _ => "Text",
            };

            var columnCell = new Border
            {
                Width = previewColumnWidth,
                Height = previewColumnHeight,
                Background = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255)),
                BorderBrush = dividerBrush,
                BorderThickness = new Thickness(0, 0, i == fields.Count - 1 && template.Template.Fields.Count <= 6 ? 0 : 1, 0),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0),
            };

            var cellGrid = new Grid();
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerBand = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)),
                BorderBrush = dividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 6, 8, 5),
                Child = new TextBlock
                {
                    Text = field.Label,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(_textPrimary),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = previewColumnWidth - 16,
                },
            };

            var cellContent = new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(8, 5, 8, 6),
                VerticalAlignment = VerticalAlignment.Top,
            };
            cellContent.Children.Add(new TextBlock
            {
                FontSize = 10,
                Text = typeCaption,
                Foreground = new SolidColorBrush(_textSecondary),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = previewColumnWidth - 16,
            });
            if (field.IsRequired)
            {
                cellContent.Children.Add(new TextBlock
                {
                    Text = "Required",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 224, 82, 82)),
                });
            }

            cellGrid.Children.Add(headerBand);
            Grid.SetRow(cellContent, 1);
            cellGrid.Children.Add(cellContent);

            columnCell.Child = cellGrid;
            previewRow.Children.Add(columnCell);
        }

        if (template.Template.Fields.Count > 6)
        {
            previewRow.Children.Add(new Border
            {
                Width = previewOverflowWidth,
                Height = previewColumnHeight,
                Background = new SolidColorBrush(Color.FromArgb(12, 31, 122, 224)),
                BorderBrush = dividerBrush,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0),
                Child = new TextBlock
                {
                    Text = $"+{template.Template.Fields.Count - 6} more",
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(_accentPrimary),
                    VerticalAlignment = VerticalAlignment.Center,
                }
            });
        }

        previewScroller.Content = previewSurface;
        fieldsSection.Children.Add(previewScroller);

        Grid.SetRow(fieldsSection, 1);
        mainGrid.Children.Add(fieldsSection);

        // ── Row 2: Bottom — field count + action buttons ──
        var bottomSection = new StackPanel
        {
            Name = "BottomSection",
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 32),
        };

        var fieldText = new TextBlock
        {
            Text = template.FieldCountLabel,
            FontSize = 14,
            Foreground = new SolidColorBrush(_textTertiary),
            HorizontalAlignment = HorizontalAlignment.Center,
            CharacterSpacing = 20,
        };
        bottomSection.Children.Add(fieldText);

        var buttonStack = new StackPanel
        {
            Name = "ButtonStack",
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        var editBtn = new Button
        {
            Width = 100, Height = 36,
            Background = new SolidColorBrush(_accentPrimary),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Tag = template,
        };
        editBtn.Click += OnCardEditClick;
        var editContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        editContent.Children.Add(new FontIcon { Glyph = "\uE70F", FontSize = 13, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
        editContent.Children.Add(new TextBlock { Text = "Edit", FontSize = 13, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        editBtn.Content = editContent;

        var unstarBtn = new Button
        {
            Width = 100, Height = 36,
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8),
            Tag = template,
        };
        unstarBtn.Click += OnCardUnstarClick;
        var unstarContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        unstarContent.Children.Add(new FontIcon { Glyph = "\uE735", FontSize = 13, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)) });
        unstarContent.Children.Add(new TextBlock { Text = "Unstar", FontSize = 13, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        unstarBtn.Content = unstarContent;

        buttonStack.Children.Add(editBtn);
        buttonStack.Children.Add(unstarBtn);
        bottomSection.Children.Add(buttonStack);

        Grid.SetRow(bottomSection, 2);
        mainGrid.Children.Add(bottomSection);

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
            ApplyCardContentVisibility(card, isCenter);

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
    private static void ApplyCardContentVisibility(Border card, bool isCenter)
    {
        if (card.Child is not Grid grid) return;

        foreach (var child in grid.Children)
        {
            if (child is not FrameworkElement fe) continue;

            switch (fe.Name)
            {
                case "TopSection":
                    fe.Opacity = 1.0; // always fully visible
                    break;
                case "FieldsSection":
                    fe.Visibility = Visibility.Visible; // always show columns
                    fe.Opacity = 1.0;
                    break;
                case "BottomSection" when fe is StackPanel bottomSection:
                    foreach (var bChild in bottomSection.Children)
                    {
                        if (bChild is TextBlock txt)
                            txt.Opacity = 1.0;
                        else if (bChild is StackPanel stack && stack.Name == "ButtonStack")
                            // Edit/Unstar buttons only on focused card
                            stack.Visibility = isCenter ? Visibility.Visible : Visibility.Collapsed;
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
