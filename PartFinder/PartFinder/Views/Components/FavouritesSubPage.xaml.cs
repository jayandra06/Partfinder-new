using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.Helpers;
using PartFinder.Models;
using PartFinder.ViewModels;

namespace PartFinder.Views.Components;

public sealed partial class FavouritesSubPage : UserControl
{
    private FavouritesViewModel? _vm;
    private TemplatesViewModel? _templatesVm;

    // Raised when the user clicks "Back to Templates"
    public event EventHandler? BackRequested;

    public FavouritesSubPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this when the sub-page becomes visible.
    /// Loads favourite templates and plays the entrance animation.
    /// </summary>
    public async Task ShowAsync(TemplatesViewModel templatesVm)
    {
        _templatesVm = templatesVm;

        _vm = App.Services.GetRequiredService<FavouritesViewModel>();
        DataContext = _vm;

        _vm.EditTemplateRequested += OnEditTemplateRequested;

        await _vm.LoadAsync(templatesVm.Templates).ConfigureAwait(true);

        RefreshActiveCard();

        // Entrance animation
        if (Resources["EntranceSb"] is Storyboard entrance)
        {
            entrance.Begin();
        }
    }

    /// <summary>
    /// Plays the exit animation. Caller should hide the control after awaiting.
    /// </summary>
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
        // Subscribe to ViewModel changes to refresh card display
        if (DataContext is FavouritesViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
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
        if (e.PropertyName == nameof(FavouritesViewModel.ActiveIndex) ||
            e.PropertyName == nameof(FavouritesViewModel.FavouriteTemplates) ||
            e.PropertyName == nameof(FavouritesViewModel.IsEmpty))
        {
            RefreshActiveCard();
        }
    }

    // ── Card rendering ────────────────────────────────────────────────────────

    private void RefreshActiveCard()
    {
        if (_vm is null || _vm.IsEmpty)
        {
            return;
        }

        var cards = _vm.FavouriteTemplates;
        var index = _vm.ActiveIndex;

        if (index < 0 || index >= cards.Count)
        {
            return;
        }

        var activeCard = cards[index];

        // Update active card content
        CardTitleText.Text = activeCard.Name;
        CardFieldCountText.Text = activeCard.FieldCountLabel;
        CardStarIcon.Glyph = activeCard.IsFavourite ? "\uE735" : "\uE734";
        CardStarIcon.Foreground = activeCard.IsFavourite
            ? (Brush)Application.Current.Resources["AccentPrimaryBrush"]
            : (Brush)Application.Current.Resources["TextSecondaryBrush"];

        // Rebuild field chips
        FieldChipsPanel.Children.Clear();
        foreach (var field in activeCard.Template.Fields.OrderBy(f => f.DisplayOrder).Take(8))
        {
            var typeColor = field.Type switch
            {
                PartFinder.Models.TemplateFieldType.Number  => "#0D2A1A",
                PartFinder.Models.TemplateFieldType.Decimal => "#0D2A1A",
                PartFinder.Models.TemplateFieldType.Date    => "#1A1A0D",
                PartFinder.Models.TemplateFieldType.Boolean => "#1A0D2A",
                PartFinder.Models.TemplateFieldType.Dropdown => "#0D1A2A",
                PartFinder.Models.TemplateFieldType.RecordLink => "#1A0D0D",
                _ => "#0D1E33",
            };
            var typeBorder = field.Type switch
            {
                PartFinder.Models.TemplateFieldType.Number  => "#2ABD8F",
                PartFinder.Models.TemplateFieldType.Decimal => "#2ABD8F",
                PartFinder.Models.TemplateFieldType.Date    => "#FFB781",
                PartFinder.Models.TemplateFieldType.Boolean => "#A4C9FF",
                PartFinder.Models.TemplateFieldType.Dropdown => "#1F7AE0",
                PartFinder.Models.TemplateFieldType.RecordLink => "#FFB4AB",
                _ => "#2A3D58",
            };

            var chip = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,
                    Convert.ToByte(typeColor.Substring(1, 2), 16),
                    Convert.ToByte(typeColor.Substring(3, 2), 16),
                    Convert.ToByte(typeColor.Substring(5, 2), 16))),
                BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,
                    Convert.ToByte(typeBorder.Substring(1, 2), 16),
                    Convert.ToByte(typeBorder.Substring(3, 2), 16),
                    Convert.ToByte(typeBorder.Substring(5, 2), 16))),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 5, 10, 5),
                Child = new TextBlock
                {
                    Text = field.Label,
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                },
            };
            FieldChipsPanel.Children.Add(chip);
        }

        // Update background card visibility based on how many favourites exist
        // (depth cards removed — horizontal slide layout)
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        AnimateCardTransition(forward: false);
        _vm.GoPreviousCommand.Execute(null);
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        AnimateCardTransition(forward: true);
        _vm.GoNextCommand.Execute(null);
    }

    private void AnimateCardTransition(bool forward)
    {
        // forward (Next)  → current slides LEFT out,  new card slides in from RIGHT
        // backward (Prev) → current slides RIGHT out, new card slides in from LEFT
        const double slideDistance = 560.0;
        var outTo  = forward ? -slideDistance :  slideDistance;
        var inFrom = forward ?  slideDistance : -slideDistance;

        var ease = new ExponentialEase { EasingMode = EasingMode.EaseInOut, Exponent = 5 };

        // ── Slide + fade OUT ──────────────────────────────────────────────────
        var outX = new DoubleAnimation
        {
            From = 0, To = outTo,
            Duration = TimeSpan.FromMilliseconds(340),
            EasingFunction = ease,
            EnableDependentAnimation = true,
        };
        var outFade = new DoubleAnimation
        {
            From = 1, To = 0,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = ease,
        };

        Storyboard.SetTarget(outX,    ActiveCardTransform); Storyboard.SetTargetProperty(outX,    "TranslateX");
        Storyboard.SetTarget(outFade, ActiveCard);          Storyboard.SetTargetProperty(outFade, "Opacity");

        var outSb = new Storyboard();
        outSb.Children.Add(outX);
        outSb.Children.Add(outFade);

        outSb.Completed += (_, _) =>
        {
            // Snap new card to incoming side (off-screen), invisible
            ActiveCardTransform.TranslateX = inFrom;
            ActiveCard.Opacity = 0;

            // Update card content
            RefreshActiveCard();

            var easeIn = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 };

            // ── Slide + fade IN ───────────────────────────────────────────────
            var inX = new DoubleAnimation
            {
                From = inFrom, To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = easeIn,
                EnableDependentAnimation = true,
            };
            var inFade = new DoubleAnimation
            {
                From = 0, To = 1,
                Duration = TimeSpan.FromMilliseconds(320),
                EasingFunction = easeIn,
            };

            Storyboard.SetTarget(inX,    ActiveCardTransform); Storyboard.SetTargetProperty(inX,    "TranslateX");
            Storyboard.SetTarget(inFade, ActiveCard);          Storyboard.SetTargetProperty(inFade, "Opacity");

            var inSb = new Storyboard();
            inSb.Children.Add(inX);
            inSb.Children.Add(inFade);
            inSb.Begin();
        };

        outSb.Begin();
    }

    // ── Card actions ──────────────────────────────────────────────────────────

    private void OnCardStarClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || _vm.IsEmpty)
        {
            return;
        }

        var card = _vm.FavouriteTemplates.ElementAtOrDefault(_vm.ActiveIndex);
        if (card is null)
        {
            return;
        }

        _vm.ToggleFavouriteAsyncCommand.Execute(card.Template.Id);
    }

    private void OnCardEditClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || _vm.IsEmpty)
        {
            return;
        }

        var card = _vm.FavouriteTemplates.ElementAtOrDefault(_vm.ActiveIndex);
        if (card is null)
        {
            return;
        }

        _vm.BeginEditTemplateCommand.Execute(card.Template.Id);
    }

    private async void OnCardAddRowClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || _templatesVm is null || XamlRoot is null || _vm.IsEmpty)
        {
            return;
        }

        var card = _vm.FavouriteTemplates.ElementAtOrDefault(_vm.ActiveIndex);
        if (card is null)
        {
            return;
        }

        // Select the template in TemplatesViewModel so InsertColumnIntoSelectedTemplateAsync works
        _templatesVm.SelectedTemplate = card.Template;

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
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        await _templatesVm.InsertColumnIntoSelectedTemplateAsync(
            card.Template.Fields.Count,
            input.Text).ConfigureAwait(true);

        // Reload to reflect new field count
        await _vm.LoadAsync(_templatesVm.Templates).ConfigureAwait(true);
        RefreshActiveCard();
    }

    private async void OnCardDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || XamlRoot is null || _vm.IsEmpty)
        {
            return;
        }

        var card = _vm.FavouriteTemplates.ElementAtOrDefault(_vm.ActiveIndex);
        if (card is null)
        {
            return;
        }

        var dlg = new ContentDialog
        {
            Title = "Delete template?",
            Content = $"This will permanently delete \"{card.Name}\" and all its data. This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            await _vm.DeleteTemplateAsyncCommand.ExecuteAsync(card.Template.Id).ConfigureAwait(true);
            RefreshActiveCard();
        }
    }

    // ── Back navigation ───────────────────────────────────────────────────────

    private void OnBackToTemplatesClick(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    // ── Edit template event ───────────────────────────────────────────────────

    private void OnEditTemplateRequested(object? sender, string templateId)
    {
        // Delegate to TemplatesViewModel to begin edit flow
        _templatesVm?.BeginEditSelectedTemplateCommand.Execute(null);
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

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
