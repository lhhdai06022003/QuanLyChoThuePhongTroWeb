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
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Persistence.IAiAssistantStore, Persistence.Features.AiAssistantStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Persistence.IYeuCauSuCoStore, Persistence.Features.YeuCauSuCoStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Persistence.IDichVuStore, Persistence.Features.DichVuStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence.INguoiThueStore, Persistence.Features.NguoiThueStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence.ITenantVisibilityStore, Persistence.Features.NguoiThueStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Persistence.IPhongTroStore, Persistence.Features.PhongTroStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms.IPublicRoomStore, Persistence.Features.PublicRoomStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.Persistence.IThanhVienHopDongStore, Persistence.Features.ThanhVienHopDongStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Persistence.IThongBaoStore, Persistence.Features.ThongBaoStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence.INguoiDungStore, Persistence.Features.NguoiDungStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.GuestAccounts.IGuestAccountStore, Persistence.Features.GuestAccounts.GuestAccountStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.Viewings.IViewingRequestStore, Persistence.Features.Viewings.ViewingRequestStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.Viewings.IViewingSlotAdminStore, Persistence.Features.Viewings.ViewingSlotAdminStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms.IRoomListingStore, Persistence.Features.RoomListingStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.Viewings.IViewingAdminRequestStore, Persistence.Features.Viewings.ViewingAdminRequestStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.Reservations.IReservationStore, Persistence.Features.Reservations.ReservationStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments.IReservationPaymentStore, Persistence.Features.ReservationPayments.ReservationPaymentStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement.IReservationSettlementStore, Persistence.Features.ReservationSettlement.ReservationSettlementStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.RoomDepositedNotices.IRoomNoticeStore, Persistence.Features.RoomDepositedNotices.RoomNoticeStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence.IHopDongStore, Persistence.Features.HopDongStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence.IHoaDonStore, Persistence.Features.HoaDonStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence.IInvoicePaymentStore, Persistence.Features.InvoicePaymentStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence.IInvoiceIssuanceStore, Persistence.Features.InvoiceIssuanceStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence.IAutoInvoiceStore, Persistence.Features.AutoInvoiceStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence.IMeterConsumptionHistoryStore, Persistence.Features.MeterConsumptionHistoryStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence.IInvoicePublicationStore, Persistence.Features.InvoicePublicationStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence.IInvoiceViewStore, Persistence.Features.InvoiceViewStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence.IDienNuocStore, Persistence.Features.DienNuocStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence.IMeterImageStore, Persistence.Features.MeterImageStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence.IMeterImageQueryStore, Persistence.Features.MeterImageQueryStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Persistence.ILichSuThanhToanStore, Persistence.Features.LichSuThanhToanStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Persistence.IDashboardStore, Persistence.Features.DashboardStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Persistence.IViecCanLamStore, Persistence.Features.ViecCanLamStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence.IEmployeeBranchStore, Persistence.Features.EmployeeBranchStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence.IEmployeeBranchAssignmentStore, Persistence.Features.EmployeeBranchAssignmentStore>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence.IUserSessionStore, Persistence.Features.UserSessionStore>();

            // Document exporters & QR services
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IInvoiceDocumentExporter, ExternalServices.DocumentExporters.InvoiceDocumentExporter>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IVietQRService, ExternalServices.DocumentExporters.VietQRService>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IContractDocumentGenerator, ExternalServices.DocumentExporters.DocXContractDocumentGenerator>();

            // External services: Email, Storage, AI
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IEmailService, ExternalServices.Emails.MailKitEmailService>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IImageStorageService, ExternalServices.Storage.CloudinaryStorageService>();

            var meterOptions = configuration.GetSection("MeterImageOptions").Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.MeterImageOptions>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.MeterImageOptions();
            meterOptions.Validate();
            services.AddSingleton(meterOptions);

            var invoiceIssuanceOptions = configuration.GetSection(QuanLyChoThuePhongTroWeb.Application.Common.Configurations.InvoiceIssuanceOptions.SectionName).Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.InvoiceIssuanceOptions>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.InvoiceIssuanceOptions();
            invoiceIssuanceOptions.Validate();
            services.AddSingleton(invoiceIssuanceOptions);

            var autoInvoiceOptions = configuration.GetSection(QuanLyChoThuePhongTroWeb.Application.Common.Configurations.AutoInvoiceOptions.SectionName).Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.AutoInvoiceOptions>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.AutoInvoiceOptions();
            autoInvoiceOptions.Validate();
            services.AddSingleton(autoInvoiceOptions);

            var invoicePaymentOptions = configuration.GetSection(QuanLyChoThuePhongTroWeb.Application.Common.Configurations.InvoicePaymentOptions.SectionName).Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.InvoicePaymentOptions>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.InvoicePaymentOptions();
            invoicePaymentOptions.Validate();
            services.AddSingleton(invoicePaymentOptions);

            var sePayOptions = configuration.GetSection(QuanLyChoThuePhongTroWeb.Application.Common.Configurations.SePayOptions.SectionName).Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.SePayOptions>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.SePayOptions();
            sePayOptions.Validate();
            services.AddSingleton(sePayOptions);
            services.AddSingleton<QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments.ISePayWebhookVerifier, ExternalServices.Payments.SePayWebhookVerifier>();

            var vnPayOptions = configuration.GetSection(QuanLyChoThuePhongTroWeb.Application.Common.Configurations.VnPayOptions.SectionName).Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.VnPayOptions>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.VnPayOptions();
            vnPayOptions.Validate();
            services.AddSingleton(vnPayOptions);

            var telegramOptions = configuration.GetSection(QuanLyChoThuePhongTroWeb.Application.Common.Configurations.TelegramOptions.SectionName).Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.TelegramOptions>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.TelegramOptions();
            telegramOptions.Validate();
            services.AddSingleton(telegramOptions);

            var onlinePaymentOptions = configuration.GetSection(QuanLyChoThuePhongTroWeb.Application.Common.Configurations.OnlinePaymentOptions.SectionName).Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.OnlinePaymentOptions>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.OnlinePaymentOptions();
            onlinePaymentOptions.Validate();
            services.AddSingleton(onlinePaymentOptions);

            // Chống cảnh báo Telegram lặp (spec thanh toán trực tuyến §5.3): một bộ nhớ dùng chung cho mọi request.
            services.AddMemoryCache();
            services.AddSingleton<QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments.ITransferAlertDeduplicator, ExternalServices.Payments.MemoryCacheTransferAlertDeduplicator>();

            services.AddHttpClient<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IMeterImageStorageService, ExternalServices.Storage.CloudinaryMeterImageStorageService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(meterOptions.DownloadTimeoutSeconds);
            });
            services.AddHttpClient<QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel.IAiChatModel, ExternalServices.AiAssistants.GeminiChatModel>(client =>
            {
                // Một câu hỏi gọi model tối đa 4 lần; 30 giây mỗi lần để request không treo quá lâu.
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddHttpClient<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IMeterOcrService, ExternalServices.AiAssistants.GeminiMeterOcrService>();
            // URL gọi Telegram chứa BotToken: gỡ logger của HttpClient để không in URL ra log.
            services.AddHttpClient<QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments.ITelegramNotifier, ExternalServices.Telegram.TelegramBotNotifier>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            }).RemoveAllLoggers();
            // Header Authorization chứa ApiToken: gỡ logger của HttpClient để không in request ra log.
            services.AddHttpClient<QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments.ISePayTransactionClient, ExternalServices.Payments.SePayTransactionClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            }).RemoveAllLoggers();

            // Security services
            services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<QuanLyChoThuePhongTroWeb.Domain.Entities.NguoiDung>, Microsoft.AspNetCore.Identity.PasswordHasher<QuanLyChoThuePhongTroWeb.Domain.Entities.NguoiDung>>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Security.IPasswordService, Security.AspNetPasswordService>();

            // Typed settings records
            services.AddSingleton(configuration.GetSection("DashboardSettings").Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.DashboardSettings>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.DashboardSettings());
            services.AddSingleton(configuration.GetSection("ViecCanLamSettings").Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.ViecCanLamSettings>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.ViecCanLamSettings());
            services.AddSingleton(configuration.GetSection("VietQRSettings").Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.VietQrSettings>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.VietQrSettings());
            services.AddSingleton(configuration.GetSection("AutoReminderSettings").Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.AutoReminderSettings>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.AutoReminderSettings());
            services.AddSingleton(configuration.GetSection("ContractAlertSettings").Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.ContractAlertSettings>() ?? new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.ContractAlertSettings());

            // Background job state stores
            services.AddSingleton<QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs.IInvoiceReminderStateStore, BackgroundJobs.State.JsonInvoiceReminderStateStore>();
            services.AddSingleton<QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs.IContractExpiryAlertStateStore, BackgroundJobs.State.JsonContractExpiryAlertStateStore>();

            // Hàng đợi và worker gửi Telegram, email biên lai sau khi ghi sổ: luôn đăng ký, không phụ thuộc BackgroundJobs:Enabled
            // (tắt job định kỳ không được làm mất tin thanh toán, spec thanh toán trực tuyến §6.1).
            services.AddSingleton<BackgroundJobs.PaymentNotifications.ChannelPaymentNotificationQueue>();
            services.AddSingleton<QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments.IPaymentNotificationQueue>(sp =>
                sp.GetRequiredService<BackgroundJobs.PaymentNotifications.ChannelPaymentNotificationQueue>());
            services.AddHostedService<BackgroundJobs.PaymentNotifications.PaymentNotificationWorker>();

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
