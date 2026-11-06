using System.Data;
using MySqlConnector;
using WebApplicationInAspire.Models;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Services;

namespace WebApplicationInAspire.DataAccess.Repositories;

/// <summary>Scoped datatilgang. Skriving låser ressursen i samme rekkefølge som tildeling.</summary>
public class ResourceRepository(ApplicationDbContext context) : IResourceRepository
{
    /// <summary>Filtrering og paginering skjer i SQL. Kartet får bare radene på den synlige siden.</summary>
    public async Task<ResourceOverviewViewModel> SearchAsync(string? ownerId, bool availableOnly, ListFilter filter)
    {
        filter.Sanitize();
        var query = context.VisibleResources(ownerId, availableOnly);
        var regions = await query.Select(r => r.Region).Distinct().OrderBy(v => v).Take(200).ToListAsync();
        var types = await query.Select(r => r.Type).Distinct().OrderBy(v => v).Take(200).ToListAsync();
        if (filter.RejectResults) query = query.Where(r => false);
        if (filter.Area is { } area) query = query.Where(r => r.Latitude >= area.South && r.Latitude <= area.North && r.Longitude >= area.West && r.Longitude <= area.East);
        else if (filter.HasAreaInput) query = query.Where(r => false);
        else if (!string.IsNullOrWhiteSpace(filter.Region)) { var region = MatchingRules.Normalize(filter.Region); query = query.Where(r => r.Region.Trim().ToUpper() == region); }
        if (!string.IsNullOrWhiteSpace(filter.Type)) { var type = MatchingRules.Normalize(filter.Type); query = query.Where(r => r.Type.Trim().ToUpper() == type); }
        if (!string.IsNullOrWhiteSpace(filter.Q)) { var term = filter.Q; query = query.Where(r => r.Name.Contains(term) || r.Type.Contains(term) || r.Region.Contains(term)); }
        if (!availableOnly && filter.Status == "assigned") query = query.Where(r => context.Needs.Any(n => n.AssignedResourceId == r.Id && n.Status == NeedStatuses.Assigned));
        if (!availableOnly && filter.Status == "paused") query = query.Where(r => !r.IsAvailable && !context.Needs.Any(n => n.AssignedResourceId == r.Id && n.Status == NeedStatuses.Assigned));
        if (filter.Status == "offered") query = query.Where(r => r.IsAvailable && !context.Needs.Any(n => n.AssignedResourceId == r.Id && n.Status == NeedStatuses.Assigned));
        var total = await query.CountAsync();
        filter.Page = Math.Min(filter.Page, Math.Max(1, (total + ListFilter.PageSize - 1) / ListFilter.PageSize));
        var rows = await query.OrderBy(r => r.Name).ThenBy(r => r.Id).Skip((filter.Page - 1) * ListFilter.PageSize).Take(ListFilter.PageSize).ToListAsync();
        var ids = rows.Select(r => r.Id).ToArray();
        var assigned = (await context.Needs.Where(n => n.Status == NeedStatuses.Assigned && n.AssignedResourceId != null && ids.Contains(n.AssignedResourceId.Value))
            .Select(n => n.AssignedResourceId!.Value).ToListAsync()).ToHashSet();
        return new ResourceOverviewViewModel { Resources = rows, Total = total, Filter = filter, Region = filter.Region, Regions = regions, Types = types, IsPublicOverview = availableOnly, AssignedResourceIds = assigned };
    }

    /// <summary>Henter rolletilpasset regionliste med én ekstra rad for å oppdage avkorting.</summary>
    public async Task<IReadOnlyList<Resource>> GetForOverviewAsync(string? ownerId, bool availableOnly, string? region)
    {
        var query = context.VisibleResources(ownerId, availableOnly);
        if (!string.IsNullOrWhiteSpace(region))
        {
            var normalized = MatchingRules.Normalize(region);
            query = query.Where(resource => resource.Region.Trim().ToUpper() == normalized);
        }
        return await query.OrderBy(resource => resource.Name).ThenBy(resource => resource.Id).Take(501).ToListAsync();
    }

    /// <summary>Finner ressurs-ID-er med aktiv tildeling.</summary>
    public async Task<IReadOnlySet<int>> GetAssignedIdsAsync() => (await context.Needs
        .Where(need => need.Status == NeedStatuses.Assigned && need.AssignedResourceId != null)
        .Select(need => need.AssignedResourceId!.Value).Distinct().ToListAsync()).ToHashSet();

    /// <summary>Henter én ressurs uten endringssporing; controlleren avgjør tilgang.</summary>
    public Task<Resource?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Resources.AsNoTracking().SingleOrDefaultAsync(resource => resource.Id == id, cancellationToken);

    /// <summary>Lagrer ressursen; samme registreringsnøkkel fra samme eier gir ikke dobbelt rad.</summary>
    public async Task<Resource> AddAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        context.Resources.Add(resource);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1062 } && resource.CreationToken.HasValue)
        {
            context.Entry(resource).State = EntityState.Detached;
            return await context.Resources.AsNoTracking().SingleAsync(r => r.OwnerId == resource.OwnerId && r.CreationToken == resource.CreationToken, cancellationToken);
        }
        return resource;
    }

    /// <summary>Låser ressursen og avviser utdaterte versjoner eller aktiv tildeling før oppdatering.</summary>
    public async Task<bool> UpdateAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var stored = (await context.Resources.FromSqlInterpolated($"SELECT * FROM Resources WHERE Id = {resource.Id} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        if (stored is null || stored.Version != resource.Version || await IsAssignedAsync(stored.Id, cancellationToken)) return false;
        stored.Version++;
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

    /// <summary>Sletter under ressurslås og avviser sletting av en aktivt tildelt ressurs.</summary>
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

    /// <summary>Kontrollerer aktiv tildeling i databasen, ikke verdier fra skjemaet.</summary>
    private Task<bool> IsAssignedAsync(int id, CancellationToken cancellationToken)
        => context.Needs.AnyAsync(need => need.AssignedResourceId == id && need.Status == NeedStatuses.Assigned, cancellationToken);
}
