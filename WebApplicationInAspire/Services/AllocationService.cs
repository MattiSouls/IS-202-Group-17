using System.Data;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;
using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Services;

public enum AllocationResult { Success, NotFound, Forbidden, Conflict }

/// <summary>Matching og tildeling. Ressurs og behov endres samlet i en MariaDB-transaksjon.</summary>
public class AllocationService(ApplicationDbContext context)
{
    /// <summary>Filtrerer på type, region og ledig ressurs uten aktiv tildeling.</summary>
    public async Task<IReadOnlyList<Resource>> FindMatchesAsync(Need need)
    {
        if (!MatchingRules.CanAssign(need)) return [];
        var type = MatchingRules.Normalize(need.Type);
        var region = MatchingRules.Normalize(need.Region);
        if (type.Length == 0 || region.Length == 0) return [];
        return await context.Resources.AsNoTracking()
            .Where(resource => resource.IsAvailable
                && resource.Type.Trim().ToUpper() == type && resource.Region.Trim().ToUpper() == region
                && !context.Needs.Any(other => other.AssignedResourceId == resource.Id && other.Status == NeedStatuses.Assigned))
            .OrderBy(resource => resource.Name).ThenBy(resource => resource.Id).ToListAsync();
    }

    /// <summary>Betingede UPDATE-er sørger for at bare én forespørsel kan ta ressursen.</summary>
    public async Task<AllocationResult> AssignAsync(int needId, int resourceId, string userId, bool isAdmin)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var need = await context.Needs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == needId);
        if (need is null) return AllocationResult.NotFound;
        if (need.CreatedByUserId != userId && !isAdmin) return AllocationResult.Forbidden;
        if (!MatchingRules.CanAssign(need)) return AllocationResult.Conflict;
        if (!await context.Resources.AnyAsync(item => item.Id == resourceId)) return AllocationResult.NotFound;

        var type = MatchingRules.Normalize(need.Type);
        var region = MatchingRules.Normalize(need.Region);
        if (type.Length == 0 || region.Length == 0) return AllocationResult.Conflict;

        // Låser/oppdaterer ressursen før behovet. Andre tildelinger må vente og sjekke vilkårene igjen.
        var claimed = await context.Resources.Where(resource => resource.Id == resourceId && resource.IsAvailable
                && resource.Type.Trim().ToUpper() == type && resource.Region.Trim().ToUpper() == region
                && !context.Needs.Any(other => other.AssignedResourceId == resource.Id && other.Status == NeedStatuses.Assigned))
            .ExecuteUpdateAsync(update => update.SetProperty(resource => resource.IsAvailable, false));
        if (claimed != 1) return AllocationResult.Conflict;

        var assigned = await context.Needs.Where(item => item.Id == needId && item.AssignedResourceId == null
                && (item.Status == NeedStatuses.New || item.Status == NeedStatuses.UnderReview))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.AssignedResourceId, resourceId)
                .SetProperty(item => item.Status, NeedStatuses.Assigned));
        if (assigned != 1) return AllocationResult.Conflict; // Dispose ruller tilbake ressursendringen.
        await transaction.CommitAsync();
        return AllocationResult.Success;
    }

    /// <summary>Frigjør tidlig til Vurderes, eller avslutter med Løst, uten å etterlate en låst ressurs.</summary>
    public async Task<AllocationResult> ReleaseAsync(int needId, string userId, bool isAdmin, bool resolve)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var need = await context.Needs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == needId);
        if (need is null) return AllocationResult.NotFound;
        if (need.CreatedByUserId != userId && !isAdmin) return AllocationResult.Forbidden;
        if (need.Status != NeedStatuses.Assigned || need.AssignedResourceId is not int resourceId)
            return AllocationResult.Conflict;

        // Samme låserekkefølge som AssignAsync. Eldre doble referanser gjør ikke ressursen ledig for tidlig.
        var released = await context.Resources.Where(resource => resource.Id == resourceId)
            .ExecuteUpdateAsync(update => update.SetProperty(resource => resource.IsAvailable,
                resource => !context.Needs.Any(other => other.Id != needId
                    && other.AssignedResourceId == resource.Id && other.Status == NeedStatuses.Assigned)));
        if (released != 1) return AllocationResult.Conflict;

        var changed = await context.Needs.Where(item => item.Id == needId && item.Status == NeedStatuses.Assigned
                && item.AssignedResourceId == resourceId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.AssignedResourceId, (int?)null)
                .SetProperty(item => item.Status, resolve ? NeedStatuses.Resolved : NeedStatuses.UnderReview));
        if (changed != 1) return AllocationResult.Conflict;
        await transaction.CommitAsync();
        return AllocationResult.Success;
    }
}
