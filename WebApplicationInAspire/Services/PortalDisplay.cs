using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Services;

public static class PortalDisplay
{
    public static readonly string[] Types = ["Aggregat", "Pumpe", "Transport", "Maskin", "Mannskap", "Lokaler", "Samband", "Drone", "Båt", "Drivstoff", "Sand og grus", "Annet"];
    public static readonly string[] Regions = ["Agder", "Akershus", "Buskerud", "Finnmark", "Innlandet", "Møre og Romsdal", "Nordland", "Oslo", "Rogaland", "Telemark", "Troms", "Trøndelag", "Vestfold", "Vestland", "Østfold"];
    public static string Status(string status) => status switch { "New" => "Ny", "UnderReview" => "Vurderes", "Assigned" => "Tildelt", "Resolved" => "Løst", _ => "Ukjent" };
    public static string Priority(string value) => value switch { "Urgent" => "Haster", "High" => "Høy", "Medium" => "Middels", "Low" => "Lav", _ => value };
    public static DateTime NorwayNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo"));
    public static string Availability(Resource resource, bool assigned)
    {
        if (assigned) return "Tildelt";
        if (!resource.IsAvailable) return "Pauset av eier";
        if (resource.AvailableFrom is null || resource.AvailableUntil is null) return "Mangler tidsrom";
        var now = NorwayNow;
        if (resource.AvailableUntil < now) return "Utløpt tidsrom";
        return resource.AvailableFrom > now ? "Tilgjengelig senere" : "Tilgjengelig nå";
    }
    public static double Distance(double latitude, double longitude, double otherLatitude, double otherLongitude)
    {
        const double rad = Math.PI / 180;
        var a = Math.Pow(Math.Sin((otherLatitude - latitude) * rad / 2), 2)
            + Math.Cos(latitude * rad) * Math.Cos(otherLatitude * rad) * Math.Pow(Math.Sin((otherLongitude - longitude) * rad / 2), 2);
        return 6371 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}
