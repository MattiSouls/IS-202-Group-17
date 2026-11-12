using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.DataAccess.Repositories;

/// <summary>
/// Datatilgang for ressursene. Controlleren må kontrollere brukerens rettigheter for skriving.
/// </summary>
public interface IResourceRepository
{
    Task<WebApplicationInAspire.Models.ResourceOverviewViewModel> SearchAsync(string? ownerId, bool availableOnly, WebApplicationInAspire.Models.ListFilter filter);
    Task<IReadOnlyList<Resource>> GetForOverviewAsync(string? ownerId, bool availableOnly, string? region);
    Task<IReadOnlySet<int>> GetAssignedIdsAsync();
    Task<Resource?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Resource> AddAsync(Resource resource, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Resource resource, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
