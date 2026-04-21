using System.Collections.ObjectModel;
using AIAttributes.Sample.Garden.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    private void AddToCart(string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return;
        _currentCart.AddItem(sku);
    }
}
