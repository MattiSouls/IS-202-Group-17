using System.Data;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;
using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Services;

public enum AllocationResult { Success, NotFound, Forbidden, Conflict }

/// <summary>Matching og tildeling. Ressurs og behov endres samlet i en MariaDB-transaksjon.</summary>
public class AllocationService(ApplicationDbContext context)
{
    public async Task<IReadOnlyList<AllocationEvent>> HistoryAsync(int id) => await context.AllocationEvents.AsNoTracking().Where(e => e.NeedId == id).OrderByDescending(e => e.Id).Take(30).ToListAsync();

    public Task<Resource?> FindCandidateAsync(Need need, int id) => MatchingRules.CanAssign(need) ? Candidates(need).SingleOrDefaultAsync(r => r.Id == id) : Task.FromResult<Resource?>(null);
    public Task<Resource?> GetResourceAsync(int id) => context.Resources.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);

    private IQueryable<Resource> CandidateFilter(Need need)
    {
        var type = MatchingRules.Normalize(need.Type);
        var region = MatchingRules.Normalize(need.Region);
        var query = context.Resources.AsNoTracking().Where(resource => resource.IsAvailable
            && resource.AvailableFrom <= need.NeededAt && resource.AvailableUntil >= need.NeededAt
            && resource.Type.Trim().ToUpper() == type
            && !context.Needs.Any(other => other.AssignedResourceId == resource.Id && other.Status == NeedStatuses.Assigned));
        if (MatchingRules.Area(need) is { } area)
            return query.Where(r => r.Latitude >= area.South && r.Latitude <= area.North && r.Longitude >= area.West && r.Longitude <= area.East);
        if (MatchingRules.HasAreaInput(need)) return query.Where(r => false); // Ugyldige eldre data må ikke utvide søket.
        return query.Where(r => r.Region.Trim().ToUpper() == region);
    }

    private IQueryable<Resource> Candidates(Need need)
    {
        var query = CandidateFilter(need);
        if (MatchingRules.Origin(need) is { } origin)
        {
            var (latitude, longitude) = origin;
            const double rad = Math.PI / 180;
            // Storcirkelavstand oversettes til MariaDB-matematikk før Skip/Take.
            return query.OrderBy(resource => Math.Acos(Math.Min(1d, Math.Max(-1d,
                Math.Sin(latitude * rad) * Math.Sin(resource.Latitude * rad)
                + Math.Cos(latitude * rad) * Math.Cos(resource.Latitude * rad) * Math.Cos((resource.Longitude - longitude) * rad)))))
                .ThenBy(resource => resource.Id);
        }
        return query.OrderBy(resource => resource.Name).ThenBy(resource => resource.Id);
    }
    public Task<int> CountMatchesAsync(Need need) => MatchingRules.CanAssign(need)
        ? Candidates(need).CountAsync() : Task.FromResult(0);

    /// <summary>Kun én side med kandidater lastes; eventuelle avstander sorteres i databasen.</summary>
    public async Task<IReadOnlyList<Resource>> FindMatchesAsync(Need need, int page = 1)
    {
        if (!MatchingRules.CanAssign(need) || string.IsNullOrWhiteSpace(need.Type) || string.IsNullOrWhiteSpace(need.Region)) return [];
        return await Candidates(need).Skip((Math.Clamp(page, 1, 100000) - 1) * 10).Take(10).ToListAsync();
    }

    /// <summary>Betingede UPDATE-er sørger for at bare én forespørsel kan ta ressursen.</summary>
    public async Task<AllocationResult> AssignAsync(int needId, int resourceId, string userId, bool isAdmin, long? expectedNeedVersion = null, long? expectedResourceVersion = null)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var need = await context.Needs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == needId);
        if (need is null) return AllocationResult.NotFound;
        if (need.CreatedByUserId != userId && !isAdmin) return AllocationResult.Forbidden;
        if (!MatchingRules.CanAssign(need) || (expectedNeedVersion.HasValue && need.Version != expectedNeedVersion)) return AllocationResult.Conflict;
        var resource = await context.Resources.AsNoTracking().SingleOrDefaultAsync(item => item.Id == resourceId);
        if (resource is null) return AllocationResult.NotFound;
        if (expectedResourceVersion.HasValue && resource.Version != expectedResourceVersion) return AllocationResult.Conflict;

        var type = MatchingRules.Normalize(need.Type);
        var region = MatchingRules.Normalize(need.Region);
        if (type.Length == 0 || region.Length == 0) return AllocationResult.Conflict;

        // Låser/oppdaterer ressursen før behovet. Andre tildelinger må vente og sjekke vilkårene igjen.
        var originalResourceVersion = resource.Version;
        var claimed = await CandidateFilter(need).Where(resource => resource.Id == resourceId && resource.Version == originalResourceVersion)
            .ExecuteUpdateAsync(update => update.SetProperty(resource => resource.IsAvailable, false)
                .SetProperty(resource => resource.Version, resource => resource.Version + 1));
        if (claimed != 1) return AllocationResult.Conflict;

        var assigned = await context.Needs.Where(item => item.Id == needId && item.Version == need.Version && item.AssignedResourceId == null
                && (item.Status == NeedStatuses.New || item.Status == NeedStatuses.UnderReview))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.AssignedResourceId, resourceId)
                .SetProperty(item => item.Status, NeedStatuses.Assigned).SetProperty(item => item.Version, item => item.Version + 1));
        if (assigned != 1) return AllocationResult.Conflict; // Dispose ruller tilbake ressursendringen.
        context.AllocationEvents.Add(new AllocationEvent { NeedId = need.Id, ResourceId = resource.Id, ActorUserId = userId, Action = "Assigned", ResourceName = resource.Name });
        AddNotices(need, resource, "Ressurs tildelt", $"{resource.Name} er tildelt behov #{need.Id}: {need.Type} i {need.Region}, {need.NeededAt:dd.MM.yyyy HH:mm}. Se tildelingen for kontakt og praktisk oppfølging.");
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return AllocationResult.Success;
    }

    /// <summary>Frigjør tidlig til Vurderes, eller avslutter med Løst, uten å etterlate en låst ressurs.</summary>
    public async Task<AllocationResult> ReleaseAsync(int needId, string userId, bool isAdmin, bool resolve, long? expectedNeedVersion = null)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var need = await context.Needs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == needId);
        if (need is null) return AllocationResult.NotFound;
        if (need.CreatedByUserId != userId && !isAdmin) return AllocationResult.Forbidden;
        if (expectedNeedVersion.HasValue && need.Version != expectedNeedVersion) return AllocationResult.Conflict;
        if (need.Status != NeedStatuses.Assigned || need.AssignedResourceId is not int resourceId)
            return AllocationResult.Conflict;

        // Samme låserekkefølge som AssignAsync. Eldre doble referanser gjør ikke ressursen ledig for tidlig.
        var resource = await context.Resources.AsNoTracking().SingleOrDefaultAsync(r => r.Id == resourceId);
        if (resource is null) return AllocationResult.Conflict;
        var released = await context.Resources.Where(resource => resource.Id == resourceId)
            .ExecuteUpdateAsync(update => update.SetProperty(resource => resource.IsAvailable,
                resource => !context.Needs.Any(other => other.Id != needId
                    && other.AssignedResourceId == resource.Id && other.Status == NeedStatuses.Assigned))
                .SetProperty(resource => resource.Version, resource => resource.Version + 1));
        if (released != 1) return AllocationResult.Conflict;

        var changed = await context.Needs.Where(item => item.Id == needId && item.Version == need.Version && item.Status == NeedStatuses.Assigned
                && item.AssignedResourceId == resourceId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.AssignedResourceId, (int?)null)
                .SetProperty(item => item.Status, resolve ? NeedStatuses.Resolved : NeedStatuses.UnderReview).SetProperty(item => item.Version, item => item.Version + 1));
        if (changed != 1) return AllocationResult.Conflict;
        context.AllocationEvents.Add(new AllocationEvent { NeedId = need.Id, ResourceId = resource.Id, ActorUserId = userId, Action = resolve ? "Resolved" : "Released", ResourceName = resource.Name });
        AddNotices(need, resource, resolve ? "Behov løst" : "Ressurs frigitt",
            $"{resource.Name} er frigitt fra behov #{need.Id}: {need.Type} i {need.Region}. " + (resolve ? "Behovet er avsluttet." : "Behovet vurderes igjen."));
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return AllocationResult.Success;
    }
    private void AddNotices(Need need, Resource resource, string title, string message)
    {
        foreach (var userId in new[] { resource.OwnerId, need.CreatedByUserId }.Distinct())
            context.Notifications.Add(new PortalNotification
            {
                UserId = userId,
                Title = title,
                Message = message,
                NeedId = need.Id,
                ResourceId = resource.Id
            });
    }
}
