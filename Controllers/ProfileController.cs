using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Identity.Profiles;

namespace Fruitables.Controllers;

// Controller quản lý hồ sơ người dùng: xem, sửa, upload/xóa avatar.
// Yêu cầu đăng nhập ([Authorize]).
[Authorize]
public class ProfileController : Controller
{
    private readonly IProfileService _profileService;

    // Inject profile service (CRUD thông tin + avatar)
    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    // GET: /Profile — xem thông tin cá nhân
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _profileService.GetProfileAsync(userId.Value);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction("Index", "Home");
        }

        return View(result.Profile);
    }

    // GET: /Profile/Edit — form chỉnh sửa profile
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _profileService.GetProfileAsync(userId.Value);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        // Map profile sang UpdateProfileRequest để pre-fill form
        var model = new UpdateProfileRequest
        {
            UserId = result.Profile!.Id,
            Name = result.Profile.Name,
            Phone = result.Profile.Phone
        };

        ViewBag.Profile = result.Profile;
        return View(model);
    }

    // POST: /Profile/Edit — lưu thông tin
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateProfileRequest model)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        model.UserId = userId.Value;

        // Validate form
        if (!ModelState.IsValid)
        {
            var profileResult = await _profileService.GetProfileAsync(userId.Value);
            ViewBag.Profile = profileResult.Profile;
            return View(model);
        }

        var result = await _profileService.UpdateProfileAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Cập nhật thất bại");
            var profileResult = await _profileService.GetProfileAsync(userId.Value);
            ViewBag.Profile = profileResult.Profile;
            return View(model);
        }

        TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Profile/UploadAvatar — upload ảnh đại diện
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAvatar(IFormFile avatar)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // Kiểm tra file được chọn
        if (avatar == null || avatar.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn file ảnh";
            return RedirectToAction(nameof(Edit));
        }

        var result = await _profileService.UpdateAvatarAsync(userId.Value, avatar);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Edit));
        }

        TempData["SuccessMessage"] = "Cập nhật avatar thành công!";
        return RedirectToAction(nameof(Edit));
    }

    // POST: /Profile/DeleteAvatar — xóa ảnh đại diện
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAvatar()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _profileService.DeleteAvatarAsync(userId.Value);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Edit));
        }

        TempData["SuccessMessage"] = "Đã xóa avatar!";
        return RedirectToAction(nameof(Edit));
    }

    // Helper: lấy userId từ claims cookie
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}
