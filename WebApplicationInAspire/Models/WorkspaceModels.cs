using WebApplicationInAspire.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationInAspire.Models;

public class ListFilter : SpatialSelection, IValidatableObject
{
    internal bool RejectResults { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => ValidateArea();
    public string? Q { get; set; }
    public string? Region { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public bool Mine { get; set; }
    public int Page { get; set; } = 1;
    public const int PageSize = 10;
    public void Sanitize()
    {
        Q = Q?.Trim()[..Math.Min(Q.Trim().Length, 100)];
        Region = Region?.Trim()[..Math.Min(Region.Trim().Length, 200)];
        Type = Type?.Trim()[..Math.Min(Type.Trim().Length, 100)];
        Page = Math.Clamp(Page, 1, 100000);
    }
}

public class NeedListViewModel
{
    public IReadOnlyList<Need> Needs { get; set; } = [];
    public ListFilter Filter { get; set; } = new();
    public int Total { get; set; }
    public int Pages => Math.Max(1, (Total + ListFilter.PageSize - 1) / ListFilter.PageSize);
    public IReadOnlyList<string> Regions { get; set; } = [];
}

public record AssignmentReview(Need Need, Resource Resource, string Action);
public record AssignmentInfo(Resource Resource, Need? Need);
public record NotificationList(IReadOnlyList<PortalNotification> Items, int Unread, int Page, int Total, string Filter = "all");
public class DashboardViewModel
{
    public bool IsPublic { get; init; }
    public bool IsAdmin { get; init; }
    public bool ShowAllNeeds { get; init; }
    public int Resources { get; init; }
    public int Uncovered { get; init; }
    public int Urgent { get; init; }
    public int TimePassed { get; init; }
    public int Assigned { get; init; }
    public int Unread { get; init; }
    public DateTime SnapshotAt { get; init; }
    public IReadOnlyList<Need> Upcoming { get; init; } = [];
    public IReadOnlyList<PortalNotification> UnreadNotices { get; init; } = [];
    public IReadOnlyList<PortalNotification> Recent { get; init; } = [];
}

public record Paging(int Page, int Total, string Controller, string Action, Dictionary<string, string> Values, int PageSize = 10)
{
    public int Pages => Math.Max(1, (Total + PageSize - 1) / PageSize);
}
