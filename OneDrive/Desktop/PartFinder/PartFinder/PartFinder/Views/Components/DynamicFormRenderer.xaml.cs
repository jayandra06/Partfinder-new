using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.Models;

namespace PartFinder.Views.Components;

public sealed partial class DynamicFormRenderer : UserControl
{
    public static readonly DependencyProperty FieldsProperty =
        DependencyProperty.Register(nameof(Fields), typeof(IReadOnlyList<TemplateFieldDefinition>), typeof(DynamicFormRenderer), new PropertyMetadata(null));

    public DynamicFormRenderer() => InitializeComponent();

    public IReadOnlyList<TemplateFieldDefinition>? Fields
    {
        get => (IReadOnlyList<TemplateFieldDefinition>?)GetValue(FieldsProperty);
        set => SetValue(FieldsProperty, value);
    }
}
