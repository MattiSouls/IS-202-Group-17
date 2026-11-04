using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.DataAccess.Repositories;

/// <summary>Lagrer og leser behov i MariaDB med samme DbContext som ressurser og Identity.</summary>
public class NeedRepository(ApplicationDbContext context) : INeedRepository
{
    public async Task<IReadOnlyList<Need>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Needs.AsNoTracking()
            .OrderBy(need => need.NeededAt)
            .ThenBy(need => need.Id)
            .ToListAsync(cancellationToken);

    public Task<Need?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Needs.AsNoTracking().SingleOrDefaultAsync(need => need.Id == id, cancellationToken);

    public async Task<Need> AddAsync(Need need, CancellationToken cancellationToken = default)
    {
        context.Needs.Add(need);
        await context.SaveChangesAsync(cancellationToken);
        return need;
    }

    /// <summary>Manuell statusendring kan ikke sette eller overskrive en tildeling.</summary>
    public async Task<bool> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default)
    {
        if (status is not (NeedStatuses.New or NeedStatuses.UnderReview or NeedStatuses.Resolved))
        {
            return false;
        }

        // Betingelsen sjekkes i selve UPDATE-en, også hvis tildeling skjer samtidig.
        return await context.Needs
            .Where(need => need.Id == id && need.Status != NeedStatuses.Assigned)
            .ExecuteUpdateAsync(update => update.SetProperty(need => need.Status, status), cancellationToken) > 0;
    }
}
