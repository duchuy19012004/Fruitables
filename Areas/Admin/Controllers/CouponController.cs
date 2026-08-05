using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Pricing.Coupons;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class CouponController : Controller
{
    private readonly ICouponService _couponService;

    public CouponController(ICouponService couponService)
    {
        _couponService = couponService;
    }

    // GET: Admin/Coupon
    public async Task<IActionResult> Index()
    {
        var coupons = await _couponService.GetAllAsync();
        return View(new CouponListViewModel { Coupons = coupons });
    }

    // GET: Admin/Coupon/Create
    public IActionResult Create()
    {
        return View(new CreateCouponViewModel());
    }

    // POST: Admin/Coupon/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCouponViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (success, error) = await _couponService.CreateAsync(model);
        if (!success)
        {
            ModelState.AddModelError("", error ?? "Có lỗi xảy ra");
            return View(model);
        }

        TempData["Success"] = "Tạo mã giảm giá thành công!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Admin/Coupon/Edit/{id}
    public async Task<IActionResult> Edit(int id)
    {
        var coupon = await _couponService.GetByIdAsync(id);
        if (coupon == null)
        {
            TempData["Error"] = "Không tìm thấy mã giảm giá";
            return RedirectToAction(nameof(Index));
        }

        var model = new EditCouponViewModel
        {
            Id             = coupon.Id,
            Code           = coupon.Code,
            Type           = coupon.Type,
            Value          = coupon.Value,
            MinOrderAmount = coupon.MinOrderAmount,
            MinQuantity    = coupon.MinQuantity,
            MaxUses        = coupon.MaxUses,
            StartDate      = coupon.StartDate,
            EndDate        = coupon.EndDate,
            IsActive       = coupon.IsActive,
            UsedCount      = coupon.UsedCount
        };
        return View(model);
    }

    // POST: Admin/Coupon/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditCouponViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        var (success, error) = await _couponService.UpdateAsync(id, model);
        if (!success)
        {
            ModelState.AddModelError("", error ?? "Có lỗi xảy ra");
            return View(model);
        }

        TempData["Success"] = "Cập nhật mã giảm giá thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: Admin/Coupon/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _couponService.DeleteAsync(id);
        if (!success)
            TempData["Error"] = error;
        else
            TempData["Success"] = "Xóa mã giảm giá thành công!";

        return RedirectToAction(nameof(Index));
    }
}
