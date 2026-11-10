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
            DataTableRequest request, LichSuThanhToanLoc loc, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);

        // loc.TuNgay/DenNgay: như GetDanhSachThanhToanAsync. Thống kê bỏ qua loc.Kenh và loc.ChiCoTienThua.
        Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(
            LichSuThanhToanLoc loc, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<LichSuThanhToan>> GetLichSuByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default);
    }
}
