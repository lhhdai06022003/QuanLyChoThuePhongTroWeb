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
            services.AddScoped<INguoiThueService, NguoiThueService>();
            services.AddScoped<INguoiDungService, NguoiDungService>();

            // Phase 6.3 services
            services.AddScoped<IHopDongService, HopDongService>();
            services.AddScoped<IThanhVienHopDongService, ThanhVienHopDongService>();
            services.AddScoped<IDienNuocService, DienNuocService>();

            // Phase 6.4 services
            services.AddScoped<IHoaDonCalculatorService, HoaDonCalculatorService>();
            services.AddScoped<IHoaDonService, HoaDonService>();
            services.AddScoped<ILichSuThanhToanService, LichSuThanhToanService>();

            // Phase 6.5 services
            services.AddScoped<IYeuCauSuCoService, YeuCauSuCoService>();
            services.AddScoped<IThongBaoService, ThongBaoService>();
            services.AddScoped<IDashboardService, DashboardService>();

            // Phase 8 Background Job Use Cases
            services.AddScoped<Features.HopDongs.UseCases.IContractAutoCloseUseCase, Features.HopDongs.UseCases.ContractAutoCloseUseCase>();
            services.AddScoped<Features.HopDongs.UseCases.IContractExpiryAlertUseCase, Features.HopDongs.UseCases.ContractExpiryAlertUseCase>();
            services.AddScoped<Features.HoaDons.UseCases.IInvoiceReminderUseCase, Features.HoaDons.UseCases.InvoiceReminderUseCase>();

            return services;
        }
    }
}
