namespace WebApplicationInAspire.Models;

/// <summary>Data til kartsiden. Inneholder ikke kontaktopplysninger.</summary>
public class MapViewModel
{
    public bool CanSeeNeeds { get; init; }
    public bool CanRegisterNeed { get; init; }
    public bool CanRegisterResource { get; init; }
    public IReadOnlyList<MapResource> Resources { get; init; } = [];
    public IReadOnlyList<MapNeed> Needs { get; init; } = [];
}

public record MapResource(int Id, string Name, string Type, string Region, double Latitude, double Longitude, string Availability);

public record MapNeed(int Id, string Type, string Region, string Priority, string PriorityClass, string Status, string NeededAt,
    double? Latitude, double? Longitude, double[]? Area);
