using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

            services.AddScoped<IUnitOfWork, Persistence.EfUnitOfWork>();
            services.AddScoped<Persistence.IDatabaseInitializer, Persistence.DatabaseInitializer>();

            // Feature Stores
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.Persistence.IChiNhanhStore, Persistence.Features.ChiNhanhStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.Persistence.IDieuKhoanMauStore, Persistence.Features.DieuKhoanMauStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Persistence.IYeuCauSuCoStore, Persistence.Features.YeuCauSuCoStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Persistence.IDichVuStore, Persistence.Features.DichVuStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence.INguoiThueStore, Persistence.Features.NguoiThueStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Persistence.IPhongTroStore, Persistence.Features.PhongTroStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.Persistence.IThanhVienHopDongStore, Persistence.Features.ThanhVienHopDongStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Persistence.IThongBaoStore, Persistence.Features.ThongBaoStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence.INguoiDungStore, Persistence.Features.NguoiDungStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence.IHopDongStore, Persistence.Features.HopDongStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence.IHoaDonStore, Persistence.Features.HoaDonStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence.IInvoicePaymentStore, Persistence.Features.InvoicePaymentStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence.IDienNuocStore, Persistence.Features.DienNuocStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Persistence.ILichSuThanhToanStore, Persistence.Features.LichSuThanhToanStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Persistence.IDashboardStore, Persistence.Features.DashboardStore>();

            // Document exporters & QR services
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IInvoiceDocumentExporter, ExternalServices.DocumentExporters.InvoiceDocumentExporter>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IVietQRService, ExternalServices.DocumentExporters.VietQRService>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IContractDocumentGenerator, ExternalServices.DocumentExporters.DocXContractDocumentGenerator>();

            // External services: Email, Storage, AI
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IEmailService, ExternalServices.Emails.MailKitEmailService>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IImageStorageService, ExternalServices.Storage.CloudinaryStorageService>();
            services.AddHttpClient<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IAiAssistantService, ExternalServices.AiAssistants.AiAssistantService>();

            // Security services
            services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<QuanLyChoThuePhongTroWeb.Domain.Entities.NguoiDung>, Microsoft.AspNetCore.Identity.PasswordHasher<QuanLyChoThuePhongTroWeb.Domain.Entities.NguoiDung>>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Security.IPasswordService, Security.AspNetPasswordService>();

            // Typed settings records
            services.AddSingleton(configuration.GetSection("DashboardSettings").Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.DashboardSettings>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.DashboardSettings());
            services.AddSingleton(configuration.GetSection("VietQRSettings").Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.VietQrSettings>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.VietQrSettings());
            services.AddSingleton(configuration.GetSection("AutoReminderSettings").Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.AutoReminderSettings>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.AutoReminderSettings());
            services.AddSingleton(configuration.GetSection("ContractAlertSettings").Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.ContractAlertSettings>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.ContractAlertSettings());

            // Background job state stores
            services.AddSingleton<QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs.IInvoiceReminderStateStore, BackgroundJobs.State.JsonInvoiceReminderStateStore>();
            services.AddSingleton<QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs.IContractExpiryAlertStateStore, BackgroundJobs.State.JsonContractExpiryAlertStateStore>();

            // Background hosted jobs (default enabled, can be disabled via BackgroundJobs:Enabled in test host)
            var backgroundJobsEnabled = configuration.GetValue<bool?>("BackgroundJobs:Enabled") ?? true;
            if (backgroundJobsEnabled)
            {
                services.AddHostedService<BackgroundJobs.ContractAutoCloseJob>();
                services.AddHostedService<BackgroundJobs.ContractExpiryAlertJob>();
                services.AddHostedService<BackgroundJobs.InvoiceReminderJob>();
            }

            return services;
        }
    }
}
