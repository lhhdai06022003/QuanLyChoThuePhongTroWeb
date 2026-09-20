using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Services
{
    public class LichSuThanhToanService : ILichSuThanhToanService
    {
        private readonly ILichSuThanhToanStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<LichSuThanhToanService> _logger;

        public LichSuThanhToanService(
            ILichSuThanhToanStore store,
            IUnitOfWork unitOfWork,
            ILogger<LichSuThanhToanService> logger)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
            DataTableRequest request, int chiNhanhId, int phuongThuc, DateTime? tuNgay, DateTime? denNgay)
        {
            return await _store.GetDanhSachThanhToanAsync(request, chiNhanhId, phuongThuc, tuNgay, denNgay);
        }

        public async Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int chiNhanhId, DateTime? tuNgay, DateTime? denNgay)
        {
            return await _store.GetThongKeThanhToanAsync(chiNhanhId, tuNgay, denNgay);
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> HuyGiaoDichAsync(int id, int adminUserId)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var lstt = await _store.GetByIdWithHoaDonAsync(id);

                if (lstt == null)
                {
                    return (false, "Không tìm thấy giao dịch hoặc giao dịch đã bị hủy trước đó.");
                }

                var hoaDon = lstt.HoaDon;
                if (hoaDon == null)
                {
                    return (false, "Không tìm thấy hóa đơn liên quan.");
                }

                lstt.IsDeleted = true;
                _store.UpdateLichSu(lstt);

                hoaDon.TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan;
                hoaDon.NgayCapNhat = DateTime.UtcNow;
                _store.UpdateHoaDon(hoaDon);

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi hệ thống khi hủy giao dịch thanh toán ID: {Id} bởi Admin: {AdminId}", id, adminUserId);
                return (false, "Lỗi hệ thống khi hủy giao dịch thanh toán.");
            }
        }

        public async Task<IReadOnlyList<LichSuThanhToanKhachThueDto>> GetLichSuByNguoiThueIdAsync(int nguoiThueId)
        {
            var list = await _store.GetLichSuByNguoiThueIdAsync(nguoiThueId);
            var result = new List<LichSuThanhToanKhachThueDto>();
            foreach (var l in list)
            {
                result.Add(new LichSuThanhToanKhachThueDto
                {
                    LichSuThanhToanId = l.LichSuThanhToanId,
                    MaGiaoDich = l.MaGiaoDich,
                    MaHoaDon = l.HoaDon?.MaHoaDon ?? string.Empty,
                    SoPhong = l.HoaDon?.HopDong?.PhongTro?.SoPhong ?? string.Empty,
                    SoTienThanhToan = l.SoTienThanhToan,
                    PhuongThucThanhToan = (int)l.PhuongThucThanhToan,
                    NgayThanhToan = l.NgayThanhToan,
                    GhiChu = l.GhiChu
                });
            }
            return result;
        }
    }
}
