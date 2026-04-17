using System.ComponentModel;
using System.Runtime.CompilerServices;
using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// View model wrapper around <see cref="PlantEntry"/> so the garden panel
/// can reflect transient UI state (e.g., pending-removal ghosting) without
/// mutating the immutable record returned by <c>GardenService</c>.
/// </summary>
public sealed class GardenPlantViewModel(PlantEntry entry) : INotifyPropertyChanged
{
    public PlantEntry Entry { get; } = entry;

    public string Nickname => Entry.Nickname;
    public string Species => Entry.Species;
    public string Location => Entry.Location;
    public string Emoji => Entry.Emoji;

    private bool _isPendingRemoval;
    public bool IsPendingRemoval
    {
        get => _isPendingRemoval;
        set
        {
            if (_isPendingRemoval == value)
                return;
            _isPendingRemoval = value;
            OnChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
