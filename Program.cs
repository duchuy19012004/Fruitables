using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Fruitables.Data;
using Fruitables.Repositories;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services;
using Fruitables.Services.Chat;
using Fruitables.Services.Chat.Intents;
using Fruitables.Services.Interfaces;
using Fruitables.Services.Search;
using Fruitables.Options;
using Fruitables.Filters;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Configure Antiforgery to accept token from AJAX header (for PUT/DELETE JSON requests)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // Register RequirePermissionFilter globally
    options.Filters.Add<RequirePermissionFilter>();
})
.AddRazorRuntimeCompilation(); // Enable runtime compilation for development

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Memory Cache
builder.Services.AddMemoryCache();

// Add Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Add Services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ITestimonialService, TestimonialService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IProductAdminService, ProductAdminService>();
builder.Services.AddScoped<IComboService, ComboService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IProductPricingService, ProductPricingService>();
builder.Services.AddScoped<IPriceManagementService, PriceManagementService>();
builder.Services.AddHostedService<PriceScheduleWorker>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddScoped<IProductLogService, ProductLogService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IOrderAdminService, OrderAdminService>();
builder.Services.AddScoped<IOrderLogService, OrderLogService>();
builder.Services.AddScoped<IUserAuthService, UserAuthService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IOrderHistoryService, OrderHistoryService>();
builder.Services.AddScoped<ISalesAnalyticsService, SalesAnalyticsService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IShippingService, ShippingService>();
builder.Services.Configure<GhnOptions>(builder.Configuration.GetSection("Ghn"));
builder.Services.Configure<SePayOptions>(builder.Configuration.GetSection("SePay"));
// ----- Chatbot RAG (cấu hình trong appsettings mục "Chat") -----
builder.Services.Configure<Fruitables.Options.ChatOptions>(
    builder.Configuration.GetSection(Fruitables.Options.ChatOptions.SectionName));
builder.Services.Configure<SearchSuggestOptions>(
    builder.Configuration.GetSection(SearchSuggestOptions.SectionName));
builder.Services.AddScoped<ISearchSuggestService, SearchSuggestService>();

// Cấu hình HttpClient cho endpoint AI local theo chuẩn OpenAI.
static void ConfigureChatHttpClient(IServiceProvider sp, HttpClient client)
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Fruitables.Options.ChatOptions>>().Value;
    ChatHttpClientConfigurator.Configure(client, options);

}

// AI chat qua endpoint local OpenAI-compatible.
builder.Services.AddHttpClient<ILlmClient, SpaceXaiLlmClient>(ConfigureChatHttpClient);

// Mã hóa tri thức: mặc định Local (không gọi API embedding).
// Đổi Chat:EmbeddingProvider=OpenAICompatible nếu sau này dùng embed qua API
var embeddingProvider = builder.Configuration["Chat:EmbeddingProvider"] ?? "Local";
if (string.Equals(embeddingProvider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmbeddingClient, SpaceXaiEmbeddingClient>(ConfigureChatHttpClient);
}
else
{
    builder.Services.AddScoped<IEmbeddingClient, LocalHashEmbeddingClient>();
}

// Các "công đoạn" chatbot
builder.Services.AddScoped<IIndexingService, IndexingService>(); // đưa FAQ/SP vào sổ tri thức
builder.Services.AddScoped<IFaqService, FaqService>();           // CRUD FAQ Admin
builder.Services.AddScoped<IRagService, RagService>();           // tìm tri thức + gọi AI
builder.Services.AddScoped<IChatService, ChatService>();         // session, lưu tin, chống spam
builder.Services.AddScoped<IIntentRouter, IntentRouter>();       // phân loại ý định khách hàng

builder.Services.AddHttpClient<IGhnService, GhnService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GhnOptions>>().Value;
    if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        client.BaseAddress = baseUri;
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "Fruitables/1.0");
});
builder.Services.AddScoped<IWordMaskingService, WordMaskingService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

// Add RBAC Services
builder.Services.AddScoped<IRbacService, RbacService>();
builder.Services.AddScoped<IMigrationService, MigrationService>();

// Named HttpClient for AddressKit API (used by MigrationService for data conversion)
builder.Services.AddHttpClient("AddressKit", client =>
{
    client.BaseAddress = new Uri("https://production.cas.so/address-kit/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Fruitables/1.0");
});

// Add VietnamAddressService with HttpClient configured for 10 second timeout
builder.Services.AddHttpClient<IVietnamAddressService, VietnamAddressService>(client =>
{
    client.BaseAddress = new Uri("https://production.cas.so/address-kit/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "Fruitables/1.0");
});

// Add Cookie Authentication with Google OAuth
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "Fruitables.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    });

// Add Google OAuth if credentials are configured via Environment Variables
// Set: Authentication__Google__ClientId and Authentication__Google__ClientSecret
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
    });
}

// Add Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add SignalR
builder.Services.AddSignalR();

// Add Data Protection to persist encryption keys to survive IIS App Pool recycles
var keysDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("FruitablesApp");

var app = builder.Build();

// Seed default settings
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.SeedDefaultSettingsAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Map API controllers (for AddressApiController and other API endpoints)
app.MapControllers();

app.MapHub<Fruitables.Hubs.EcommerceHub>("/hubs/ecommerce");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
