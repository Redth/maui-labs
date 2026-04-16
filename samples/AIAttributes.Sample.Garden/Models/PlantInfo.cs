namespace AIAttributes.Sample.Garden.Models;

/// <summary>
/// A plant species in the catalog.
/// </summary>
public record PlantInfo(
    string CommonName,
    string ScientificName,
    string Category,
    string SunlightNeeds,
    int WateringFrequencyDays);
