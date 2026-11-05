using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Services;

/// <summary>Enkelt samsvar på type og region, uavhengig av store bokstaver og ytre mellomrom.</summary>
public static class MatchingRules
{
    /// <summary>Normaliserer feltene som brukes i matching.</summary>
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
    /// <summary>Løste eller allerede tildelte behov kan ikke få en ny tildeling.</summary>
    public static bool CanAssign(Need need) => need.AssignedResourceId is null
        && need.Status is NeedStatuses.New or NeedStatuses.UnderReview;
}
