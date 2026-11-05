using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Models;

/// <summary>Et lagret behov med aktuelle ressurser og brukerens rett til å tildele.</summary>
public class NeedMatchesViewModel
{
    public required Need Need { get; init; }
    public IReadOnlyList<Resource> Resources { get; init; } = [];
    public int Page { get; init; } = 1;
    public int Total { get; init; }
    public int Pages => Math.Max(1, (Total + 9) / 10);
    public bool CanManage { get; init; }
}
