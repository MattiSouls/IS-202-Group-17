using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Models;

namespace WebApplicationInAspire.Services;

/// <summary>Enkelt samsvar på type og region, uavhengig av store bokstaver og ytre mellomrom.</summary>
public static class MatchingRules
{
    public static GeoArea? Area(Need need) => GeoArea.From(need.AreaSouth, need.AreaWest, need.AreaNorth, need.AreaEast);
    public static bool HasAreaInput(Need need) => need.AreaSouth.HasValue || need.AreaWest.HasValue || need.AreaNorth.HasValue || need.AreaEast.HasValue;
    public static (double Latitude, double Longitude)? Origin(Need need) => Area(need) is { } area
        ? (area.CenterLatitude, area.CenterLongitude)
        : need.Latitude is double lat && need.Longitude is double lng ? (lat, lng) : null;
    /// <summary>Normaliserer feltene som brukes i matching.</summary>
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
    /// <summary>Løste eller allerede tildelte behov kan ikke få en ny tildeling.</summary>
    public static bool CanAssign(Need need) => need.AssignedResourceId is null
        && need.Status is NeedStatuses.New or NeedStatuses.UnderReview;
}
