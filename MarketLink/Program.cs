using MarketLink.Data;
using MarketLink.Models;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using MarketLink.Services;
using MarketLink.Services.Admin;
using MarketLink.Services.Farmer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Use English (US) number and date formats everywhere, e.g. 10.762622 and 95,000
var culture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Add services to the container.
builder.Services.AddControllersWithViews();


builder.Services.AddDbContext<MarketLinkDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MarketConnection")));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICustomerAccountService, CustomerAccountService>();
builder.Services.AddScoped<IShopService, ShopService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<ICustomerOrderService, CustomerOrderService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IDisputeService, DisputeService>();
builder.Services.AddHostedService<AutoCancelService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IFarmerAccountService, FarmerAccountService>();
builder.Services.AddScoped<IFarmerProfileService, FarmerProfileService>();
builder.Services.AddScoped<IFarmerStallManagementService, FarmerStallManagementService>();
builder.Services.AddScoped<IProductUnitService, ProductUnitService>();
builder.Services.AddScoped<IFarmerProductService, FarmerProductService>();
builder.Services.AddScoped<IFarmerDashboardService, FarmerDashboardService>();

// Email (settings in appsettings.json -> "EmailSettings")
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// AI check of product names typed by hand (Google Gemini, free key from AI Studio).
// The key lives in user-secrets, never in appsettings.json.
builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection("Gemini"));
builder.Services.AddHttpClient<IProductNameChecker, GeminiProductNameChecker>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Admin portal
builder.Services.AddScoped<IAdminReportService, AdminReportService>();
builder.Services.AddScoped<IAdminMarketService, AdminMarketService>();
builder.Services.AddScoped<IAdminFarmerService, AdminFarmerService>();
builder.Services.AddScoped<IAdminCustomerService, AdminCustomerService>();
builder.Services.AddScoped<IAdminCategoryService, AdminCategoryService>();
builder.Services.AddScoped<IAdminProductExpService, AdminProductExpService>();
builder.Services.AddScoped<IAdminProductTemplateService, AdminProductTemplateService>();

// Cookie-based login
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";              // not logged in -> redirect here
        options.AccessDeniedPath = "/Account/AccessDenied"; // wrong role -> redirect here
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
    {
        int userId;
        if (int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId))
        {
            var db = context.RequestServices.GetRequiredService<MarketLinkDbContext>();
            bool isActive = await db.Users.AnyAsync(u => u.UserId == userId && u.Status == "active");
            if (!isActive)
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Redirect("/Account/Login?locked=true");
                return;
            }
        }
    }
    await next();
});

app.UseAuthorization();

app.MapStaticAssets();

// Admin area: /Admin, /Admin/Markets, /Admin/Farmers/Index?status=pending ...
app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
