using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Views.Components;

public sealed partial class WmsRoadmapPlaceholder : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(WmsRoadmapPlaceholder),
        new PropertyMetadata("Coming soon"));

    public static readonly DependencyProperty PhaseLabelProperty = DependencyProperty.Register(
        nameof(PhaseLabel),
        typeof(string),
        typeof(WmsRoadmapPlaceholder),
        new PropertyMetadata("Roadmap (not operational)"));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(WmsRoadmapPlaceholder),
        new PropertyMetadata(
            "This module is planned for a future WMS phase. No stock or order actions are enabled here."));

    public WmsRoadmapPlaceholder() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string PhaseLabel
    {
        get => (string)GetValue(PhaseLabelProperty);
        set => SetValue(PhaseLabelProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}