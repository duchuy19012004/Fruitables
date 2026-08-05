using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.Constants;
using Fruitables.ViewModels;
using Fruitables.Services.Infrastructure;

namespace Fruitables.ViewComponents;

public class FeaturesBannerViewComponent : ViewComponent
{
    private readonly ISettingsService _settingsService;

    public FeaturesBannerViewComponent(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new BannerSettingsViewModel
        {
            Banner1 = new BannerItemViewModel
            {
                Title = await _settingsService.GetSettingAsync(SettingKeys.Banner1Title) ?? "Fresh Apples",
                Subtitle = await _settingsService.GetSettingAsync(SettingKeys.Banner1Subtitle) ?? "20% OFF",
                ImagePath = await _settingsService.GetSettingAsync(SettingKeys.Banner1Image) ?? "/img/featur-1.jpg",
                Link = await _settingsService.GetSettingAsync(SettingKeys.Banner1Link) ?? "#"
            },
            Banner2 = new BannerItemViewModel
            {
                Title = await _settingsService.GetSettingAsync(SettingKeys.Banner2Title) ?? "Tasty Fruits",
                Subtitle = await _settingsService.GetSettingAsync(SettingKeys.Banner2Subtitle) ?? "Free delivery",
                ImagePath = await _settingsService.GetSettingAsync(SettingKeys.Banner2Image) ?? "/img/featur-2.jpg",
                Link = await _settingsService.GetSettingAsync(SettingKeys.Banner2Link) ?? "#"
            },
            Banner3 = new BannerItemViewModel
            {
                Title = await _settingsService.GetSettingAsync(SettingKeys.Banner3Title) ?? "Exotic Vegetables",
                Subtitle = await _settingsService.GetSettingAsync(SettingKeys.Banner3Subtitle) ?? "Discount 30$",
                ImagePath = await _settingsService.GetSettingAsync(SettingKeys.Banner3Image) ?? "/img/featur-3.jpg",
                Link = await _settingsService.GetSettingAsync(SettingKeys.Banner3Link) ?? "#"
            }
        };

        return View("Default", model);
    }
}
