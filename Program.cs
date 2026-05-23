using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ChiNhanhs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HopDongs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiThues;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros;
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

// Đăng ký Service vào Container
builder.Services.AddScoped<IChiNhanhService, ChiNhanhService>();
builder.Services.AddScoped<IPhongTroService, PhongTroService>();
builder.Services.AddScoped<INguoiThueService, NguoiThueService>();
builder.Services.AddScoped<IHopDongService, HopDongService>();

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "MyAreas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
