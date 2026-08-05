using System.Security.Claims;
using Fruitables.Attributes;
using Fruitables.Services.Returns;
using Fruitables.ViewModels.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using CustomerReturnController = Fruitables.Controllers.ReturnController;
using AdminReturnController = Fruitables.Areas.Admin.Controllers.ReturnController;

namespace Fruitables.Tests;

public sealed class ReturnControllerSecurityTests
{
    [Fact]
    public async Task Customer_details_cannot_read_another_users_request()
    {
        var service = new Mock<IReturnService>();
        service.Setup(x => x.GetCustomerDetailAsync(7, 1100))
            .ReturnsAsync((ReturnDetailViewModel?)null);
        var controller = new CustomerReturnController(service.Object)
        {
            ControllerContext = ControllerContextForUser(1100)
        };

        var result = await controller.Details(7);

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public void Admin_decision_requires_refund_permission()
    {
        var method = typeof(AdminReturnController)
            .GetMethod(nameof(AdminReturnController.Decide));
        var permission = method!.GetCustomAttributes(typeof(RequirePermissionAttribute), true)
            .Cast<RequirePermissionAttribute>()
            .Single();

        Assert.Equal("orders.refund", permission.Permissions.Single());
    }

    [Fact]
    public void Create_post_requires_antiforgery()
    {
        var method = typeof(CustomerReturnController).GetMethods()
            .Single(x => x.Name == nameof(CustomerReturnController.Create)
                && x.GetCustomAttributes(typeof(HttpPostAttribute), true).Any());

        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
    }

    [Fact]
    public void Customer_controller_requires_authentication()
    {
        var attribute = typeof(CustomerReturnController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
    }

    private static ControllerContext ControllerContextForUser(int userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                "test"))
        }
    };
}
