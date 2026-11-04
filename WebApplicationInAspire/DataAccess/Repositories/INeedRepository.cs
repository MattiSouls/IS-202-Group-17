using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.DataAccess.Repositories;

/// <summary>Datatilgang for behov. Controlleren kontrollerer rolle og eierskap.</summary>
public interface INeedRepository
{
    Task<IReadOnlyList<Need>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Need?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Need> AddAsync(Need need, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default);
}
