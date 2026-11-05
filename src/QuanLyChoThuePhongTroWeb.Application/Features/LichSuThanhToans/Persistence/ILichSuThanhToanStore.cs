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
        // tuNgay/denNgay là ngày lịch Việt Nam (gồm cả hai đầu); ngày thanh toán lưu UTC nên store đổi sang mốc UTC+7.
        Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
            DataTableRequest request, int chiNhanhId, int phuongThuc, DateTime? tuNgay, DateTime? denNgay, bool chiCoTienThua = false, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);

        // tuNgay/denNgay: như GetDanhSachThanhToanAsync.
        Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int chiNhanhId, DateTime? tuNgay, DateTime? denNgay, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<LichSuThanhToan>> GetLichSuByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default);
    }
}
