using Fruitables.Areas.Admin.Controllers;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Fruitables.Services.Analytics.Dashboard;

namespace Fruitables.Tests;

public class DashboardControllerTests
{
    [Fact]
    public async Task Index_ReturnsDashboardViewWithServiceModel()
    {
        var expected = new DashboardViewModel
        {
            Orders = new OrderStatistics { TodayOrders = 3 },
            RecentOrders = new List<RecentOrderItem>()
        };
        var service = new Mock<IDashboardService>();
        service.Setup(x => x.GetDashboardDataAsync(ChartPeriod.Last7Days, 10)).ReturnsAsync(expected);
        var controller = new DashboardController(service.Object);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(expected, view.Model);
        service.Verify(x => x.GetDashboardDataAsync(ChartPeriod.Last7Days, 10), Times.Once);
    }
}
