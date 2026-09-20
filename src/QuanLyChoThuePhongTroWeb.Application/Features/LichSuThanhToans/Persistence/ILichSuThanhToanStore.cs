using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Persistence
{
    public interface ILichSuThanhToanStore
    {
        Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
            DataTableRequest request, int chiNhanhId, int phuongThuc, DateTime? tuNgay, DateTime? denNgay, CancellationToken cancellationToken = default);

        Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int chiNhanhId, DateTime? tuNgay, DateTime? denNgay, CancellationToken cancellationToken = default);

        Task<LichSuThanhToan?> GetByIdWithHoaDonAsync(int id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<LichSuThanhToan>> GetLichSuByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default);

        void UpdateLichSu(LichSuThanhToan lichSu);
        void UpdateHoaDon(HoaDon hoaDon);
    }
}
