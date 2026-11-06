using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Models;

/// <summary>Rollefiltrerte ressurser, regionvalg og aktive tildelinger for oversikt og utskrift.</summary>
public class ResourceOverviewViewModel
{
    public ListFilter Filter { get; init; } = new();
    public int Total { get; init; }
    public int Pages => Math.Max(1, (Total + ListFilter.PageSize - 1) / ListFilter.PageSize);
    public IReadOnlyList<string> Types { get; init; } = [];
    public IReadOnlyList<Resource> Resources { get; init; } = [];
    public IReadOnlyList<string> Regions { get; init; } = [];
    public IReadOnlySet<int> AssignedResourceIds { get; init; } = new HashSet<int>();
    public string? Region { get; init; }
    public bool IsPublicOverview { get; init; }
}
