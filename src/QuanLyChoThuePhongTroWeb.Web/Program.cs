using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using QuanLyChoThuePhongTroWeb.Models;
using QuanLyChoThuePhongTroWeb.Infrastructure;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.Hubs;
using System;
using System.Threading.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Infrastructure & Persistence & Application
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
if (builder.Environment.IsDevelopment())
{
    // Room photos are public; save them locally during development so uploads work offline.
    builder.Services.RemoveAll<IRoomPhotoStorage>();
    builder.Services.AddScoped<IRoomPhotoStorage>(_ => new LocalRoomPhotoStorage(
        Path.Combine(builder.Environment.WebRootPath ??
            Path.Combine(builder.Environment.ContentRootPath, "wwwroot"), "uploads", "rooms")));
}

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<QuanLyChoThuePhongTroWeb.Filters.GlobalExceptionFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new QuanLyChoThuePhongTroWeb.Helpers.DateTimeJsonConverter());
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("guest-viewing", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddScoped<IThongBaoNotifier, SignalRThongBaoNotifier>();

// Cấu hình Antiforgery để nhận Token qua Header của AJAX
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});


// Cấu hình Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/QuanLyNhaTro/DangNhap";
        options.LogoutPath = "/QuanLyNhaTro/DangXuat";
        options.AccessDeniedPath = "/QuanLyNhaTro/DangNhap";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events.OnValidatePrincipal = async context =>
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<INguoiDungService>();
            var id = int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId) ? actorId : 0;
            var user = id > 0 ? await users.GetByIdAsync(id) : null;
            if (user is null || !user.IsActive)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }
            var role = user.Role.ToString();
            var tenantId = user.NguoiThueId?.ToString();
            if (context.Principal?.FindFirstValue(ClaimTypes.Role) != role ||
                context.Principal?.FindFirstValue("NguoiThueId") != tenantId)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.NguoiDungId.ToString()),
                    new(ClaimTypes.Name, user.TenDangNhap), new(ClaimTypes.Role, role)
                };
                if (tenantId is not null) claims.Add(new("NguoiThueId", tenantId));
                context.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(claims,
                    CookieAuthenticationDefaults.AuthenticationScheme)));
                context.ShouldRenew = true;
            }
        };
    });

var app = builder.Build();

// Khởi tạo Database và Seed data qua Infrastructure Initializer
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await initializer.InitializeAsync();
}
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
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "MyAreas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<QuanLyChoThuePhongTroWeb.Hubs.ThongBaoHub>("/thongBaoHub");

app.Run();

public partial class Program { }
