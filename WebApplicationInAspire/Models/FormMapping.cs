using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Models;

/// <summary>Kopierer kun redigerbare felt fra validerte skjemaer, uten refleksjon eller automatisk binding.</summary>
public static class FormMapping
{
    // ID, eier, registreringsnøkkel, versjon og behovets tildelingsstatus håndteres separat.
    /// <summary>Bruker de samme ressursfeltene ved oppretting og redigering etter validering.</summary>
    public static void ApplyTo(this ResourceViewModel form, Resource resource)
    {
        resource.Name = form.Name;
        resource.Type = form.Type;
        resource.Region = form.Region.Trim();
        resource.IsAvailable = form.IsAvailable;
        resource.AvailableFrom = form.AvailableFrom;
        resource.AvailableUntil = form.AvailableUntil;
        resource.Description = form.Description;
        resource.ContactName = form.ContactName;
        resource.PhoneNumber = form.PhoneNumber;
        if (form.Latitude.HasValue) resource.Latitude = form.Latitude.Value;
        if (form.Longitude.HasValue) resource.Longitude = form.Longitude.Value;
    }

    /// <summary>Kopierer behovets innhold uten å endre eier, versjon, status eller tildeling.</summary>
    public static void ApplyTo(this NeedViewModel form, Need need)
    {
        need.Type = form.Type.Trim();
        need.Region = form.Region.Trim();
        need.Location = form.Location;
        need.NeededAt = form.NeededAt!.Value;
        need.Priority = form.Priority;
        need.ContactName = form.ContactName;
        need.PhoneNumber = form.PhoneNumber;
        need.Latitude = form.Latitude;
        need.Longitude = form.Longitude;
        need.AreaSouth = form.AreaSouth;
        need.AreaWest = form.AreaWest;
        need.AreaNorth = form.AreaNorth;
        need.AreaEast = form.AreaEast;
    }
}
