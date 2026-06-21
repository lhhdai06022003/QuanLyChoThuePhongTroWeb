using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ChiNhanhs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DichVus;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DienNuocs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HopDongs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HoaDons;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiThues;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ThanhVienHopDongs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.LichSuThanhToans;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Emails;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Dashboard;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.BackgroundJobs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DieuKhoanMaus;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.AiAssistants;
using QuanLyChoThuePhongTroWeb.Data;
using System;

var builder = WebApplication.CreateBuilder(args);

//  cấu hình PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddControllersWithViews().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddControllers();

// Cấu hình Antiforgery để nhận Token qua Header của AJAX
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Đăng ký Service vào Container
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IChiNhanhService, ChiNhanhService>();
builder.Services.AddScoped<IPhongTroService, PhongTroService>();
builder.Services.AddScoped<INguoiThueService, NguoiThueService>();
builder.Services.AddScoped<IHopDongService, HopDongService>();
builder.Services.AddScoped<IThanhVienHopDongService, ThanhVienHopDongService>();
builder.Services.AddScoped<IDichVuService, DichVuService>();
builder.Services.AddScoped<IDienNuocService, DienNuocService>();
builder.Services.AddScoped<INguoiDungService, NguoiDungService>();
builder.Services.AddScoped<IHoaDonService, HoaDonService>();
builder.Services.AddScoped<ILichSuThanhToanService, LichSuThanhToanService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IDieuKhoanMauService, DieuKhoanMauService>();
builder.Services.AddHttpClient<IAiAssistantService, AiAssistantService>();
builder.Services.AddHostedService<InvoiceReminderService>();
builder.Services.AddHostedService<ContractAutoCloseService>();
builder.Services.AddHostedService<ContractExpiryAlertService>();

// Cấu hình Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/QuanLyNhaTro/DangNhap";
        options.LogoutPath = "/QuanLyNhaTro/DangXuat";
        options.AccessDeniedPath = "/QuanLyNhaTro/DangNhap";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });

var app = builder.Build();
// Lệnh này sẽ tự động chạy các Migration còn thiếu lên Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        // Lệnh này sẽ tự động chạy các Migration còn thiếu lên Database
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Một lỗi đã xảy ra khi đang tự động tạo/cập nhật Database.");
    }
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

app.Run();
