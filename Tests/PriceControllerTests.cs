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
}
