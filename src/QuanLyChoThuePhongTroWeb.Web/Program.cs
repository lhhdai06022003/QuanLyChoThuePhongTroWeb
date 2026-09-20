using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using QuanLyChoThuePhongTroWeb.Models;
using QuanLyChoThuePhongTroWeb.Infrastructure;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.Hubs;
using System;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Infrastructure & Persistence & Application
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

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
