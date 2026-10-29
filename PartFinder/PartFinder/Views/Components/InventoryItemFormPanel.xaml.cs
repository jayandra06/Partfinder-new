using Microsoft.UI.Xaml.Controls;
using PartFinder.ViewModels;

namespace PartFinder.Views.Components;

public sealed partial class InventoryItemFormPanel : UserControl
{
    public InventoryItemFormPanel()
    {
        InitializeComponent();
    }

    public void PrepareForAdd()
    {
        PartNameBox.IsReadOnly = false;
        PartNameBox.Text = string.Empty;
        PartIdBox.Text = string.Empty;
        CategoryBox.Text = string.Empty;
        SupplierBox.Text = string.Empty;
        LocationBox.Text = string.Empty;
        QuantityBox.Text = string.Empty;
        MinStockBox.Text = string.Empty;
        UnitPriceBox.Text = string.Empty;
        NotesBox.Text = string.Empty;
    }

    public void PrepareForEdit(InventoryItem item)
    {
        PartNameBox.IsReadOnly = true;
        PartNameBox.Text = item.PartName;
        PartIdBox.Text = item.Id;
        CategoryBox.Text = item.Category;
        SupplierBox.Text = item.Supplier;
        LocationBox.Text = item.Location;
        QuantityBox.Text = item.Quantity.ToString();
        MinStockBox.Text = item.MinStock.ToString();
        UnitPriceBox.Text = item.UnitPrice.ToString("F2");
        NotesBox.Text = string.Empty;
    }

    public string PartName => PartNameBox.Text.Trim();
    public string PartId => PartIdBox.Text.Trim();
    public string Category => CategoryBox.Text.Trim();
    public string Supplier => SupplierBox.Text.Trim();
    public string Location => LocationBox.Text.Trim();
    public string QuantityText => QuantityBox.Text.Trim();
    public string MinStockText => MinStockBox.Text.Trim();
    public string UnitPriceText => UnitPriceBox.Text.Trim();
    public string Notes => NotesBox.Text.Trim();
}
