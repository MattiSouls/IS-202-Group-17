using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.DataAccess;

/// <summary>Felles lesefiltre. IQueryable gjør at filtreringen fortsatt utføres i MariaDB.</summary>
public static class PortalQueries
{
    /// <summary>Eierfilter og offentlige tilbud brukes likt i oversikt, utskrift, CSV og dashbord.</summary>
    public static IQueryable<Resource> VisibleResources(this ApplicationDbContext database, string? ownerId, bool offersOnly)
    {
        var resources = database.Resources.AsNoTracking();
        if (ownerId is not null) resources = resources.Where(resource => resource.OwnerId == ownerId);
        if (offersOnly)
            resources = resources.Where(resource => resource.IsAvailable
                && resource.AvailableFrom != null && resource.AvailableUntil > resource.AvailableFrom
                && !database.Needs.Any(need => need.AssignedResourceId == resource.Id && need.Status == NeedStatuses.Assigned));
        return resources;
    }

    /// <summary>Et udekket behov er åpent og har ingen tildelt ressurs.</summary>
    public static IQueryable<Need> Uncovered(this IQueryable<Need> needs) => needs.Where(need =>
        need.AssignedResourceId == null && (need.Status == NeedStatuses.New || need.Status == NeedStatuses.UnderReview));

    /// <summary>Løste behov sist; ellers prioritet, tidspunkt og ID for stabil paginering.</summary>
    public static IOrderedQueryable<Need> InPriorityOrder(this IQueryable<Need> needs) => needs
        .OrderBy(need => need.Status == NeedStatuses.Resolved)
        .ThenByDescending(need => need.Priority == "Urgent")
        .ThenByDescending(need => need.Priority == "High")
        .ThenByDescending(need => need.Priority == "Medium")
        .ThenBy(need => need.NeededAt)
        .ThenBy(need => need.Id);
}
