using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Controllers;

// Controller xử lý đăng ký, đăng nhập, đăng xuất, quên/đặt lại mật khẩu, và Google OAuth.
public class AccountController : Controller
{
    private readonly IUserAuthService _userAuthService;
    private readonly IGoogleAuthService _googleAuthService;

    // Inject service xác thực: login cơ bản + Google OAuth
    public AccountController(IUserAuthService userAuthService, IGoogleAuthService googleAuthService)
    {
        _userAuthService = userAuthService;
        _googleAuthService = googleAuthService;
    }

    // GET: Hiển thị form đăng ký
    [HttpGet]
    public IActionResult Register()
    {
        // Nếu đã đăng nhập → chuyển về trang chủ
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }

        ViewBag.ActiveTab = "register";
        return View("Auth", new AuthPageViewModel { Register = new RegisterRequest() });
    }

    // POST: Xử lý đăng ký
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([Bind(Prefix = "Register")] RegisterRequest model)
    {
        // Nếu đã đăng nhập → bỏ qua
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }

        // Validate form
        if (!ModelState.IsValid)
        {
            ViewBag.ActiveTab = "register";
            return View("Auth", new AuthPageViewModel { Register = model });
        }

        // Gọi service đăng ký
        var result = await _userAuthService.RegisterAsync(model);

        // Đăng ký thất bại → hiện lỗi
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Đăng ký thất bại");
            ViewBag.ActiveTab = "register";
            return View("Auth", new AuthPageViewModel { Register = model });
        }

        // Thành công → hiện thông báo, chuyển sang trang login
        TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    // GET: Hiển thị form đăng nhập
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // Nếu đã đăng nhập → chuyển về trang chủ
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }

        // Lưu returnUrl để redirect sau khi login thành công
        ViewBag.ReturnUrl = returnUrl;
        ViewBag.ActiveTab = "login";
        return View("Auth", new AuthPageViewModel { Login = new LoginRequest() });
    }

    // POST: Xử lý đăng nhập
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([Bind(Prefix = "Login")] LoginRequest model, string? returnUrl = null)
    {
        // Nếu đã đăng nhập → bỏ qua
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }

        // Validate form
        if (!ModelState.IsValid)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.ActiveTab = "login";
            return View("Auth", new AuthPageViewModel { Login = model });
        }

        // Gọi service kiểm tra email + password
        var result = await _userAuthService.LoginAsync(model.Email, model.Password);

        // Sai thông tin → hiện lỗi
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Đăng nhập thất bại");
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.ActiveTab = "login";
            return View("Auth", new AuthPageViewModel { Login = model });
        }

        // Login thành công → tạo claims cho cookie
        var user = result.User!;

        // Claims: thông tin user lưu trong cookie (id, email, tên, role)
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        // Tạo identity từ claims + scheme cookie
        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        // Cấu hình cookie: remember me → 30 ngày, không thì 24h
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(24)
        };

        // Ghi cookie vào session
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        TempData["SuccessMessage"] = $"Chào mừng {user.Name}! Đăng nhập thành công.";

        // Nếu có returnUrl hợp lệ → redirect về đó (vd: trang đang xem trước khi bị đá ra login)
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        // Không có returnUrl → redirect theo role (Admin → admin dashboard, Customer → home)
        var redirectUrl = _userAuthService.GetRedirectUrlByRole(user.Role);
        return Redirect(redirectUrl);
    }

    // POST: Đăng xuất — xóa cookie
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // GET: Bắt đầu flow đăng nhập Google — redirect sang Google
    [HttpGet]
    public async Task<IActionResult> GoogleLogin(string? returnUrl = null)
    {
        // Nếu đã đăng nhập → bỏ qua
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToHome();

        // Kiểm tra Google OAuth có được bật trong settings không
        if (!await _googleAuthService.IsGoogleAuthEnabledAsync())
        {
            TempData["ErrorMessage"] = "Đăng nhập Google chưa được bật. Vui lòng liên hệ quản trị viên.";
            return RedirectToAction(nameof(Login));
        }

        // Tạo URL callback khi Google trả về + redirect sang Google
        var redirectUrl = Url.Action(nameof(GoogleCallback), "Account", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    // GET: Callback từ Google sau khi user đồng ý
    [HttpGet]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = null)
    {
        // Lấy kết quả xác thực từ Google
        var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

        // User hủy đăng nhập → quay lại trang login
        if (!authenticateResult.Succeeded)
        {
            TempData["ErrorMessage"] = "Đăng nhập bị hủy";
            return RedirectToAction(nameof(Login));
        }

        // Extract thông tin từ Google claims (email, googleId, name)
        var claims = authenticateResult.Principal?.Claims;
        var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var googleId = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        // Thiếu thông tin bắt buộc → lỗi
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(googleId))
        {
            TempData["ErrorMessage"] = "Không thể lấy thông tin từ Google";
            return RedirectToAction(nameof(Login));
        }

        // Gọi service: nếu email đã tồn tại → link Google ID, nếu chưa → tạo user mới
        var result = await _googleAuthService.ProcessGoogleLoginAsync(email, name, googleId);

        // Xử lý thất bại
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Login));
        }

        // Tạo cookie đăng nhập (giống flow login thường)
        var user = result.User!;

        var userClaims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(userClaims, CookieAuthenticationDefaults.AuthenticationScheme);

        // Google login luôn remember 30 ngày
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        TempData["SuccessMessage"] = $"Chào mừng {user.Name}! Đăng nhập thành công.";

        // Nếu có returnUrl hợp lệ → redirect về đó
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        // Redirect theo role (Admin → admin dashboard, Customer → home)
        var redirectUrl = _userAuthService.GetRedirectUrlByRole(user.Role);
        return Redirect(redirectUrl);
    }

    // Helper: redirect về trang chủ (dùng khi user đã đăng nhập mà vẫn vào login/register)
    private IActionResult RedirectToHome()
    {
        return RedirectToAction("Index", "Home");
    }

    // GET: Hiển thị form quên mật khẩu
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        // Nếu đã đăng nhập → chuyển về trang chủ
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToHome();

        return View(new ForgotPasswordRequest());
    }

    // POST: Gửi email đặt lại mật khẩu
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest model)
    {
        // Validate form
        if (!ModelState.IsValid)
            return View(model);

        // Tạo URL callback cho link đặt lại mật khẩu trong email
        var resetCallbackUrl = Url.Action(nameof(ResetPassword), "Account",
            values: null, protocol: Request.Scheme)!;

        // Gọi service: kiểm tra email tồn tại → tạo token → gửi email
        await _userAuthService.GeneratePasswordResetTokenAsync(model.Email, resetCallbackUrl);

        // Luôn hiện success để tránh lộ thông tin email có tồn tại hay không (email enumeration)
        TempData["SuccessMessage"] = "Nếu email tồn tại trong hệ thống, chúng tôi đã gửi hướng dẫn đặt lại mật khẩu. Vui lòng kiểm tra hộp thư của bạn.";
        return RedirectToAction(nameof(ForgotPassword));
    }

    // GET: Hiển thị form đặt lại mật khẩu (từ link trong email)
    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        // Thiếu email hoặc token → link không hợp lệ
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            TempData["ErrorMessage"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
            return RedirectToAction(nameof(Login));
        }

        // Truyền email + token vào form để POST xử lý
        return View(new ResetPasswordRequest { Email = email, Token = token });
    }

    // POST: Xử lý đặt lại mật khẩu
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest model)
    {
        // Validate form
        if (!ModelState.IsValid)
            return View(model);

        // Gọi service: kiểm tra token hợp lệ → cập nhật mật khẩu mới
        var success = await _userAuthService.ResetPasswordAsync(model);

        // Token hết hạn hoặc không hợp lệ
        if (!success)
        {
            ModelState.AddModelError(string.Empty, "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng thử lại.");
            return View(model);
        }

        // Thành công → redirect về login
        TempData["SuccessMessage"] = "Mật khẩu đã được đặt lại thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }
}
