using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.DataAccess.Repositories;

/// <summary>EF Core-implementasjon. Hver endring lagres i MariaDB med SaveChangesAsync.</summary>
public class ResourceRepository(ApplicationDbContext context) : IResourceRepository
{
    public async Task<IReadOnlyList<Resource>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Resources.AsNoTracking()
            .OrderBy(resource => resource.Name)
            .ThenBy(resource => resource.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Resource>> GetByOwnerAsync(
        string ownerId, CancellationToken cancellationToken = default)
        => await context.Resources.AsNoTracking()
            .Where(resource => resource.OwnerId == ownerId)
            .OrderBy(resource => resource.Name)
            .ThenBy(resource => resource.Id)
            .ToListAsync(cancellationToken);

    public Task<Resource?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Resources.AsNoTracking()
            .SingleOrDefaultAsync(resource => resource.Id == id, cancellationToken);

    public async Task<Resource> AddAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        context.Resources.Add(resource);
        await context.SaveChangesAsync(cancellationToken);
        return resource;
    }

    public async Task<bool> UpdateAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        var stored = await context.Resources.FindAsync([resource.Id], cancellationToken);
        if (stored is null)
        {
            return false;
        }

        // Eier og opprettelsestid endres ikke gjennom et vanlig redigeringsskjema.
        stored.Name = resource.Name;
        stored.Type = resource.Type;
        stored.Description = resource.Description;
        stored.ContactName = resource.ContactName;
        stored.PhoneNumber = resource.PhoneNumber;
        stored.Latitude = resource.Latitude;
        stored.Longitude = resource.Longitude;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var stored = await context.Resources.FindAsync([id], cancellationToken);
        if (stored is null)
        {
            return false;
        }

        context.Resources.Remove(stored);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
