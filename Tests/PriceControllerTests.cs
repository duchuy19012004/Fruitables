using Fruitables.Areas.Admin.Controllers;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Fruitables.Services.Pricing.ProductPricing;

namespace Fruitables.Tests;

public class PriceControllerTests
{
    [Fact]
    public async Task UpdateBasePrice_without_admin_identifier_returns_unauthorized()
    {
        var service = new Mock<IPriceManagementService>();
        var controller = new PriceController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.UpdateBasePrice(new UpdateBasePriceRequest
        {
            ProductId = 1,
            NewPrice = 90_000,
            ExpectedBasePrice = 100_000,
            ExpectedRevision = 1
        });

        Assert.IsType<UnauthorizedResult>(result);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public void Schedule_form_contains_antiforgery_token()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(
            root, "Areas", "Admin", "Views", "Price", "_ScheduleModals.cshtml"));

        var formStart = source.IndexOf("id=\"scheduleForm\"", StringComparison.Ordinal);
        var formEnd = source.IndexOf("</form>", formStart, StringComparison.Ordinal);

        Assert.True(formStart >= 0 && formEnd > formStart);
        Assert.Contains("@Html.AntiForgeryToken()", source[formStart..formEnd]);
    }
}
