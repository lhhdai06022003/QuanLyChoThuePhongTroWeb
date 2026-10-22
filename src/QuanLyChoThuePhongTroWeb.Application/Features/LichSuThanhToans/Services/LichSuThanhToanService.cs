using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Services
{
    public class LichSuThanhToanService : ILichSuThanhToanService
    {
        private readonly ILichSuThanhToanStore _store;
        private readonly IEmployeeAccessService _access;

        public LichSuThanhToanService(ILichSuThanhToanStore store, IEmployeeAccessService access)
        {
            _store = store;
            _access = access;
        }

        // Admin xem mọi chi nhánh; nhân viên chỉ thấy chi nhánh đang được phân công. Allowed = false nếu không có quyền xem.
        private async Task<(bool Allowed, IReadOnlyCollection<int>? BranchIds)> ResolveBranchesAsync(int actorId, int chiNhanhId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || (chiNhanhId > 0 && !scope.CanAccessBranch(chiNhanhId)))
            {
                return (false, null);
            }

            return (true, scope.IsAdmin ? null : scope.ActiveBranchIds.ToList());
        }

        public async Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
            int actorId, DataTableRequest request, int chiNhanhId, int phuongThuc, DateTime? tuNgay, DateTime? denNgay, bool chiCoTienThua = false)
        {
            var (allowed, branchIds) = await ResolveBranchesAsync(actorId, chiNhanhId);
            if (!allowed)
            {
                return new DataTableResponse<LichSuThanhToanGiaoDichRes> { draw = request.Draw };
            }

            var page = await _store.GetDanhSachThanhToanAsync(request, chiNhanhId, phuongThuc, tuNgay, denNgay, chiCoTienThua, branchIds);
            foreach (var row in page.data)
            {
                var info = PaymentNoteTags.Parse(row.GhiChu);
                row.TienThua = info.TienThua;
                row.ChenhLech = info.ChenhLech;
                row.NhanGhiChu = PaymentNoteTags.Labels(info).ToList();
                row.NoiDungGhiChu = info.NoiDung;
            }

            return page;
        }

        public async Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int actorId, int chiNhanhId, DateTime? tuNgay, DateTime? denNgay)
        {
            var (allowed, branchIds) = await ResolveBranchesAsync(actorId, chiNhanhId);
            if (!allowed)
            {
                return new ThongKeThanhToanRes();
            }

            return await _store.GetThongKeThanhToanAsync(chiNhanhId, tuNgay, denNgay, branchIds);
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
                    GhiChu = PaymentNoteTags.ToDisplayText(l.GhiChu)
                });
            }
            return result;
        }
    }
}
