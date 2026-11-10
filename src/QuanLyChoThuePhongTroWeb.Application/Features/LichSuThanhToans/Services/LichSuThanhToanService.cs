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
        private async Task<(bool Allowed, IReadOnlyCollection<int>? BranchIds, bool IsAdmin)> ResolveBranchesAsync(int actorId, int chiNhanhId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || (chiNhanhId > 0 && !scope.CanAccessBranch(chiNhanhId)))
            {
                return (false, null, false);
            }

            return (true, scope.IsAdmin ? null : scope.ActiveBranchIds.ToList(), scope.IsAdmin);
        }

        // Chuẩn hóa bộ lọc thô từ Web. Slug kênh thu lạ hoặc rỗng nghĩa là mọi kênh. Danh sách áp kênh và tiền thừa;
        // thống kê thì không. Kỳ hóa đơn chỉ Admin được dùng (nhân viên gửi kỳ thì bỏ qua, spec 11.4).
        private static LichSuThanhToanLoc ChuanHoa(LichSuThanhToanLocReq loc, int actorId, bool isAdmin, bool choDanhSach)
        {
            var (nguoiThu, nguoiThuId) = ChuanHoaNguoiThu(loc.NguoiThu, actorId);
            var coKy = isAdmin
                && loc.KyThang is >= 1 and <= 12
                && loc.KyNam is >= 2000 and <= 2100;

            return new LichSuThanhToanLoc
            {
                ChiNhanhId = loc.ChiNhanhId,
                Kenh = choDanhSach && KenhThuSlugs.TryParse(loc.PhuongThuc, out var kenh) ? kenh : null,
                TuNgay = loc.TuNgay,
                DenNgay = loc.DenNgay,
                ChiCoTienThua = choDanhSach && loc.ChiCoTienThua,
                NguoiThu = nguoiThu,
                NguoiThuId = nguoiThuId,
                KyThang = coKy ? loc.KyThang : null,
                KyNam = coKy ? loc.KyNam : null
            };
        }

        // "toi" là chính người xem; "he-thong" là khoản không có người xác nhận (SePay, VNPay, tự động);
        // số nguyên dương là một NguoiDungId; còn lại là tất cả. Phạm vi chi nhánh vẫn do store áp.
        private static (NguoiThuLoai Loai, int? Id) ChuanHoaNguoiThu(string? nguoiThu, int actorId)
        {
            var s = nguoiThu?.Trim();
            if (string.IsNullOrEmpty(s))
            {
                return (NguoiThuLoai.TatCa, null);
            }

            if (s.Equals("toi", StringComparison.OrdinalIgnoreCase))
            {
                return (NguoiThuLoai.NguoiDung, actorId);
            }

            if (s.Equals("he-thong", StringComparison.OrdinalIgnoreCase))
            {
                return (NguoiThuLoai.HeThong, null);
            }

            if (int.TryParse(s, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0)
            {
                return (NguoiThuLoai.NguoiDung, id);
            }

            return (NguoiThuLoai.TatCa, null);
        }

        public async Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(int actorId, DataTableRequest request, LichSuThanhToanLocReq loc)
        {
            var (allowed, branchIds, isAdmin) = await ResolveBranchesAsync(actorId, loc.ChiNhanhId);
            if (!allowed)
            {
                return new DataTableResponse<LichSuThanhToanGiaoDichRes> { draw = request.Draw };
            }

            var page = await _store.GetDanhSachThanhToanAsync(request, ChuanHoa(loc, actorId, isAdmin, true), branchIds);
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

        public async Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int actorId, LichSuThanhToanLocReq loc)
        {
            var (allowed, branchIds, isAdmin) = await ResolveBranchesAsync(actorId, loc.ChiNhanhId);
            if (!allowed)
            {
                return new ThongKeThanhToanRes();
            }

            // Ô tổng áp chi nhánh, khoảng ngày, kỳ (Admin) và người thu; không áp kênh thu và "chỉ khoản có tiền thừa"
            // vì các ô theo kênh đã tách sẵn.
            var chuanHoa = ChuanHoa(loc, actorId, isAdmin, false);
            var stats = await _store.GetThongKeThanhToanAsync(chuanHoa, branchIds);
            stats.LocTheoKy = chuanHoa.KyThang.HasValue && chuanHoa.KyNam.HasValue;
            stats.KyThang = chuanHoa.KyThang;
            stats.KyNam = chuanHoa.KyNam;
            return stats;
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
