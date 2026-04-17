using System.Collections.ObjectModel;
using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// A group of shopping-list items in a single category, used to drive an
/// <c>IsGrouped</c> CollectionView so the workspace clusters items like
/// real shopping aisles ("Seeds · 3", "Tools · 1").
/// </summary>
public sealed class CategoryGroup(string category, IEnumerable<ShoppingListItemViewModel> items)
    : ObservableCollection<ShoppingListItemViewModel>(items)
{
    public string Category { get; } = category;
    public string Header => $"📂 {Category} · {Count}";
}
