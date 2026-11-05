using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WebApplicationInAspire.Controllers;
using WebApplicationInAspire.DataAccess.Repositories;
using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Tests;

public class ResourceControllerTests
{
    private readonly IResourceRepository repository = Substitute.For<IResourceRepository>();
    private ResourceController Controller(string owner = "owner", string role = "ResourceProvider")
    {
        var controller = new ResourceController(repository, NullLogger<ResourceController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, owner), new Claim(ClaimTypes.Role, role)], "test"))
        }};
        return controller;
    }

    [Fact]
    public async Task Registration_uses_authenticated_owner_and_persists_period()
    {
        var model = ValidationTests.ValidResource();
        var result = await Controller().Register(model);
        Assert.IsType<RedirectToActionResult>(result);
        await repository.Received(1).AddAsync(Arg.Is<Resource>(resource => resource.OwnerId == "owner"
            && resource.AvailableFrom == model.AvailableFrom && resource.AvailableUntil == model.AvailableUntil));
    }

    [Fact]
    public async Task Invalid_model_is_not_written()
    {
        var controller = Controller(); controller.ModelState.AddModelError("AvailableUntil", "Ugyldig tidsrom");
        Assert.IsType<ViewResult>(await controller.Register(ValidationTests.ValidResource()));
        await repository.DidNotReceive().AddAsync(Arg.Any<Resource>());
    }

    [Fact]
    public async Task Another_owner_cannot_edit_or_delete()
    {
        repository.GetByIdAsync(1).Returns(new Resource { Id = 1, OwnerId = "someone-else" });
        var model = ValidationTests.ValidResource(); model.Id = 1;
        Assert.IsType<ForbidResult>(await Controller().Edit(1, model));
        Assert.IsType<ForbidResult>(await Controller().DeleteConfirmed(1));
        await repository.DidNotReceive().UpdateAsync(Arg.Any<Resource>());
        await repository.DidNotReceive().DeleteAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task Edit_requires_route_and_model_id_to_agree()
    {
        var model = ValidationTests.ValidResource(); model.Id = 2;
        Assert.IsType<BadRequestResult>(await Controller().Edit(1, model));
        await repository.DidNotReceive().GetByIdAsync(Arg.Any<int>());
    }

    [Theory]
    [InlineData("ResourceProvider", "owner", false)]
    [InlineData("PublicUser", null, true)] [InlineData("Admin", null, true)]
    public async Task Overview_passes_role_scope_to_repository(string role, string? owner, bool availableOnly)
    {
        repository.GetForOverviewAsync(Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string?>())
            .Returns(Array.Empty<Resource>());
        repository.GetAssignedIdsAsync().Returns(new HashSet<int>());
        await Controller(role: role).Overview("Agder");
        await repository.Received(1).GetForOverviewAsync(owner, availableOnly, "Agder");
    }

    [Fact]
    public async Task Assignment_conflict_returns_409_instead_of_overwriting_resource()
    {
        repository.GetByIdAsync(1).Returns(new Resource { Id = 1, OwnerId = "owner" });
        repository.UpdateAsync(Arg.Any<Resource>()).Returns(false);
        repository.GetAssignedIdsAsync().Returns(new HashSet<int> { 1 });
        var controller = Controller(); var model = ValidationTests.ValidResource(); model.Id = 1;
        Assert.IsType<ViewResult>(await controller.Edit(1, model));
        Assert.Equal(409, controller.Response.StatusCode);
        Assert.False(controller.ModelState.IsValid);
    }
}
