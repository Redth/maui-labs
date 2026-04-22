using System.Collections.ObjectModel;
using System.ComponentModel;
using AIAttributes.Sample.Garden.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// Owns the product catalog data and the add-to-cart action.
/// </summary>
public sealed partial class CatalogViewModel : ObservableObject
{
    private readonly CurrentCart _currentCart;

    public CatalogViewModel(CurrentCart currentCart)
    {
        _currentCart = currentCart;

        var groups = ProductCatalog.All
            .GroupBy(p => p.Category)
            .Select(g =>
            {
                var group = new CatalogGroupViewModel(g.Key);
                group.AddRange(g.Select(p => new CatalogItemViewModel(p)));
                return group;
            })
            .ToList();

        Products = new(groups.SelectMany(g => g));
        Groups = groups;
    }

    public ObservableCollection<CatalogItemViewModel> Products { get; }

    public IReadOnlyList<CatalogGroupViewModel> Groups { get; }

    [RelayCommand]
    [ExportAIFunction("add_catalog_item_to_cart")]
    [Description("Add a product from the catalog to the cart by SKU or product name.")]
    public void AddToCart(
        [Description("The catalog product SKU or name to add to the cart.")] string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("A product SKU or name is required.", nameof(sku));

        _currentCart.AddItem(sku);
    }
}
