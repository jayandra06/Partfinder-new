using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using PartFinder.Helpers;
using PartFinder.Models;
using PartFinder.ViewModels;
using System.ComponentModel;
using System.Numerics;

namespace PartFinder.Views.Pages;

public sealed partial class TemplatesPage : Page
{
    private TemplatesViewModel? _boundVm;
    private bool _isCarouselAnimating;

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

    private async void OnPreviousTemplateClick(object sender, RoutedEventArgs e)
    {
        await SwitchCarouselAsync(isNext: false).ConfigureAwait(true);
    }

    private async void OnNextTemplateClick(object sender, RoutedEventArgs e)
    {
        await SwitchCarouselAsync(isNext: true).ConfigureAwait(true);
    }

    private async void OnLeftCarouselCardTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        await SwitchCarouselAsync(isNext: false).ConfigureAwait(true);
    }

    private async void OnRightCarouselCardTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        await SwitchCarouselAsync(isNext: true).ConfigureAwait(true);
    }

    private async Task SwitchCarouselAsync(bool isNext)
    {
        if (_isCarouselAnimating || DataContext is not TemplatesViewModel vm || vm.Templates.Count == 0)
        {
            return;
        }

        _isCarouselAnimating = true;
        try
        {
            if (vm.SelectedTemplate is null)
            {
                vm.SelectedTemplate = vm.Templates.FirstOrDefault();
            }
            else
            {
                var currentIndex = vm.Templates.IndexOf(vm.SelectedTemplate);
                if (currentIndex >= 0)
                {
                    var newIndex = isNext
                        ? (currentIndex + 1) % vm.Templates.Count
                        : (currentIndex - 1 + vm.Templates.Count) % vm.Templates.Count;
                    vm.SelectedTemplate = vm.Templates[newIndex];
                }
            }
            // Keep only deterministic template switching to prevent stage drift.
            await Task.Delay(90).ConfigureAwait(true);
        }
        finally
        {
            _isCarouselAnimating = false;
        }
    }

    private async void OnTemplatesPageKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Left)
        {
            e.Handled = true;
            await SwitchCarouselAsync(isNext: false).ConfigureAwait(true);
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Right)
        {
            e.Handled = true;
            await SwitchCarouselAsync(isNext: true).ConfigureAwait(true);
        }
    }

    private void RunCarouselOutTransition(bool isNext)
    {
        if (CenterCarouselCard is null || LeftCarouselCard is null || RightCarouselCard is null)
        {
            return;
        }

        AnimateCard(CenterCarouselCard, isNext ? -64f : 64f, -7f, 0.76f, 0.915f, isNext ? -8f : 8f, 190, 0);
        AnimateCard(LeftCarouselCard, isNext ? -36f : 20f, 6f, 0.58f, 0.915f, isNext ? -12f : -6f, 200, 24);
        AnimateCard(RightCarouselCard, isNext ? -20f : 36f, 6f, 0.58f, 0.915f, isNext ? 6f : 12f, 200, 44);

        if (LeftFarCarouselCard is not null && RightFarCarouselCard is not null)
        {
            AnimateCard(LeftFarCarouselCard, isNext ? -16f : 8f, 5f, 0.26f, 0.92f, isNext ? -18f : -12f, 205, 30);
            AnimateCard(RightFarCarouselCard, isNext ? -8f : 16f, 5f, 0.26f, 0.92f, isNext ? 12f : 18f, 205, 52);
        }
    }

    private void RunCarouselInTransition(bool isNext)
    {
        if (CenterCarouselCard is null || LeftCarouselCard is null || RightCarouselCard is null)
        {
            return;
        }

        PrimeCard(CenterCarouselCard, isNext ? 60f : -60f, 6f, 0.82f, 0.92f, isNext ? 8f : -8f);
        PrimeCard(LeftCarouselCard, isNext ? 30f : -30f, 6f, 0.54f, 0.92f, -12f);
        PrimeCard(RightCarouselCard, isNext ? 30f : -30f, 6f, 0.54f, 0.92f, 12f);

        if (LeftFarCarouselCard is not null && RightFarCarouselCard is not null)
        {
            PrimeCard(LeftFarCarouselCard, isNext ? 14f : -14f, 5f, 0.24f, 0.92f, -18f);
            PrimeCard(RightFarCarouselCard, isNext ? 14f : -14f, 5f, 0.24f, 0.92f, 18f);
        }

        AnimateCard(CenterCarouselCard, 0f, 0f, 1f, 1f, 0f, 240, 0);
        AnimateCard(LeftCarouselCard, 0f, 0f, 1f, 1f, 0f, 255, 36);
        AnimateCard(RightCarouselCard, 0f, 0f, 1f, 1f, 0f, 255, 56);

        if (LeftFarCarouselCard is not null && RightFarCarouselCard is not null)
        {
            AnimateCard(LeftFarCarouselCard, 0f, 0f, 1f, 1f, 0f, 265, 44);
            AnimateCard(RightFarCarouselCard, 0f, 0f, 1f, 1f, 0f, 265, 64);
        }

        // Soft pulse on active card after it settles.
        PulseCenterCard();
    }

    private void PulseCenterCard()
    {
        if (CenterCarouselCard is null)
        {
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(CenterCarouselCard);
        var compositor = visual.Compositor;
        visual.CenterPoint = new Vector3((float)CenterCarouselCard.RenderSize.Width / 2f, (float)CenterCarouselCard.RenderSize.Height / 2f, 0f);

        var pulse = compositor.CreateVector3KeyFrameAnimation();
        var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.24f, 0.94f), new Vector2(0.28f, 1f));
        pulse.InsertKeyFrame(0f, new Vector3(1f, 1f, 1f));
        pulse.InsertKeyFrame(0.45f, new Vector3(1.018f, 1.018f, 1f), ease);
        pulse.InsertKeyFrame(1f, new Vector3(1f, 1f, 1f), ease);
        pulse.Duration = TimeSpan.FromMilliseconds(230);
        pulse.DelayTime = TimeSpan.FromMilliseconds(70);

        visual.StartAnimation(nameof(Visual.Scale), pulse);
    }

    private void ResetCarouselVisualState()
    {
        ResetCardVisual(CenterCarouselCard, targetOpacity: 1d);
        ResetCardVisual(LeftCarouselCard, targetOpacity: 0.82d);
        ResetCardVisual(RightCarouselCard, targetOpacity: 0.82d);
        ResetCardVisual(LeftFarCarouselCard, targetOpacity: 0.32d);
        ResetCardVisual(RightFarCarouselCard, targetOpacity: 0.32d);
    }

    private static void ResetCardVisual(UIElement? element, double targetOpacity)
    {
        if (element is null)
        {
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation(nameof(Visual.Offset));
        visual.StopAnimation(nameof(Visual.Scale));
        visual.StopAnimation(nameof(Visual.Opacity));
        visual.StopAnimation(nameof(Visual.RotationAngleInDegrees));
        visual.Offset = Vector3.Zero;
        visual.Scale = new Vector3(1f, 1f, 1f);
        visual.RotationAngleInDegrees = 0f;
        visual.Opacity = (float)targetOpacity;
    }

    private static void PrimeCard(UIElement element, float offsetX, float offsetY, float opacity, float scale, float rotationDeg)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.Offset = new Vector3(offsetX, offsetY, 0f);
        visual.Opacity = opacity;
        visual.Scale = new Vector3(scale, scale, 1f);
        visual.RotationAngleInDegrees = rotationDeg;
    }

    private static void AnimateCard(
        UIElement element,
        float offsetX,
        float offsetY,
        float settleOpacity,
        float startScale,
        float rotationDeg,
        int durationMs,
        int delayMs)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        visual.Opacity = settleOpacity;
        visual.Offset = new Vector3(offsetX, offsetY, 0f);
        visual.Scale = new Vector3(startScale, startScale, 1f);
        visual.CenterPoint = new Vector3((float)element.RenderSize.Width / 2f, (float)element.RenderSize.Height / 2f, 0f);
        visual.RotationAngleInDegrees = rotationDeg;

        var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        var easeOut = compositor.CreateCubicBezierEasingFunction(new Vector2(0.23f, 0.92f), new Vector2(0.31f, 1f));
        offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f), easeOut);
        offsetAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
        offsetAnimation.DelayTime = TimeSpan.FromMilliseconds(delayMs);

        var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(1f, 1f, easeOut);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
        opacityAnimation.DelayTime = TimeSpan.FromMilliseconds(delayMs);

        var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(1f, new Vector3(1f, 1f, 1f), easeOut);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
        scaleAnimation.DelayTime = TimeSpan.FromMilliseconds(delayMs);

        var rotateAnimation = compositor.CreateScalarKeyFrameAnimation();
        rotateAnimation.InsertKeyFrame(1f, 0f, easeOut);
        rotateAnimation.Duration = TimeSpan.FromMilliseconds(durationMs);
        rotateAnimation.DelayTime = TimeSpan.FromMilliseconds(delayMs);

        visual.StartAnimation(nameof(Visual.Offset), offsetAnimation);
        visual.StartAnimation(nameof(Visual.Opacity), opacityAnimation);
        visual.StartAnimation(nameof(Visual.Scale), scaleAnimation);
        visual.StartAnimation(nameof(Visual.RotationAngleInDegrees), rotateAnimation);
    }

    private void OnMacNavPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        element.Scale = new Vector3(1.03f, 1.03f, 1f);
        element.Translation = new Vector3(0f, -1f, 0f);
        AffordanceAnimationHelper.Fade(element, show: true, shownOpacity: 1, hiddenOpacity: 0.88);
    }

    private void OnMacNavPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        element.Scale = new Vector3(1f, 1f, 1f);
        element.Translation = new Vector3(0f, 0f, 0f);
        AffordanceAnimationHelper.Fade(element, show: false, shownOpacity: 1, hiddenOpacity: 0.88);
    }

    private void OnMacNavPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        element.Scale = new Vector3(0.97f, 0.97f, 1f);
        element.Translation = new Vector3(0f, 0f, 0f);
        element.Opacity = 0.96;
    }

    private void OnMacNavPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        element.Scale = new Vector3(1.03f, 1.03f, 1f);
        element.Translation = new Vector3(0f, -1f, 0f);
        element.Opacity = 1;
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

}
