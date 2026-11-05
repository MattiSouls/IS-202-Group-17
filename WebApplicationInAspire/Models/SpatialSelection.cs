using System.ComponentModel.DataAnnotations;

namespace WebApplicationInAspire.Models;

/// <summary>Et eksplisitt rektangel i WGS84. Grensene inkluderer kanten; datolinjekryssing støttes ikke.</summary>
public record GeoArea(double South, double West, double North, double East)
{
    public double CenterLatitude => (South + North) / 2;
    public double CenterLongitude => (West + East) / 2;
    public static GeoArea? From(double? south, double? west, double? north, double? east)
        => south is double s && west is double w && north is double n && east is double e
            && double.IsFinite(s) && double.IsFinite(w) && double.IsFinite(n) && double.IsFinite(e)
            && s >= -90 && n <= 90 && w >= -180 && e <= 180 && s < n && w < e
            ? new(s, w, n, e) : null;
}

public class SpatialSelection
{
    [Range(-90d, 90d, ErrorMessage = "Breddegrad må være mellom -90 og 90")]
    public double? Latitude { get; set; }
    [Range(-180d, 180d, ErrorMessage = "Lengdegrad må være mellom -180 og 180")]
    public double? Longitude { get; set; }
    public double? AreaSouth { get; set; }
    public double? AreaWest { get; set; }
    public double? AreaNorth { get; set; }
    public double? AreaEast { get; set; }
    public bool HasAreaInput => AreaSouth.HasValue || AreaWest.HasValue || AreaNorth.HasValue || AreaEast.HasValue;
    public GeoArea? Area => GeoArea.From(AreaSouth, AreaWest, AreaNorth, AreaEast);
    public IEnumerable<ValidationResult> ValidateArea()
    {
        if (HasAreaInput && Area is null)
            yield return new ValidationResult("Oppgi fire gyldige områdegrenser: sør må være mindre enn nord og vest mindre enn øst. Området kan ikke krysse datolinjen.", [nameof(AreaSouth), nameof(AreaWest), nameof(AreaNorth), nameof(AreaEast)]);
    }
}
