using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.Models;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class TemplatesPage : Page
{
    public TemplatesPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<TemplatesViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ((TemplatesViewModel)DataContext).LoadAsync();
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
}
