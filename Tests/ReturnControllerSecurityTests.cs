using System.Reflection;
using System.Security.Claims;
using Fruitables.Attributes;
using Fruitables.Controllers;
using Fruitables.Data;
using Fruitables.Filters;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AdminReturnController = Fruitables.Areas.Admin.Controllers.ReturnController;

namespace Fruitables.Tests;

public class ReturnControllerSecurityTests
{
    [Fact]
    public async Task CustomerCannotReadAnotherUsersReturnRequest()
    {
        var returns = new Mock<IReturnService>();
        returns.Setup(x => x.GetForCustomerAsync(42, 10, It.IsAny<CancellationToken>())).ReturnsAsync((Models.Returns.ReturnRequest?)null);
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller = new ReturnController(returns.Object, Mock.Of<IReturnEligibilityService>(), Mock.Of<IReturnEvidenceService>(), Mock.Of<IRefundService>(), db)
        {
            ControllerContext = Context(10, "Customer")
        };
        Assert.IsType<NotFoundResult>(await controller.Details(42));
    }

    [Fact]
    public async Task CustomerDestinationPost_UsesAuthenticatedUserId()
    {
        var refunds = new Mock<IRefundService>();
        refunds.Setup(x => x.SaveDestinationAsync(
                7,
                10,
                It.IsAny<RefundDestinationInputViewModel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)null));
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller = new ReturnController(
            Mock.Of<IReturnService>(),
            Mock.Of<IReturnEligibilityService>(),
            Mock.Of<IReturnEvidenceService>(),
            refunds.Object,
            db)
        {
            ControllerContext = Context(10, "Customer"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.SaveRefundDestination(new RefundDestinationInputViewModel
        {
            RefundId = 7,
            ReturnRequestId = 42,
            BankCode = "VCB",
            AccountNumber = "0123456789",
            AccountHolder = "NGUYEN VAN A"
        });

        refunds.Verify(x => x.SaveDestinationAsync(
            7,
            10,
            It.IsAny<RefundDestinationInputViewModel>(),
            It.IsAny<CancellationToken>()));
        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public void EveryReturnPostActionRequiresAntiforgery()
    {
        var postMethods = new[] { typeof(ReturnController), typeof(AdminReturnController) }
            .SelectMany(x => x.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(x => x.GetCustomAttribute<HttpPostAttribute>() != null)
            .ToList();
        Assert.NotEmpty(postMethods);
        Assert.All(postMethods, method => Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
    }

    [Fact]
    public void AdminReturnRoutesRequireAdminRoleAndActionPermissions()
    {
        var authorize = Assert.Single(typeof(AdminReturnController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("Admin,SuperAdmin", authorize.Roles);
        var sensitive = typeof(AdminReturnController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(x => x.Name is not nameof(AdminReturnController.Index) and not nameof(AdminReturnController.Detail))
            .Where(x => x.GetCustomAttribute<HttpPostAttribute>() != null);
        Assert.All(sensitive, method => Assert.NotEmpty(method.GetCustomAttributes<RequirePermissionAttribute>()));
        Assert.Null(typeof(AdminReturnController).GetMethod("CreateRefund"));
        Assert.Null(typeof(AdminReturnController).GetMethod("ConfirmRefund"));
        Assert.Null(typeof(AdminReturnController).GetMethod("UpdateResolution"));
        Assert.Null(typeof(AdminReturnController).GetMethod("RecordDisposition"));
    }

    [Fact]
    public async Task MissingReturnsPermissionProducesForbid()
    {
        var rbac = new Mock<IRbacService>();
        rbac.Setup(x => x.HasAnyPermissionAsync(10, It.IsAny<string[]>())).ReturnsAsync(false);
        var filter = new RequirePermissionFilter(rbac.Object, NullLogger<RequirePermissionFilter>.Instance);
        var descriptor = new ActionDescriptor
        {
            DisplayName = "Admin Return Index",
            EndpointMetadata = new List<object> { new RequirePermissionAttribute("returns.view") }
        };
        var http = Context(10, "Admin").HttpContext;
        var action = new ActionContext(http, new RouteData(), descriptor);
        var context = new AuthorizationFilterContext(action, new List<IFilterMetadata>());
        await filter.OnAuthorizationAsync(context);
        Assert.IsType<ForbidResult>(context.Result);
    }

    private static ControllerContext Context(int userId, string role)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role) }, "Test");
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }
}
