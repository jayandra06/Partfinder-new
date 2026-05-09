using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.Models;
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

    // Same peek constants as FavouritesSubPage
    private const double PEEK_RATIO = 0.13;
    private const double PEEK_MIN   = 60.0;
    private const double PEEK_MAX   = 130.0;
    private const double CARD_GAP   = 12.0;

    private static readonly Color[] _accents = new[]
    {
        Color.FromArgb(255, 31,  122, 224),
        Color.FromArgb(255, 168, 85,  247),
        Color.FromArgb(255, 56,  189, 248),
        Color.FromArgb(255, 236, 72,  153),
    };

    public AllTemplatesCarousel()
    {
        InitializeComponent();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Load(TemplatesViewModel vm)
    {
        _vm = vm;
        _activeIndex = 0;

        CarouselCanvas.Children.Clear();
        _cards.Clear();

        var templates = vm.Templates.ToList();
        for (int i = 0; i < templates.Count; i++)
        {
            var card = BuildCard(templates[i], vm, i);
            _cards.Add(card);
            CarouselCanvas.Children.Add(card);
        }

        UpdateNav();
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
        {
            await Task.Delay(50);
            UpdateLayout(animate: false);
        });
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

            ApplyCardContent(card, isCenter);
            card.IsHitTestVisible = Math.Abs(distance) <= 1;
            card.RenderTransform = null;

            if (animate)
                AnimateCard(card, finalX, finalY, cardWidth, cardHeight, opacity, zIndex);
            else
                ApplyCardTransform(card, finalX, finalY, cardWidth, cardHeight, opacity, zIndex);
        }
    }

    private static void ApplyCardContent(Border card, bool isCenter)
    {
        if (card.Child is not Grid grid) return;
        foreach (var child in grid.Children)
        {
            if (child is not FrameworkElement fe) continue;
            switch (fe.Name)
            {
                case "TopSection":    fe.Opacity = 1.0; break;
                case "FieldsSection": fe.Visibility = Visibility.Visible; fe.Opacity = 1.0; break;
                case "BottomSection" when fe is StackPanel bs:
                    foreach (var bc in bs.Children)
                    {
                        if (bc is TextBlock t) t.Opacity = 1.0;
                        else if (bc is StackPanel s && s.Name == "ButtonStack")
                            s.Visibility = isCenter ? Visibility.Visible : Visibility.Collapsed;
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

    private Border BuildCard(PartTemplateDefinition template, TemplatesViewModel vm, int index)
    {
        var isFav   = vm.IsFavouriteFor(template.Id);
        var accent  = _accents[index % _accents.Length];

        var card = new Border
        {
            Width = 300, Height = 400,
            CornerRadius    = new CornerRadius(20),
            Background      = new SolidColorBrush(Color.FromArgb(255, 17, 26, 38)),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(160, 42, 61, 88)),
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
            CornerRadius = new CornerRadius(20, 20, 0, 0), Height = 1,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
            Opacity = 0.08, Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            Margin = new Thickness(1, 0, 1, 0),
        });

        // Row 0: icon + name
        var topSection = new StackPanel
        {
            Name = "TopSection", Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(20, 60, 20, 0),
        };
        topSection.Children.Add(new FontIcon
        {
            Glyph = "\uE8A5", FontSize = 52,
            Foreground = new SolidColorBrush(accent),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        topSection.Children.Add(new TextBlock
        {
            Text = template.Name, FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 234, 242, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap, MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 300,
        });
        Grid.SetRow(topSection, 0);
        mainGrid.Children.Add(topSection);

        // Row 1: field chips
        var fieldsSection = new StackPanel
        {
            Name = "FieldsSection", Spacing = 8,
            Margin = new Thickness(24, 20, 24, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        foreach (var field in template.Fields.OrderBy(f => f.DisplayOrder).Take(6))
        {
            var typeIcon = field.Type switch
            {
                Models.TemplateFieldType.Number     => "\uE8EF",
                Models.TemplateFieldType.Date       => "\uE787",
                Models.TemplateFieldType.Dropdown   => "\uE70D",
                Models.TemplateFieldType.RecordLink => "\uE71B",
                _                                   => "\uE8D2",
            };
            var chip = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(60, 42, 61, 88)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(8, 4, 8, 4),
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new FontIcon
            {
                Glyph = typeIcon, FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 123, 141, 168)),
                VerticalAlignment = VerticalAlignment.Center,
            });
            row.Children.Add(new TextBlock
            {
                Text = field.Label, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 170, 184, 202)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 200,
            });
            if (field.IsRequired)
                row.Children.Add(new TextBlock
                {
                    Text = "*", FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 224, 82, 82)),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            chip.Child = row;
            fieldsSection.Children.Add(chip);
        }
        if (template.Fields.Count > 6)
            fieldsSection.Children.Add(new TextBlock
            {
                Text = $"+{template.Fields.Count - 6} more fields", FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 123, 141, 168)),
                Margin = new Thickness(4, 2, 0, 0),
            });
        Grid.SetRow(fieldsSection, 1);
        mainGrid.Children.Add(fieldsSection);

        // Row 2: column count + 3 buttons
        var bottomSection = new StackPanel
        {
            Name = "BottomSection", Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20),
        };
        bottomSection.Children.Add(new TextBlock
        {
            Text = $"{template.Fields.Count} column{(template.Fields.Count == 1 ? "" : "s")}",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 123, 141, 168)),
            HorizontalAlignment = HorizontalAlignment.Center, CharacterSpacing = 20,
        });

        var buttonStack = new StackPanel
        {
            Name = "ButtonStack", Orientation = Orientation.Horizontal,
            Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        // Star button
        var starIcon = new FontIcon
        {
            Glyph = isFav ? "\uE735" : "\uE734", FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)),
        };
        var starBtn = new Button
        {
            Width = 90, Height = 36,
            Background      = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)),
            BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(8),
        };
        var starContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        starContent.Children.Add(starIcon);
        starContent.Children.Add(new TextBlock
        {
            Text = isFav ? "Unstar" : "Star", FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        starBtn.Content = starContent;
        starBtn.Click += async (_, _) =>
        {
            await vm.ToggleFavouritePublicAsync(template.Id);
            var nowFav = vm.IsFavouriteFor(template.Id);
            starIcon.Glyph = nowFav ? "\uE735" : "\uE734";
            if (starContent.Children[1] is TextBlock lbl) lbl.Text = nowFav ? "Unstar" : "Star";
        };

        // Edit button
        var editBtn = new Button
        {
            Width = 90, Height = 36,
            Background      = new SolidColorBrush(Color.FromArgb(255, 31, 122, 224)),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(8),
        };
        var editContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        editContent.Children.Add(new FontIcon { Glyph = "\uE70F", FontSize = 13, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
        editContent.Children.Add(new TextBlock { Text = "Edit", FontSize = 13, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        editBtn.Content = editContent;
        editBtn.Click += (_, _) =>
        {
            vm.SelectedTemplate = template;
            vm.BeginEditSelectedTemplateCommand.Execute(null);
        };

        // Delete button
        var deleteBtn = new Button
        {
            Width = 90, Height = 36,
            Background      = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 224, 82, 82)),
            BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(8),
        };
        var deleteContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        deleteContent.Children.Add(new FontIcon { Glyph = "\uE74D", FontSize = 13, Foreground = new SolidColorBrush(Color.FromArgb(255, 224, 82, 82)) });
        deleteContent.Children.Add(new TextBlock { Text = "Delete", FontSize = 13, Foreground = new SolidColorBrush(Color.FromArgb(255, 224, 82, 82)), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        deleteBtn.Content = deleteContent;
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
                Load(vm); // rebuild carousel after delete
            }
            catch { /* ignore */ }
        };

        buttonStack.Children.Add(starBtn);
        buttonStack.Children.Add(editBtn);
        buttonStack.Children.Add(deleteBtn);
        bottomSection.Children.Add(buttonStack);
        Grid.SetRow(bottomSection, 2);
        mainGrid.Children.Add(bottomSection);

        card.Child = mainGrid;
        return card;
    }
}
