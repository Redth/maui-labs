using System.Collections.ObjectModel;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// A group of garden plants at a single location, used to drive an
/// <c>IsGrouped</c> CollectionView so the sidebar clusters plants like a
/// real garden ("Kitchen windowsill", "Back porch", ...).
/// </summary>
public sealed class LocationGroup(string location, IEnumerable<GardenPlantViewModel> plants)
    : ObservableCollection<GardenPlantViewModel>(plants)
{
    public string Location { get; } = location;
    public string Header => $"📍 {Location} · {Count}";
}
