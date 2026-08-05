using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Constants;
using Fruitables.Models;
using Fruitables.Areas.Admin.ViewModels;
using System.Text.Json;
using Fruitables.Services.Infrastructure;
using Fruitables.Services.Shipping.Delivery;

namespace Fruitables.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;
        private readonly IShippingService _shippingService;

        public SettingsController(
            ISettingsService settingsService,
            IShippingService shippingService)
        {
            _settingsService = settingsService;
            _shippingService = shippingService;
        }

        // GET: Admin/Settings
        public async Task<IActionResult> Index(string tab = "general")
        {
            ViewBag.ActiveTab = tab;
            
            return tab.ToLower() switch
            {
                "seo" => View(await GetSeoViewModel()),
                "contact" => View(await GetContactViewModel()),
                "social" => View(await GetSocialViewModel()),
                "banner" => View(await GetBannerViewModel()),
                "shipping" => View(await GetShippingViewModel()),
                "smtp" => View(await GetSmtpViewModel()),
                "googleauth" => View(await GetGoogleAuthViewModel()),
                _ => View(await GetGeneralViewModel())
            };
        }

        // GET: Admin/Settings/General
        public async Task<IActionResult> General()
        {
            ViewBag.ActiveTab = "general";
            return View("Index", await GetGeneralViewModel());
        }

        // GET: Admin/Settings/Seo
        public async Task<IActionResult> Seo()
        {
            ViewBag.ActiveTab = "seo";
            return View("Index", await GetSeoViewModel());
        }

        // GET: Admin/Settings/Contact
        public async Task<IActionResult> Contact()
        {
            ViewBag.ActiveTab = "contact";
            return View("Index", await GetContactViewModel());
        }


        // GET: Admin/Settings/Social
        public async Task<IActionResult> Social()
        {
            ViewBag.ActiveTab = "social";
            return View("Index", await GetSocialViewModel());
        }

        // GET: Admin/Settings/Banner
        public async Task<IActionResult> Banner()
        {
            ViewBag.ActiveTab = "banner";
            return View("Index", await GetBannerViewModel());
        }

        // GET: Admin/Settings/Shipping
        public async Task<IActionResult> Shipping()
        {
            ViewBag.ActiveTab = "shipping";
            return View("Index", await GetShippingViewModel());
        }

        // GET: Admin/Settings/Smtp
        public async Task<IActionResult> Smtp()
        {
            ViewBag.ActiveTab = "smtp";
            return View("Index", await GetSmtpViewModel());
        }

        // GET: Admin/Settings/GoogleAuth
        public async Task<IActionResult> GoogleAuth()
        {
            ViewBag.ActiveTab = "googleauth";
            return View("Index", await GetGoogleAuthViewModel());
        }

        // POST: Admin/Settings/SaveGeneral
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGeneral(GeneralSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ActiveTab = "general";
                return View("Index", model);
            }

            // Save Site Name
            var result = await _settingsService.SaveSettingAsync(
                SettingKeys.SiteName, 
                model.SiteName, 
                SettingGroups.General);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                ViewBag.ActiveTab = "general";
                return View("Index", model);
            }

            // Upload Logo if provided
            if (model.LogoFile != null && model.LogoFile.Length > 0)
            {
                result = await _settingsService.SaveFileSettingAsync(
                    SettingKeys.Logo, 
                    model.LogoFile, 
                    SettingGroups.General);

                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage;
                    ViewBag.ActiveTab = "general";
                    return View("Index", await GetGeneralViewModel());
                }
            }

            // Upload Favicon if provided
            if (model.FaviconFile != null && model.FaviconFile.Length > 0)
            {
                result = await _settingsService.SaveFileSettingAsync(
                    SettingKeys.Favicon, 
                    model.FaviconFile, 
                    SettingGroups.General);

                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage;
                    ViewBag.ActiveTab = "general";
                    return View("Index", await GetGeneralViewModel());
                }
            }

            TempData["Success"] = "General settings saved successfully!";
            return RedirectToAction(nameof(Index), new { tab = "general" });
        }


        // POST: Admin/Settings/SaveSeo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSeo(SeoSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ActiveTab = "seo";
                return View("Index", model);
            }

            var settings = new Dictionary<string, string?>
            {
                { SettingKeys.MetaTitle, model.MetaTitle },
                { SettingKeys.MetaDescription, model.MetaDescription },
                { SettingKeys.MetaKeywords, model.MetaKeywords },
                { SettingKeys.GoogleAnalyticsId, model.GoogleAnalyticsId }
            };

            var result = await _settingsService.SaveSettingsAsync(settings, SettingGroups.Seo);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                ViewBag.ActiveTab = "seo";
                return View("Index", model);
            }

            TempData["Success"] = "SEO settings saved successfully!";
            return RedirectToAction(nameof(Index), new { tab = "seo" });
        }

        // POST: Admin/Settings/SaveContact
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveContact(ContactSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ActiveTab = "contact";
                return View("Index", model);
            }

            var settings = new Dictionary<string, string?>
            {
                { SettingKeys.ContactAddress, model.Address },
                { SettingKeys.ContactPhone, model.Phone },
                { SettingKeys.ContactEmail, model.Email },
                { SettingKeys.ContactWorkingHours, model.WorkingHours },
                { SettingKeys.ContactMapEmbed, model.MapEmbedCode }
            };

            var result = await _settingsService.SaveSettingsAsync(settings, SettingGroups.Contact);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                ViewBag.ActiveTab = "contact";
                return View("Index", model);
            }

            TempData["Success"] = "Contact settings saved successfully!";
            return RedirectToAction(nameof(Index), new { tab = "contact" });
        }


        // POST: Admin/Settings/SaveSocial
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSocial(SocialSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ActiveTab = "social";
                return View("Index", model);
            }

            var settings = new Dictionary<string, string?>
            {
                { SettingKeys.SocialFacebook, model.Facebook },
                { SettingKeys.SocialTwitter, model.Twitter },
                { SettingKeys.SocialInstagram, model.Instagram },
                { SettingKeys.SocialYoutube, model.Youtube },
                { SettingKeys.SocialLinkedIn, model.LinkedIn }
            };

            var result = await _settingsService.SaveSettingsAsync(settings, SettingGroups.Social);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                ViewBag.ActiveTab = "social";
                return View("Index", model);
            }

            TempData["Success"] = "Social settings saved successfully!";
            return RedirectToAction(nameof(Index), new { tab = "social" });
        }

        // POST: Admin/Settings/SaveBanner
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBanner(BannerSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ActiveTab = "banner";
                return View("Index", model);
            }

            // Save Banner 1
            var settings = new Dictionary<string, string?>
            {
                { SettingKeys.Banner1Title, model.Banner1.Title },
                { SettingKeys.Banner1Subtitle, model.Banner1.Subtitle },
                { SettingKeys.Banner1Link, model.Banner1.Link },
                { SettingKeys.Banner2Title, model.Banner2.Title },
                { SettingKeys.Banner2Subtitle, model.Banner2.Subtitle },
                { SettingKeys.Banner2Link, model.Banner2.Link },
                { SettingKeys.Banner3Title, model.Banner3.Title },
                { SettingKeys.Banner3Subtitle, model.Banner3.Subtitle },
                { SettingKeys.Banner3Link, model.Banner3.Link }
            };

            var result = await _settingsService.SaveSettingsAsync(settings, SettingGroups.Banner);
            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                ViewBag.ActiveTab = "banner";
                return View("Index", await GetBannerViewModel());
            }

            // Upload images if provided
            if (model.Banner1.ImageFile != null && model.Banner1.ImageFile.Length > 0)
            {
                result = await _settingsService.SaveFileSettingAsync(SettingKeys.Banner1Image, model.Banner1.ImageFile, SettingGroups.Banner);
                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage;
                    ViewBag.ActiveTab = "banner";
                    return View("Index", await GetBannerViewModel());
                }
            }

            if (model.Banner2.ImageFile != null && model.Banner2.ImageFile.Length > 0)
            {
                result = await _settingsService.SaveFileSettingAsync(SettingKeys.Banner2Image, model.Banner2.ImageFile, SettingGroups.Banner);
                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage;
                    ViewBag.ActiveTab = "banner";
                    return View("Index", await GetBannerViewModel());
                }
            }

            if (model.Banner3.ImageFile != null && model.Banner3.ImageFile.Length > 0)
            {
                result = await _settingsService.SaveFileSettingAsync(SettingKeys.Banner3Image, model.Banner3.ImageFile, SettingGroups.Banner);
                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage;
                    ViewBag.ActiveTab = "banner";
                    return View("Index", await GetBannerViewModel());
                }
            }

            TempData["Success"] = "Cài đặt banner đã được lưu!";
            return RedirectToAction(nameof(Index), new { tab = "banner" });
        }

        // POST: Admin/Settings/SaveShipping
        /// <summary>
        /// Lưu cài đặt vận chuyển
        /// Requirements 1.2, 1.3: Validate và lưu phí vận chuyển
        /// Requirements 2.1: Lưu ngưỡng miễn phí ship
        /// Requirements 3.1: Lưu danh sách quận/huyện theo khu vực
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveShipping(ShippingSettingsViewModel model)
        {
            // Validate shipping fees (Requirements 1.3)
            if (model.FeeZone1 < 0 || model.FeeZone2 < 0 || model.FeeZone3 < 0 ||
                model.FreeShippingThreshold < 0 || model.ReducedFeeZone3 < 0)
            {
                TempData["Error"] = "Phí vận chuyển và ngưỡng phải >= 0";
                ViewBag.ActiveTab = "shipping";
                return View("Index", await GetShippingViewModel());
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ActiveTab = "shipping";
                return View("Index", await GetShippingViewModel());
            }

            // Save shipping fees (Requirements 1.2)
            var settings = new Dictionary<string, string?>
            {
                { SettingKeys.ShippingFeeZone1, model.FeeZone1.ToString() },
                { SettingKeys.ShippingFeeZone2, model.FeeZone2.ToString() },
                { SettingKeys.ShippingFeeZone3, model.FeeZone3.ToString() },
                { SettingKeys.FreeShippingThreshold, model.FreeShippingThreshold.ToString() },
                { SettingKeys.ReducedShippingFeeZone3, model.ReducedFeeZone3.ToString() },
                { SettingKeys.Zone1Districts, JsonSerializer.Serialize(model.Zone1Districts ?? new List<string>()) },
                { SettingKeys.Zone2Districts, JsonSerializer.Serialize(model.Zone2Districts ?? new List<string>()) }
            };

            var result = await _settingsService.SaveSettingsAsync(settings, SettingGroups.Shipping);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                ViewBag.ActiveTab = "shipping";
                return View("Index", await GetShippingViewModel());
            }

            TempData["Success"] = "Cài đặt vận chuyển đã được lưu!";
            return RedirectToAction(nameof(Index), new { tab = "shipping" });
        }

        // POST: Admin/Settings/SaveSmtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSmtp(SmtpSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ActiveTab = "smtp";
                return View("Index", model);
            }

            var settings = new Dictionary<string, string?>
            {
                { SettingKeys.SmtpHost, model.Host },
                { SettingKeys.SmtpPort, model.Port.ToString() },
                { SettingKeys.SmtpUsername, model.Username },
                { SettingKeys.SmtpEnableSsl, model.EnableSsl.ToString() },
                { SettingKeys.SmtpSenderName, model.SenderName },
                { SettingKeys.SmtpSenderEmail, model.SenderEmail }
            };

            // Chỉ lưu password nếu người dùng nhập mới (không ghi đè bằng chuỗi rỗng)
            if (!string.IsNullOrWhiteSpace(model.Password))
                settings[SettingKeys.SmtpPassword] = model.Password;

            var result = await _settingsService.SaveSettingsAsync(settings, SettingGroups.Smtp);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                ViewBag.ActiveTab = "smtp";
                return View("Index", model);
            }

            TempData["Success"] = "Cài đặt SMTP đã được lưu!";
            return RedirectToAction(nameof(Index), new { tab = "smtp" });
        }

        // POST: Admin/Settings/SaveGoogleAuth
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGoogleAuth(GoogleAuthSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ActiveTab = "googleauth";
                return View("Index", model);
            }

            var settings = new Dictionary<string, string?>
            {
                { SettingKeys.GoogleAuthClientId, model.ClientId },
                { SettingKeys.GoogleAuthIsEnabled, model.IsEnabled.ToString() }
            };

            // Chỉ lưu client secret nếu người dùng nhập mới
            if (!string.IsNullOrWhiteSpace(model.ClientSecret))
                settings[SettingKeys.GoogleAuthClientSecret] = model.ClientSecret;

            var result = await _settingsService.SaveSettingsAsync(settings, SettingGroups.GoogleAuth);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                ViewBag.ActiveTab = "googleauth";
                return View("Index", model);
            }

            TempData["Success"] = "Cài đặt Google Authentication đã được lưu!";
            return RedirectToAction(nameof(Index), new { tab = "googleauth" });
        }

        #region Private Helper Methods

        private async Task<GeneralSettingsViewModel> GetGeneralViewModel()
        {
            return new GeneralSettingsViewModel
            {
                SiteName = await _settingsService.GetSettingAsync(SettingKeys.SiteName),
                LogoPath = await _settingsService.GetSettingAsync(SettingKeys.Logo),
                FaviconPath = await _settingsService.GetSettingAsync(SettingKeys.Favicon)
            };
        }

        private async Task<SeoSettingsViewModel> GetSeoViewModel()
        {
            return new SeoSettingsViewModel
            {
                MetaTitle = await _settingsService.GetSettingAsync(SettingKeys.MetaTitle),
                MetaDescription = await _settingsService.GetSettingAsync(SettingKeys.MetaDescription),
                MetaKeywords = await _settingsService.GetSettingAsync(SettingKeys.MetaKeywords),
                GoogleAnalyticsId = await _settingsService.GetSettingAsync(SettingKeys.GoogleAnalyticsId)
            };
        }

        private async Task<ContactSettingsViewModel> GetContactViewModel()
        {
            return new ContactSettingsViewModel
            {
                Address = await _settingsService.GetSettingAsync(SettingKeys.ContactAddress),
                Phone = await _settingsService.GetSettingAsync(SettingKeys.ContactPhone),
                Email = await _settingsService.GetSettingAsync(SettingKeys.ContactEmail),
                WorkingHours = await _settingsService.GetSettingAsync(SettingKeys.ContactWorkingHours),
                MapEmbedCode = await _settingsService.GetSettingAsync(SettingKeys.ContactMapEmbed)
            };
        }

        private async Task<SocialSettingsViewModel> GetSocialViewModel()
        {
            return new SocialSettingsViewModel
            {
                Facebook = await _settingsService.GetSettingAsync(SettingKeys.SocialFacebook),
                Twitter = await _settingsService.GetSettingAsync(SettingKeys.SocialTwitter),
                Instagram = await _settingsService.GetSettingAsync(SettingKeys.SocialInstagram),
                Youtube = await _settingsService.GetSettingAsync(SettingKeys.SocialYoutube),
                LinkedIn = await _settingsService.GetSettingAsync(SettingKeys.SocialLinkedIn)
            };
        }

        private async Task<BannerSettingsViewModel> GetBannerViewModel()
        {
            return new BannerSettingsViewModel
            {
                Banner1 = new BannerItemViewModel
                {
                    Title = await _settingsService.GetSettingAsync(SettingKeys.Banner1Title) ?? "Fresh Apples",
                    Subtitle = await _settingsService.GetSettingAsync(SettingKeys.Banner1Subtitle) ?? "20% OFF",
                    ImagePath = await _settingsService.GetSettingAsync(SettingKeys.Banner1Image) ?? "/img/featur-1.jpg",
                    Link = await _settingsService.GetSettingAsync(SettingKeys.Banner1Link)
                },
                Banner2 = new BannerItemViewModel
                {
                    Title = await _settingsService.GetSettingAsync(SettingKeys.Banner2Title) ?? "Tasty Fruits",
                    Subtitle = await _settingsService.GetSettingAsync(SettingKeys.Banner2Subtitle) ?? "Free delivery",
                    ImagePath = await _settingsService.GetSettingAsync(SettingKeys.Banner2Image) ?? "/img/featur-2.jpg",
                    Link = await _settingsService.GetSettingAsync(SettingKeys.Banner2Link)
                },
                Banner3 = new BannerItemViewModel
                {
                    Title = await _settingsService.GetSettingAsync(SettingKeys.Banner3Title) ?? "Exotic Vegetables",
                    Subtitle = await _settingsService.GetSettingAsync(SettingKeys.Banner3Subtitle) ?? "Discount 30$",
                    ImagePath = await _settingsService.GetSettingAsync(SettingKeys.Banner3Image) ?? "/img/featur-3.jpg",
                    Link = await _settingsService.GetSettingAsync(SettingKeys.Banner3Link)
                }
            };
        }

        /// <summary>
        /// Get shipping settings view model
        /// Requirements 1.1: Hiển thị form cấu hình phí ship
        /// </summary>
        private async Task<ShippingSettingsViewModel> GetShippingViewModel()
        {
            var config = await _shippingService.GetShippingConfigAsync();

            return new ShippingSettingsViewModel
            {
                FeeZone1 = config.FeeZone1,
                FeeZone2 = config.FeeZone2,
                FeeZone3 = config.FeeZone3,
                FreeShippingThreshold = config.FreeShippingThreshold,
                ReducedFeeZone3 = config.ReducedFeeZone3,
                Zone1Districts = config.Zone1Districts,
                Zone2Districts = config.Zone2Districts,
                AllDistricts = new List<string>()
            };
        }

        private async Task<SmtpSettingsViewModel> GetSmtpViewModel()
        {
            return new SmtpSettingsViewModel
            {
                Host = await _settingsService.GetSettingAsync(SettingKeys.SmtpHost) ?? string.Empty,
                Port = int.TryParse(await _settingsService.GetSettingAsync(SettingKeys.SmtpPort), out var port) ? port : 587,
                Username = await _settingsService.GetSettingAsync(SettingKeys.SmtpUsername) ?? string.Empty,
                EnableSsl = bool.TryParse(await _settingsService.GetSettingAsync(SettingKeys.SmtpEnableSsl), out var ssl) ? ssl : true,
                SenderName = await _settingsService.GetSettingAsync(SettingKeys.SmtpSenderName) ?? string.Empty,
                SenderEmail = await _settingsService.GetSettingAsync(SettingKeys.SmtpSenderEmail) ?? string.Empty
            };
        }

        private async Task<GoogleAuthSettingsViewModel> GetGoogleAuthViewModel()
        {
            return new GoogleAuthSettingsViewModel
            {
                ClientId = await _settingsService.GetSettingAsync(SettingKeys.GoogleAuthClientId) ?? string.Empty,
                IsEnabled = bool.TryParse(await _settingsService.GetSettingAsync(SettingKeys.GoogleAuthIsEnabled), out var enabled) ? enabled : false
            };
        }

        #endregion
    }
}
