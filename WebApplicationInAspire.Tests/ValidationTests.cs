using System.ComponentModel.DataAnnotations;
using WebApplicationInAspire.Models;
using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Services;

namespace WebApplicationInAspire.Tests;

public class ValidationTests
{
    public static ResourceViewModel ValidResource() => new()
    {
        Name = "Vannpumpe", Type = "Pumpe", Region = "Agder", ContactName = "Testperson",
        PhoneNumber = "+4799999999", Latitude = 58.1467, Longitude = 7.9956,
        AvailableFrom = new DateTime(2026, 11, 1, 8, 0, 0),
        AvailableUntil = new DateTime(2026, 11, 30, 18, 0, 0)
    };

    private static List<ValidationResult> Errors(object model)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), errors, true);
        return errors;
    }

    [Fact]
    public void Resource_accepts_valid_period_and_coordinates() => Assert.Empty(Errors(ValidResource()));

    [Theory]
    [InlineData(null, 5)] [InlineData(1, null)] [InlineData(5, 1)] [InlineData(1, 1)]
    public void Resource_rejects_missing_or_reversed_period(int? start, int? end)
    {
        var model = ValidResource();
        model.AvailableFrom = start.HasValue ? new DateTime(2026, 11, start.Value) : null;
        model.AvailableUntil = end.HasValue ? new DateTime(2026, 11, end.Value) : null;
        Assert.NotEmpty(Errors(model));
    }

    [Theory]
    [InlineData(null, 8d)] [InlineData(58d, null)] [InlineData(91d, 8d)] [InlineData(58d, 181d)]
    [InlineData(-91d, 8d)] [InlineData(58d, -181d)]
    public void Resource_rejects_missing_or_invalid_coordinates(double? latitude, double? longitude)
    {
        var model = ValidResource(); model.Latitude = latitude; model.Longitude = longitude;
        Assert.NotEmpty(Errors(model));
    }

    [Theory]
    [InlineData("Name", 201)] [InlineData("Type", 101)] [InlineData("Region", 201)]
    [InlineData("ContactName", 201)] [InlineData("Description", 4001)]
    public void Resource_rejects_oversized_fields(string field, int length)
    {
        var model = ValidResource();
        typeof(ResourceViewModel).GetProperty(field)!.SetValue(model, new string('x', length));
        Assert.NotEmpty(Errors(model));
    }

    [Fact]
    public void Resource_rejects_date_outside_MariaDB_range()
    {
        var model = ValidResource(); model.AvailableFrom = DateTime.MinValue;
        Assert.Contains(Errors(model), error => error.MemberNames.Contains(nameof(model.AvailableFrom)));
    }

    [Theory]
    [InlineData("New", null, true)] [InlineData("UnderReview", null, true)]
    [InlineData("Assigned", 1, false)] [InlineData("Resolved", null, false)]
    [InlineData("New", 1, false)] [InlineData("invalid", null, false)]
    public void Matching_only_accepts_unassigned_open_needs(string status, int? assigned, bool expected)
        => Assert.Equal(expected, MatchingRules.CanAssign(new Need { Status = status, AssignedResourceId = assigned }));

    [Theory]
    [InlineData("  aGdEr ", "AGDER")] [InlineData(" Pumpe", "PUMPE")]
    public void Matching_normalizes_type_and_region(string input, string expected)
        => Assert.Equal(expected, MatchingRules.Normalize(input));
}
