namespace AIAttributes.Sample.KeyedAgents.Models;

/// <summary>
/// Detailed care guide for a species.
/// </summary>
public record CareGuide(
    string Species,
    string WateringInstructions,
    string SunlightRequirements,
    string SoilType,
    string FertilizingSchedule,
    bool FrostTolerant,
    string[] CommonIssues);
