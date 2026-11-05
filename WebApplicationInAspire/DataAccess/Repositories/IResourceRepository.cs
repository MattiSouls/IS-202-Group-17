using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.DataAccess.Repositories;

/// <summary>
/// Datatilgang for ressursene. Controlleren må kontrollere brukerens rettigheter for skriving.
/// </summary>
public interface IResourceRepository
{
    Task<IReadOnlyList<Resource>> GetForOverviewAsync(string? ownerId, bool availableOnly, string? region);
    Task<IReadOnlySet<int>> GetAssignedIdsAsync();
    Task<IReadOnlyList<Resource>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Resource>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);
    Task<Resource?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Resource> AddAsync(Resource resource, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Resource resource, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
