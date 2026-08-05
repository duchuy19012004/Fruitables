using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.Constants;
using Fruitables.Models;
using Fruitables.Services.Infrastructure;
using Fruitables.Services.Shipping.Delivery;

namespace Fruitables.ViewComponents
{
    /// <summary>
    /// ViewComponent để đọc settings trong Views
    /// </summary>
    public class SettingsViewComponent : ViewComponent
    {
        private readonly ISettingsService _settingsService;
        private readonly IShippingService _shippingService;

        public SettingsViewComponent(ISettingsService settingsService, IShippingService shippingService)
        {
            _settingsService = settingsService;
            _shippingService = shippingService;
        }

        public async Task<IViewComponentResult> InvokeAsync(string section = "all")
        {
            var model = new SettingsViewModel();

            switch (section.ToLower())
            {
                case "general":
                    model.SiteName = await _settingsService.GetSettingAsync(SettingKeys.SiteName, "Fruitables");
                    model.LogoPath = await _settingsService.GetSettingAsync(SettingKeys.Logo);
                    model.FaviconPath = await _settingsService.GetSettingAsync(SettingKeys.Favicon);
                    break;

                case "seo":
                    model.MetaTitle = await _settingsService.GetSettingAsync(SettingKeys.MetaTitle);
                    model.MetaDescription = await _settingsService.GetSettingAsync(SettingKeys.MetaDescription);
                    model.MetaKeywords = await _settingsService.GetSettingAsync(SettingKeys.MetaKeywords);
                    model.GoogleAnalyticsId = await _settingsService.GetSettingAsync(SettingKeys.GoogleAnalyticsId);
                    break;

                case "contact":
                    model.ContactAddress = await _settingsService.GetSettingAsync(SettingKeys.ContactAddress);
                    model.ContactPhone = await _settingsService.GetSettingAsync(SettingKeys.ContactPhone);
                    model.ContactEmail = await _settingsService.GetSettingAsync(SettingKeys.ContactEmail);
                    model.ContactWorkingHours = await _settingsService.GetSettingAsync(SettingKeys.ContactWorkingHours);
                    break;

                case "social":
                    model.SocialFacebook = await _settingsService.GetSettingAsync(SettingKeys.SocialFacebook);
                    model.SocialTwitter = await _settingsService.GetSettingAsync(SettingKeys.SocialTwitter);
                    model.SocialInstagram = await _settingsService.GetSettingAsync(SettingKeys.SocialInstagram);
                    model.SocialYoutube = await _settingsService.GetSettingAsync(SettingKeys.SocialYoutube);
                    model.SocialLinkedIn = await _settingsService.GetSettingAsync(SettingKeys.SocialLinkedIn);
                    break;

                case "shipping":
                    // Load shipping config using ShippingService
                    // Requirements 1.1: Load shipping settings for display
                    var shippingConfig = await _shippingService.GetShippingConfigAsync();
                    model.ShippingConfig = shippingConfig;
                    break;

                default: // all
                    model.SiteName = await _settingsService.GetSettingAsync(SettingKeys.SiteName, "Fruitables");
                    model.LogoPath = await _settingsService.GetSettingAsync(SettingKeys.Logo);
                    model.FaviconPath = await _settingsService.GetSettingAsync(SettingKeys.Favicon);
                    model.MetaTitle = await _settingsService.GetSettingAsync(SettingKeys.MetaTitle);
                    model.MetaDescription = await _settingsService.GetSettingAsync(SettingKeys.MetaDescription);
                    model.MetaKeywords = await _settingsService.GetSettingAsync(SettingKeys.MetaKeywords);
                    model.GoogleAnalyticsId = await _settingsService.GetSettingAsync(SettingKeys.GoogleAnalyticsId);
                    model.ContactAddress = await _settingsService.GetSettingAsync(SettingKeys.ContactAddress);
                    model.ContactPhone = await _settingsService.GetSettingAsync(SettingKeys.ContactPhone);
                    model.ContactEmail = await _settingsService.GetSettingAsync(SettingKeys.ContactEmail);
                    model.ContactWorkingHours = await _settingsService.GetSettingAsync(SettingKeys.ContactWorkingHours);
                    model.SocialFacebook = await _settingsService.GetSettingAsync(SettingKeys.SocialFacebook);
                    model.SocialTwitter = await _settingsService.GetSettingAsync(SettingKeys.SocialTwitter);
                    model.SocialInstagram = await _settingsService.GetSettingAsync(SettingKeys.SocialInstagram);
                    model.SocialYoutube = await _settingsService.GetSettingAsync(SettingKeys.SocialYoutube);
                    model.SocialLinkedIn = await _settingsService.GetSettingAsync(SettingKeys.SocialLinkedIn);
                    // Also load shipping config for "all" section
                    model.ShippingConfig = await _shippingService.GetShippingConfigAsync();
                    break;
            }

            return View(model);
        }
    }

    public class SettingsViewModel
    {
        // General
        public string? SiteName { get; set; }
        public string? LogoPath { get; set; }
        public string? FaviconPath { get; set; }

        // SEO
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public string? GoogleAnalyticsId { get; set; }

        // Contact
        public string? ContactAddress { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactWorkingHours { get; set; }

        // Social
        public string? SocialFacebook { get; set; }
        public string? SocialTwitter { get; set; }
        public string? SocialInstagram { get; set; }
        public string? SocialYoutube { get; set; }
        public string? SocialLinkedIn { get; set; }

        // Shipping
        public ShippingConfig? ShippingConfig { get; set; }
    }
}
