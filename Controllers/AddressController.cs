using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Models;
using Fruitables.Services.Interfaces;

namespace Fruitables.Controllers;

// Controller quản lý địa chỉ giao hàng của user: CRUD + đặt mặc định.
// Yêu cầu đăng nhập ([Authorize]).
[Authorize]
public class AddressController : Controller
{
    private readonly IAddressService _addressService;
    private readonly IProfileService _profileService;
    private readonly IVietnamAddressService _vietnamAddressService;
    private readonly IGhnService _ghnService;

    // Inject 4 service: address CRUD, profile, address VN, GHN code lookup
    public AddressController(
        IAddressService addressService, 
        IProfileService profileService,
        IVietnamAddressService vietnamAddressService,
        IGhnService ghnService)
    {
        _addressService = addressService;
        _profileService = profileService;
        _vietnamAddressService = vietnamAddressService;
        _ghnService = ghnService;
    }

    // GET: Danh sách địa chỉ của user
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var addresses = await _addressService.GetUserAddressesAsync(userId.Value);
        return View(addresses);
    }

    // GET: Form thêm địa chỉ mới
    public async Task<IActionResult> Create()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // Pre-fill tên + SĐT từ profile nếu có
        var address = new Address();
        var profileResult = await _profileService.GetProfileAsync(userId.Value);
        if (profileResult.Success && profileResult.Profile != null)
        {
            address.FullName = profileResult.Profile.Name ?? string.Empty;
            address.Phone = profileResult.Profile.Phone ?? string.Empty;
        }

        return View(address);
    }

    // POST: Xử lý thêm địa chỉ
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Address address)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // Validate form
        if (!ModelState.IsValid)
        {
            return View(address);
        }

        try
        {
            // Sanitize địa chỉ để chống XSS
            address.StreetAddress = _vietnamAddressService.SanitizeStreetAddress(address.StreetAddress);
            await FillGhnCodesAsync(address);
            
            var setAsDefault = address.IsDefault;
            address.UserId = userId.Value;
            // Để service xử lý: địa chỉ đầu tiên tự động là mặc định
            address.IsDefault = false;
            
            // Tạo địa chỉ mới
            var created = await _addressService.CreateAddressAsync(address);
            
            // Nếu user chọn "đặt làm mặc định" → gọi service set default
            if (setAsDefault)
            {
                await _addressService.SetDefaultAddressAsync(userId.Value, created.Id);
            }
            
            TempData["SuccessMessage"] = "Địa chỉ đã được thêm thành công.";
            return RedirectToAction(nameof(Index));
        }
        // Lỗi business logic từ service
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(address);
        }
    }

    // GET: Form chỉnh sửa địa chỉ
    public async Task<IActionResult> Edit(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var address = await _addressService.GetAddressByIdAsync(id);
        // Kiểm tra tồn tại + thuộc về user hiện tại
        if (address == null || address.UserId != userId.Value)
        {
            return NotFound();
        }

        return View(address);
    }

    // POST: Xử lý chỉnh sửa địa chỉ
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Address address)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // ID trong URL phải khớp với ID trong form
        if (id != address.Id)
        {
            return BadRequest();
        }

        // Kiểm tra quyền sở hữu
        var existing = await _addressService.GetAddressByIdAsync(id);
        if (existing == null || existing.UserId != userId.Value)
        {
            return NotFound();
        }

        // Validate form
        if (!ModelState.IsValid)
        {
            return View(address);
        }

        try
        {
            // Sanitize địa chỉ chống XSS
            address.StreetAddress = _vietnamAddressService.SanitizeStreetAddress(address.StreetAddress);
            await FillGhnCodesAsync(address);
            
            var setAsDefault = address.IsDefault;
            address.UserId = userId.Value;
            // Giữ nguyên trạng thái default hiện tại khi update
            address.IsDefault = existing.IsDefault;
            
            // Cập nhật địa chỉ
            await _addressService.UpdateAddressAsync(address);
            
            // Nếu user chọn "đặt làm mặc định" và trước đó chưa phải default
            if (setAsDefault && !existing.IsDefault)
            {
                await _addressService.SetDefaultAddressAsync(userId.Value, id);
            }
            
            TempData["SuccessMessage"] = "Địa chỉ đã được cập nhật thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(address);
        }
    }

    // POST: Xóa địa chỉ
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // Kiểm tra tồn tại + quyền sở hữu
        var address = await _addressService.GetAddressByIdAsync(id);
        if (address == null || address.UserId != userId.Value)
        {
            return NotFound();
        }

        var result = await _addressService.DeleteAddressAsync(id);
        if (result)
        {
            TempData["SuccessMessage"] = "Địa chỉ đã được xóa thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = "Không thể xóa địa chỉ.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Đặt địa chỉ mặc định (hỗ trợ cả AJAX và form submit)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            // AJAX request → trả JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = "Unauthorized" });
            return RedirectToAction("Login", "Account");
        }

        var address = await _addressService.GetAddressByIdAsync(id);
        if (address == null || address.UserId != userId.Value)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = "Không tìm thấy địa chỉ" });
            return NotFound();
        }

        var result = await _addressService.SetDefaultAddressAsync(userId.Value, id);
        
        // AJAX → trả JSON
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { 
                success = result, 
                message = result ? "Địa chỉ mặc định đã được cập nhật." : "Không thể đặt địa chỉ mặc định."
            });
        }
        
        // Form submit → redirect với thông báo
        if (result)
        {
            TempData["SuccessMessage"] = "Địa chỉ mặc định đã được cập nhật.";
        }
        else
        {
            TempData["ErrorMessage"] = "Không thể đặt địa chỉ mặc định.";
        }

        return RedirectToAction(nameof(Index));
    }
    
    // POST: Xóa địa chỉ bằng AJAX (trả JSON, kèm ID địa chỉ mặc định mới nếu có)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAjax(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Json(new { success = false, message = "Unauthorized" });
        }

        // Kiểm tra tồn tại + quyền sở hữu
        var address = await _addressService.GetAddressByIdAsync(id);
        if (address == null || address.UserId != userId.Value)
        {
            return Json(new { success = false, message = "Không tìm thấy địa chỉ" });
        }

        var result = await _addressService.DeleteAddressAsync(id);
        
        // Sau khi xóa, lấy địa chỉ mặc định mới (nếu có) để frontend cập nhật UI
        int? newDefaultId = null;
        if (result)
        {
            var defaultAddress = await _addressService.GetDefaultAddressAsync(userId.Value);
            newDefaultId = defaultAddress?.Id;
        }
        
        return Json(new { 
            success = result, 
            message = result ? "Địa chỉ đã được xóa thành công." : "Không thể xóa địa chỉ.",
            newDefaultId = newDefaultId
        });
    }

    // Helper: Lấy userId từ claims trong cookie
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // Parse string → int, trả null nếu không hợp lệ
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }

    private async Task FillGhnCodesAsync(Address address)
    {
        var code = await _ghnService.ResolveAddressAsync(address.ProvinceName, address.CommuneName);
        address.GhnDistrictId = code?.DistrictId;
        address.GhnWardCode = code?.WardCode;
    }
}
