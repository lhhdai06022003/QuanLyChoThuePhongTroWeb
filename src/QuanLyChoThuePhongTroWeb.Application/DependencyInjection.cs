using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.Application.Features.GuestAccounts;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;
using QuanLyChoThuePhongTroWeb.Application.Features.RoomDepositedNotices;

namespace QuanLyChoThuePhongTroWeb.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // Phase 6.1 services
            services.AddScoped<IDieuKhoanMauService, DieuKhoanMauService>();
            services.AddScoped<IDichVuService, DichVuService>();
            services.AddScoped<IChiNhanhService, ChiNhanhService>();

            // Phase 6.2 services
            services.AddScoped<IPhongTroService, PhongTroService>();
            services.AddScoped<IPublicRoomService, PublicRoomService>();
            services.AddScoped<IGuestRegistrationService, GuestRegistrationService>();
            services.AddScoped<IViewingRequestService, ViewingRequestService>();
            services.AddScoped<IViewingSlotAdminService, ViewingSlotAdminService>();
            services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms.IRoomListingService, QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms.RoomListingService>();
            services.AddScoped<IViewingAdminRequestService, ViewingAdminRequestService>();
            services.AddScoped<IReservationService, ReservationService>();
            services.AddScoped<IReservationPaymentService, ReservationPaymentService>();
            services.AddScoped<IReservationSettlementService, ReservationSettlementService>();
            services.AddScoped<IRoomNoticeService, RoomNoticeService>();
            services.AddScoped<INguoiThueService, NguoiThueService>();
            services.AddScoped<INguoiDungService, NguoiDungService>();

            // Phase 6.3 services
            services.AddScoped<IHopDongService, HopDongService>();
            services.AddScoped<IThanhVienHopDongService, ThanhVienHopDongService>();
            services.AddScoped<IDienNuocService, DienNuocService>();
            services.AddScoped<IMeterReadingWorkflowService, MeterReadingWorkflowService>();
            services.AddScoped<IMeterImagePortalService, MeterImagePortalService>();
            services.AddScoped<IMeterAnomalyCheckService, MeterAnomalyCheckService>();

            // Phase 6.4 services
            services.AddScoped<IHoaDonCalculatorService, HoaDonCalculatorService>();
            services.AddScoped<IHoaDonService, HoaDonService>();
            services.AddScoped<IInvoiceIssuanceService, InvoiceIssuanceService>();
            services.AddScoped<IAutoInvoiceService, AutoInvoiceService>();
            services.AddScoped<IInvoicePublicationService, InvoicePublicationService>();
            services.AddScoped<IInvoiceAdjustmentService, InvoiceAdjustmentService>();
            services.AddScoped<IInvoiceViewService, InvoiceViewService>();
            services.AddScoped<IInvoicePaymentConfirmationService, InvoicePaymentConfirmationService>();
            services.AddScoped<IInvoicePaymentRequestService, InvoicePaymentRequestService>();
            services.AddScoped<IInvoiceLedgerService, InvoiceLedgerService>();
            services.AddScoped<Features.OnlinePayments.InvoiceTransferHandler>();
            services.AddScoped<Features.OnlinePayments.IIncomingTransferHandler>(sp => sp.GetRequiredService<Features.OnlinePayments.InvoiceTransferHandler>());
            services.AddScoped<Features.OnlinePayments.IVnPayPaymentService, Features.OnlinePayments.VnPayPaymentService>();
            services.AddScoped<Features.OnlinePayments.ReservationTransferHandler>();
            services.AddScoped<Features.OnlinePayments.IIncomingTransferHandler>(sp => sp.GetRequiredService<Features.OnlinePayments.ReservationTransferHandler>());
            services.AddScoped<Features.OnlinePayments.IReservationVnPayService, Features.OnlinePayments.ReservationVnPayService>();
            services.AddScoped<Features.OnlinePayments.IncomingTransferClassifier>();
            services.AddScoped<Features.OnlinePayments.IBankTransferIngestionService, Features.OnlinePayments.BankTransferIngestionService>();
            services.AddScoped<Features.OnlinePayments.IPaymentOutcomePublisher, Features.OnlinePayments.PaymentOutcomePublisher>();
            services.AddScoped<Features.OnlinePayments.IPaymentNotificationDispatcher, Features.OnlinePayments.PaymentNotificationDispatcher>();
            services.AddScoped<Features.OnlinePayments.ITenantPaymentPanelService, Features.OnlinePayments.TenantPaymentPanelService>();
            services.AddScoped<Features.OnlinePayments.IInvoiceReceiptService, Features.OnlinePayments.InvoiceReceiptService>();
            services.AddScoped<Features.OnlinePayments.ISePaySyncService, Features.OnlinePayments.SePaySyncService>();
            services.AddScoped<Features.HoaDons.Payments.InvoicePaymentTransaction>();
            services.AddSingleton<Features.HoaDons.Payments.IPaymentCodeGenerator, Features.HoaDons.Payments.PaymentCodeGenerator>();
            services.AddScoped<ILichSuThanhToanService, LichSuThanhToanService>();

            // Phase 6.5 services
            services.AddScoped<IYeuCauSuCoService, YeuCauSuCoService>();
            services.AddScoped<IThongBaoService, ThongBaoService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IViecCanLamService, ViecCanLamService>();

            // AI assistant
            services.AddScoped<Features.AiAssistants.Services.IAiAssistantService, Features.AiAssistants.Services.AiAssistantService>();
            services.AddScoped<Features.AiAssistants.Tools.AiToolExecutor>();

            // Phase 8 Background Job Use Cases
            services.AddScoped<Features.HopDongs.UseCases.IContractAutoCloseUseCase, Features.HopDongs.UseCases.ContractAutoCloseUseCase>();
            services.AddScoped<Features.HopDongs.UseCases.IContractExpiryAlertUseCase, Features.HopDongs.UseCases.ContractExpiryAlertUseCase>();
            services.AddScoped<Features.HoaDons.UseCases.IInvoiceReminderUseCase, Features.HoaDons.UseCases.InvoiceReminderUseCase>();

            // Security & Employee Access Services
            services.AddScoped<Abstractions.Security.IEmployeeAccessService, Features.NhanViens.Services.EmployeeAccessService>();
            services.AddScoped<Features.NhanViens.Services.IEmployeeBranchAssignmentService, Features.NhanViens.Services.EmployeeBranchAssignmentService>();
            services.AddScoped<Features.NguoiDungs.Services.IUserSessionService, Features.NguoiDungs.Services.UserSessionService>();

            Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, System.TimeProvider.System);

            return services;
        }
    }
}
