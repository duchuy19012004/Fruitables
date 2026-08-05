using Fruitables.Controllers;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Fruitables.Services.Identity.Authentication;
using Fruitables.Services.Identity.Users;

namespace Fruitables.Tests;

public class AccountControllerTests
{
    private static AccountController CreateController()
    {
        var userAuthService = new Mock<IUserAuthService>();
        var googleAuthService = new Mock<IGoogleAuthService>();
        googleAuthService.Setup(s => s.IsGoogleAuthEnabledAsync()).ReturnsAsync(false);

        return new AccountController(userAuthService.Object, googleAuthService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void Login_Get_ReturnsAuthViewWithLoginTabActive()
    {
        var controller = CreateController();

        var result = controller.Login();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Auth", viewResult.ViewName);
        var model = Assert.IsType<AuthPageViewModel>(viewResult.Model);
        Assert.Equal("login", controller.ViewBag.ActiveTab);
        Assert.NotNull(model.Login);
    }

    [Fact]
    public void Register_Get_ReturnsAuthViewWithRegisterTabActive()
    {
        var controller = CreateController();

        var result = controller.Register();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Auth", viewResult.ViewName);
        var model = Assert.IsType<AuthPageViewModel>(viewResult.Model);
        Assert.Equal("register", controller.ViewBag.ActiveTab);
        Assert.NotNull(model.Register);
    }

    [Fact]
    public void Login_Post_BindsNestedLoginFormFields()
    {
        var method = typeof(AccountController).GetMethod(nameof(AccountController.Login), new[] { typeof(LoginRequest), typeof(string) });

        var bind = Assert.IsType<BindAttribute>(method!.GetParameters()[0].GetCustomAttributes(typeof(BindAttribute), false).Single());
        Assert.Equal("Login", bind.Prefix);
    }

    [Fact]
    public void Register_Post_BindsNestedRegisterFormFields()
    {
        var method = typeof(AccountController).GetMethod(nameof(AccountController.Register), new[] { typeof(RegisterRequest) });

        var bind = Assert.IsType<BindAttribute>(method!.GetParameters()[0].GetCustomAttributes(typeof(BindAttribute), false).Single());
        Assert.Equal("Register", bind.Prefix);
    }
}
