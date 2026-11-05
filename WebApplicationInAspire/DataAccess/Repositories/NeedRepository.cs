using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using WebApplicationInAspire.Models;
using WebApplicationInAspire.Services;
using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.DataAccess.Repositories;

/// <summary>Lagrer og leser behov i MariaDB med samme DbContext som ressurser og Identity.</summary>
public class NeedRepository(ApplicationDbContext context) : INeedRepository
{
    /// <summary>Filtrerer og sorterer behov i SQL, med valgfritt eierfilter og avgrenset side.</summary>
    public async Task<NeedListViewModel> SearchAsync(ListFilter filter, string userId)
    {
        filter.Sanitize();
        var query = context.Needs.AsNoTracking();
        var regions = await query.Select(n => n.Region).Distinct().OrderBy(v => v).Take(200).ToListAsync();
        if (filter.Mine) query = query.Where(n => n.CreatedByUserId == userId);
        if (NeedStatusesForFilter.Contains(filter.Status ?? "")) query = query.Where(n => n.Status == filter.Status);
        if (filter.Status is "uncovered" or "urgent" or "time-passed") query = query.Uncovered();
        if (filter.Status == "urgent") query = query.Where(n => n.Priority == "Urgent");
        if (filter.Status == "time-passed") { var now = PortalDisplay.NorwayNow; query = query.Where(n => n.NeededAt < now); }
        if (!string.IsNullOrWhiteSpace(filter.Region)) { var region = MatchingRules.Normalize(filter.Region); query = query.Where(n => n.Region.Trim().ToUpper() == region); }
        if (!string.IsNullOrWhiteSpace(filter.Q)) { var term = filter.Q; query = query.Where(n => n.Type.Contains(term) || n.Region.Contains(term) || (n.Location != null && n.Location.Contains(term))); }
        var total = await query.CountAsync();
        filter.Page = Math.Min(filter.Page, Math.Max(1, (total + ListFilter.PageSize - 1) / ListFilter.PageSize));
        var rows = await query.Include(n => n.AssignedResource).InPriorityOrder()
            .Skip((filter.Page - 1) * ListFilter.PageSize).Take(ListFilter.PageSize).ToListAsync();
        return new NeedListViewModel { Needs = rows, Total = total, Filter = filter, Regions = regions };
    }
    private static readonly string[] NeedStatusesForFilter = [NeedStatuses.New, NeedStatuses.UnderReview, NeedStatuses.Assigned, NeedStatuses.Resolved];

    /// <summary>Endrer bare et åpent behov som fortsatt har skjemaets opprinnelige versjon.</summary>
    public async Task<bool> EditAsync(Need need) => await context.Needs.Where(n => n.Id == need.Id && n.Version == need.Version && n.Status != NeedStatuses.Assigned && n.Status != NeedStatuses.Resolved)
        .ExecuteUpdateAsync(update => update.SetProperty(n => n.Type, need.Type.Trim()).SetProperty(n => n.Region, need.Region.Trim())
            .SetProperty(n => n.Location, need.Location).SetProperty(n => n.NeededAt, need.NeededAt).SetProperty(n => n.Priority, need.Priority)
            .SetProperty(n => n.ContactName, need.ContactName).SetProperty(n => n.PhoneNumber, need.PhoneNumber)
            .SetProperty(n => n.Latitude, need.Latitude).SetProperty(n => n.Longitude, need.Longitude)
            .SetProperty(n => n.AreaSouth, need.AreaSouth).SetProperty(n => n.AreaWest, need.AreaWest)
            .SetProperty(n => n.AreaNorth, need.AreaNorth).SetProperty(n => n.AreaEast, need.AreaEast)
            .SetProperty(n => n.Version, n => n.Version + 1)) == 1;

    /// <summary>Henter ett behov med eventuell tildelt ressurs.</summary>
    public Task<Need?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Needs.AsNoTracking().Include(need => need.AssignedResource)
            .SingleOrDefaultAsync(need => need.Id == id, cancellationToken);

    /// <summary>Lagrer behovet og gjenbruker tidligere resultat ved dobbelt registreringskall.</summary>
    public async Task<Need> AddAsync(Need need, CancellationToken cancellationToken = default)
    {
        context.Needs.Add(need);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1062 } && need.CreationToken.HasValue)
        {
            context.Entry(need).State = EntityState.Detached;
            return await context.Needs.AsNoTracking().SingleAsync(n => n.CreatedByUserId == need.CreatedByUserId && n.CreationToken == need.CreationToken, cancellationToken);
        }
        return need;
    }

    /// <summary>Manuell statusendring kan ikke sette eller overskrive en tildeling.</summary>
    public async Task<bool> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default, long? expectedVersion = null)
    {
        if (status is not (NeedStatuses.New or NeedStatuses.UnderReview or NeedStatuses.Resolved))
        {
            return false;
        }

        // Betingelsen sjekkes i selve UPDATE-en, også hvis tildeling skjer samtidig.
        return await context.Needs
            .Where(need => need.Id == id && (!expectedVersion.HasValue || need.Version == expectedVersion.Value) && need.Status != NeedStatuses.Assigned)
            .ExecuteUpdateAsync(update => update.SetProperty(need => need.Status, status).SetProperty(need => need.Version, n => n.Version + 1), cancellationToken) > 0;
    }
}
