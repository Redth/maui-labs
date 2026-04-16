namespace AIAttributes.Sample.KeyedAgents.Models;

/// <summary>
/// A plant in the user's garden.
/// </summary>
public record PlantEntry(
    string Nickname,
    string Species,
    string Location,
    DateTime AddedAt,
    DateTime? LastWatered = null);
