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

    // Card sizing for carousel effect — scaled down to fit standard screens
    private const double CENTER_CARD_WIDTH = 260.0;
    private const double CENTER_CARD_HEIGHT = 380.0;
    private const double ADJACENT_CARD_WIDTH = 200.0;
    private const double ADJACENT_CARD_HEIGHT = 300.0;
    private const double OUTER_CARD_WIDTH = 140.0;
    private const double OUTER_CARD_HEIGHT = 220.0;
    private const double FAR_OUTER_CARD_WIDTH = 90.0;
    private const double FAR_OUTER_CARD_HEIGHT = 150.0;

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
        // View model is assigned in ShowAsync, so we just setup UI events here


        // Update layout when canvas size changes
        CarouselCanvas.SizeChanged += (s, e) => UpdateCarouselLayout(animate: false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.EditTemplateRequested -= OnEditTemplateRequested;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FavouritesViewModel.ActiveIndex))
        {
            UpdateCarouselLayout(animate: true);
        }
        else if (e.PropertyName == nameof(FavouritesViewModel.FavouriteTemplates))
        {
            BuildCarouselCards();
            UpdateCarouselLayout(animate: false);
        }
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
        // Get accent color for this card based on position
        var (accentColor, accentColorAlt) = GetCardAccentColors(number);

        // ── Outer glow border (the glowing neon effect) ──
        var card = new Border
        {
            Width = CENTER_CARD_WIDTH,
            Height = CENTER_CARD_HEIGHT,
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(_cardBg),
            BorderBrush = new SolidColorBrush(Color.FromArgb(100, _borderDefault.R, _borderDefault.G, _borderDefault.B)),
            BorderThickness = new Thickness(1.5),
            Tag = template,
            Padding = new Thickness(0),
            Translation = new System.Numerics.Vector3(0, 0, 32),
        };

        // Add ThemeShadow for depth
        card.Shadow = new ThemeShadow();

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Glassmorphism top edge highlight ──
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

        // ── Top Section: Icon & Title label ──
        var topSection = new StackPanel
        {
            Name = "TopSection",
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 40, 0, 0),
        };

        // Add a nice icon based on template
        var icon = new FontIcon
        {
            Glyph = GetIconForTemplate(number),
            FontSize = 48,
            Foreground = new SolidColorBrush(accentColor),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        topSection.Children.Add(icon);

        var titleLabel = new TextBlock
        {
            Text = template.Name,
            FontSize = 20, // Larger font
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 135, 206, 250)), // Light Blue (LightSkyBlue)
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220,
        };
        topSection.Children.Add(titleLabel);

        Grid.SetRow(topSection, 0);
        mainGrid.Children.Add(topSection);

        // ── Center Content: Empty (pushes bottom to end) ──
        var centerContent = new Grid();
        Grid.SetRow(centerContent, 1);
        mainGrid.Children.Add(centerContent);

        // ── Bottom Section: Field count + Action buttons ──
        var bottomSection = new StackPanel
        {
            Name = "BottomSection",
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 24),
        };

        // Field count label
        var fieldText = new TextBlock
        {
            Text = template.FieldCountLabel,
            FontSize = 13,
            Foreground = new SolidColorBrush(_textTertiary),
            HorizontalAlignment = HorizontalAlignment.Center,
            CharacterSpacing = 20,
        };
        bottomSection.Children.Add(fieldText);

        // ── Action Buttons (shown/hidden based on center selection) ──
        // We will toggle visibility of this entire stack in UpdateCarouselLayout
        var buttonStack = new StackPanel
        {
            Name = "ButtonStack",
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed, // Initially hidden
        };

        // Edit button — accent-filled
        var editBtn = new Button
        {
            Width = 100,
            Height = 38,
            Background = new SolidColorBrush(_accentPrimary),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Tag = template,
        };
        editBtn.Click += OnCardEditClick;
        var editContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        editContent.Children.Add(new FontIcon { Glyph = "\uE70F", FontSize = 14, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
        editContent.Children.Add(new TextBlock { Text = "Edit", FontSize = 14, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        editBtn.Content = editContent;

        // Unstar button — golden outlined
        var unstarBtn = new Button
        {
            Width = 100,
            Height = 38,
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)), // Golden border
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Tag = template,
        };
        unstarBtn.Click += OnCardUnstarClick;
        var unstarContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        unstarContent.Children.Add(new FontIcon { Glyph = "\uE735", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)) }); // Filled star
        unstarContent.Children.Add(new TextBlock { Text = "Unstar", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        unstarBtn.Content = unstarContent;

        buttonStack.Children.Add(editBtn);
        buttonStack.Children.Add(unstarBtn);
        bottomSection.Children.Add(buttonStack);

        Grid.SetRow(bottomSection, 2);
        mainGrid.Children.Add(bottomSection);

        card.Child = mainGrid;

        // Click handler for card selection
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

        // If canvas hasn't measured yet, try forcing it
        if (canvasWidth <= 0)
        {
            CarouselCanvas.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            canvasWidth = CarouselCanvas.DesiredSize.Width;
            canvasHeight = CarouselCanvas.DesiredSize.Height;
            if (canvasWidth <= 0)
            {
                // Schedule a retry
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    UpdateCarouselLayout(animate);
                });
                return;
            }
        }

        if (canvasHeight <= 0) canvasHeight = 400; // Fallback height

        var activeIndex = _vm.ActiveIndex;
        var centerX = canvasWidth / 2.0;

        // Calculate spacing relative to canvas width for responsive layout (increased gaps)
        var adjacentSpacing = Math.Min(canvasWidth * 0.32, 280);
        var outerSpacing = Math.Min(canvasWidth * 0.44, 400);
        var farOuterSpacing = Math.Min(canvasWidth * 0.52, 500);

        for (int i = 0; i < _cardElements.Count; i++)
        {
            var card = _cardElements[i];
            var distance = i - activeIndex;
            var isCenter = Math.Abs(distance) == 0;

            double width, height, opacity, offsetX, zIndex;
            double scaleX, scaleY;
            byte borderAlpha;
            Color glowColor;

            // Get the card's accent colors
            var (accentColor, _) = GetCardAccentColors(i + 1);

            // Calculate properties based on distance from center
            switch (Math.Abs(distance))
            {
                case 0: // Center card — full size, bright glow border
                    width = CENTER_CARD_WIDTH;
                    height = CENTER_CARD_HEIGHT;
                    opacity = 1.0;
                    offsetX = 0;
                    zIndex = 100;
                    scaleX = 1.0;
                    scaleY = 1.0;
                    borderAlpha = 200;
                    glowColor = accentColor;
                    break;

                case 1: // Adjacent cards — medium, subtle border
                    width = ADJACENT_CARD_WIDTH;
                    height = ADJACENT_CARD_HEIGHT;
                    opacity = 0.75;
                    offsetX = distance > 0 ? adjacentSpacing : -adjacentSpacing;
                    zIndex = 50;
                    scaleX = 0.95;
                    scaleY = 0.95;
                    borderAlpha = 60;
                    glowColor = _borderDefault;
                    break;

                case 2: // Second outer cards
                    width = OUTER_CARD_WIDTH;
                    height = OUTER_CARD_HEIGHT;
                    opacity = 0.3;
                    offsetX = distance > 0 ? outerSpacing : -outerSpacing;
                    zIndex = 20;
                    scaleX = 0.9;
                    scaleY = 0.9;
                    borderAlpha = 40;
                    glowColor = _borderDefault;
                    break;

                default: // Far outer cards — completely hidden (max 5 visible cards)
                    width = FAR_OUTER_CARD_WIDTH;
                    height = FAR_OUTER_CARD_HEIGHT;
                    opacity = 0.0;
                    offsetX = distance > 0 ? farOuterSpacing : -farOuterSpacing;
                    zIndex = 5;
                    scaleX = 0.85;
                    scaleY = 0.85;
                    borderAlpha = 0;
                    glowColor = _borderDefault;
                    break;
            }

            var finalX = centerX + offsetX - (width / 2.0);
            var finalY = (canvasHeight - height) / 2.0; // Center vertically

            // Show/hide buttons based on selection and fade out inactive text
            if (card.Child is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is FrameworkElement fe)
                    {
                        if (fe.Name == "TopSection")
                        {
                            fe.Opacity = isCenter ? 1.0 : 0.15; // Fade out icon and title for side cards
                        }
                        else if (fe.Name == "BottomSection" && fe is StackPanel bottomSection)
                        {
                            foreach (var bChild in bottomSection.Children)
                            {
                                if (bChild is TextBlock txt) // Field count
                                {
                                    txt.Opacity = isCenter ? 1.0 : 0.15;
                                }
                                else if (bChild is StackPanel stack && stack.Name == "ButtonStack")
                                {
                                    stack.Visibility = isCenter ? Visibility.Visible : Visibility.Collapsed;
                                }
                            }
                        }
                    }
                }
            }

            card.IsHitTestVisible = opacity > 0;

            // Apply border glow effect — center card gets neon accent glow, others get subtle border
            card.BorderBrush = new SolidColorBrush(
                Color.FromArgb(borderAlpha, glowColor.R, glowColor.G, glowColor.B));

            // Apply scale transform for depth perspective
            card.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            card.RenderTransform = new CompositeTransform
            {
                ScaleX = scaleX,
                ScaleY = scaleY,
            };

            if (animate)
            {
                AnimateCard(card, finalX, finalY, width, height, opacity, zIndex);
            }
            else
            {
                ApplyCardTransform(card, finalX, finalY, width, height, opacity, zIndex);
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
        _templatesVm?.BeginEditSelectedTemplateCommand.Execute(null);
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
