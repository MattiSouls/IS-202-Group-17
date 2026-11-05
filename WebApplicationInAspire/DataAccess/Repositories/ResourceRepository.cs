using System.Data;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Services;

namespace WebApplicationInAspire.DataAccess.Repositories;

/// <summary>Scoped datatilgang. Skriving låser ressursen i samme rekkefølge som tildeling.</summary>
public class ResourceRepository(ApplicationDbContext context) : IResourceRepository
{
    public async Task<IReadOnlyList<Resource>> GetForOverviewAsync(string? ownerId, bool availableOnly, string? region)
    {
        var query = context.Resources.AsNoTracking();
        if (ownerId is not null) query = query.Where(resource => resource.OwnerId == ownerId);
        if (availableOnly) query = query.Where(resource => resource.IsAvailable
            && resource.AvailableFrom != null && resource.AvailableUntil > resource.AvailableFrom
            && !context.Needs.Any(need => need.AssignedResourceId == resource.Id && need.Status == NeedStatuses.Assigned));
        if (!string.IsNullOrWhiteSpace(region))
        {
            var normalized = MatchingRules.Normalize(region);
            query = query.Where(resource => resource.Region.Trim().ToUpper() == normalized);
        }
        return await query.OrderBy(resource => resource.Name).ThenBy(resource => resource.Id).ToListAsync();
    }

    public async Task<IReadOnlySet<int>> GetAssignedIdsAsync() => (await context.Needs
        .Where(need => need.Status == NeedStatuses.Assigned && need.AssignedResourceId != null)
        .Select(need => need.AssignedResourceId!.Value).Distinct().ToListAsync()).ToHashSet();

    public async Task<IReadOnlyList<Resource>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Resources.AsNoTracking().OrderBy(resource => resource.Name).ThenBy(resource => resource.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Resource>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
        => await context.Resources.AsNoTracking().Where(resource => resource.OwnerId == ownerId)
            .OrderBy(resource => resource.Name).ThenBy(resource => resource.Id).ToListAsync(cancellationToken);

    public Task<Resource?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Resources.AsNoTracking().SingleOrDefaultAsync(resource => resource.Id == id, cancellationToken);

    public async Task<Resource> AddAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        context.Resources.Add(resource);
        await context.SaveChangesAsync(cancellationToken);
        return resource;
    }

    public async Task<bool> UpdateAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var stored = (await context.Resources.FromSqlInterpolated($"SELECT * FROM Resources WHERE Id = {resource.Id} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        if (stored is null || await IsAssignedAsync(stored.Id, cancellationToken)) return false;
        stored.Name = resource.Name;
        stored.Type = resource.Type;
        stored.Region = resource.Region;
        stored.IsAvailable = resource.IsAvailable;
        stored.AvailableFrom = resource.AvailableFrom;
        stored.AvailableUntil = resource.AvailableUntil;
        stored.Description = resource.Description;
        stored.ContactName = resource.ContactName;
        stored.PhoneNumber = resource.PhoneNumber;
        stored.Latitude = resource.Latitude;
        stored.Longitude = resource.Longitude;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var stored = (await context.Resources.FromSqlInterpolated($"SELECT * FROM Resources WHERE Id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        if (stored is null || await IsAssignedAsync(id, cancellationToken)) return false;
        context.Resources.Remove(stored);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private Task<bool> IsAssignedAsync(int id, CancellationToken cancellationToken)
        => context.Needs.AnyAsync(need => need.AssignedResourceId == id && need.Status == NeedStatuses.Assigned, cancellationToken);
}
