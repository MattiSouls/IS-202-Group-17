using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Models;

/// <summary>Rollefiltrerte ressurser, regionvalg og aktive tildelinger for oversikt og utskrift.</summary>
public class ResourceOverviewViewModel
{
    public IReadOnlyList<Resource> Resources { get; init; } = [];
    public IReadOnlyList<string> Regions { get; init; } = [];
    public IReadOnlySet<int> AssignedResourceIds { get; init; } = new HashSet<int>();
    public string? Region { get; init; }
    public bool IsPublicOverview { get; init; }
}
