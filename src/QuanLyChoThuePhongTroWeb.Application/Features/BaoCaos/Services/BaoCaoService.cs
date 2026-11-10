using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services
{
    public class BaoCaoService : IBaoCaoService
    {
        private readonly IBaoCaoStore _store;
        private readonly IEmployeeAccessService _access;
        private readonly IBaoCaoExcelExporter _exporter;
        private readonly TimeProvider _timeProvider;

        public BaoCaoService(IBaoCaoStore store, IEmployeeAccessService access, IBaoCaoExcelExporter exporter, TimeProvider timeProvider)
        {
            _store = store;
            _access = access;
            _exporter = exporter;
            _timeProvider = timeProvider;
        }

        // Kết quả kiểm tra chung: phạm vi truy cập, danh sách kỳ, mô tả phạm vi và thời điểm tính.
        private sealed record Boi(IReadOnlyCollection<int>? AllowedBranchIds, int? ChiNhanhId, IReadOnlyList<BaoCaoKy> Kys, bool CoBangChiNhanh, BaoCaoPhamViDto PhamVi, DateTime NowUtc);

        public async Task<ServiceResult<BaoCaoDoanhThuDto>> GetDoanhThuAsync(int actorId, BaoCaoFilter filter, CancellationToken cancellationToken = default)
        {
            var (loi, b) = await KiemTraAsync(actorId, filter, true, true, cancellationToken);
            if (loi != null || b == null)
            {
                return ServiceResult<BaoCaoDoanhThuDto>.FailFrom(loi!);
            }

            var tuKey = b.Kys[0].Key;
            var denKey = b.Kys[^1].Key;
            var hoaDons = await _store.GetHoaDonDaGuiAsync(b.ChiNhanhId, b.AllowedBranchIds, tuKey, denKey, cancellationToken);
            var thanhToans = await _store.GetThanhToanCuaHoaDonDaGuiAsync(b.ChiNhanhId, b.AllowedBranchIds, tuKey, denKey, cancellationToken);
            var tienCoc = await _store.GetTienCocDangGiuAsync(b.ChiNhanhId, b.AllowedBranchIds, cancellationToken);
            var giuChos = await _store.GetGiuChoDaNhanTienAsync(b.ChiNhanhId, b.AllowedBranchIds, cancellationToken);
            return ServiceResult<BaoCaoDoanhThuDto>.Ok(BaoCaoDoanhThuBuilder.Build(b.PhamVi, b.Kys, b.CoBangChiNhanh, hoaDons, thanhToans, tienCoc, giuChos));
        }

        public async Task<ServiceResult<BaoCaoCongNoDto>> GetCongNoAsync(int actorId, BaoCaoFilter filter, CancellationToken cancellationToken = default)
        {
            var (loi, b) = await KiemTraAsync(actorId, filter, false, false, cancellationToken);
            if (loi != null || b == null)
            {
                return ServiceResult<BaoCaoCongNoDto>.FailFrom(loi!);
            }

            var rows = await _store.GetHoaDonConNoAsync(b.ChiNhanhId, b.AllowedBranchIds, cancellationToken);
            return ServiceResult<BaoCaoCongNoDto>.Ok(BaoCaoCongNoBuilder.Build(b.PhamVi, rows, b.NowUtc));
        }

        public async Task<ServiceResult<BaoCaoCongSuatDto>> GetCongSuatPhongAsync(int actorId, BaoCaoFilter filter, CancellationToken cancellationToken = default)
        {
            var (loi, b) = await KiemTraAsync(actorId, filter, true, false, cancellationToken);
            if (loi != null || b == null)
            {
                return ServiceResult<BaoCaoCongSuatDto>.FailFrom(loi!);
            }

            var phongs = await _store.GetPhongAsync(b.ChiNhanhId, b.AllowedBranchIds, cancellationToken);
            var hopDongs = await _store.GetHopDongAsync(b.ChiNhanhId, b.AllowedBranchIds, cancellationToken);
            return ServiceResult<BaoCaoCongSuatDto>.Ok(BaoCaoCongSuatBuilder.Build(b.PhamVi, b.Kys, b.CoBangChiNhanh, phongs, hopDongs, b.NowUtc));
        }

        public async Task<ServiceResult<BaoCaoHopDongDto>> GetHopDongAsync(int actorId, BaoCaoFilter filter, CancellationToken cancellationToken = default)
        {
            var (loi, b) = await KiemTraAsync(actorId, filter, true, false, cancellationToken);
            if (loi != null || b == null)
            {
                return ServiceResult<BaoCaoHopDongDto>.FailFrom(loi!);
            }

            var hopDongs = await _store.GetHopDongAsync(b.ChiNhanhId, b.AllowedBranchIds, cancellationToken);
            return ServiceResult<BaoCaoHopDongDto>.Ok(BaoCaoHopDongBuilder.Build(b.PhamVi, b.Kys, hopDongs, b.NowUtc));
        }

        public async Task<ServiceResult<BaoCaoDienNuocDto>> GetDienNuocAsync(int actorId, BaoCaoFilter filter, CancellationToken cancellationToken = default)
        {
            var (loi, b) = await KiemTraAsync(actorId, filter, true, false, cancellationToken);
            if (loi != null || b == null)
            {
                return ServiceResult<BaoCaoDienNuocDto>.FailFrom(loi!);
            }

            var rows = await _store.GetDienNuocDaDuyetAsync(b.ChiNhanhId, b.AllowedBranchIds, b.Kys[0].Key, b.Kys[^1].Key, cancellationToken);
            return ServiceResult<BaoCaoDienNuocDto>.Ok(BaoCaoDienNuocBuilder.Build(b.PhamVi, b.Kys, rows));
        }

        public async Task<ServiceResult<BaoCaoFileDto>> XuatExcelAsync(int actorId, BaoCaoTab tab, BaoCaoFilter filter, CancellationToken cancellationToken = default)
        {
            BaoCaoPhamViDto phamVi;
            byte[] noiDung;
            switch (tab)
            {
                case BaoCaoTab.DoanhThu:
                {
                    var r = await GetDoanhThuAsync(actorId, filter, cancellationToken);
                    if (!r.Success) return ServiceResult<BaoCaoFileDto>.FailFrom(r);
                    phamVi = r.Data!.PhamVi;
                    noiDung = _exporter.XuatDoanhThu(r.Data);
                    break;
                }
                case BaoCaoTab.CongNo:
                {
                    var r = await GetCongNoAsync(actorId, filter, cancellationToken);
                    if (!r.Success) return ServiceResult<BaoCaoFileDto>.FailFrom(r);
                    phamVi = r.Data!.PhamVi;
                    noiDung = _exporter.XuatCongNo(r.Data);
                    break;
                }
                case BaoCaoTab.CongSuat:
                {
                    var r = await GetCongSuatPhongAsync(actorId, filter, cancellationToken);
                    if (!r.Success) return ServiceResult<BaoCaoFileDto>.FailFrom(r);
                    phamVi = r.Data!.PhamVi;
                    noiDung = _exporter.XuatCongSuatPhong(r.Data);
                    break;
                }
                case BaoCaoTab.HopDong:
                {
                    var r = await GetHopDongAsync(actorId, filter, cancellationToken);
                    if (!r.Success) return ServiceResult<BaoCaoFileDto>.FailFrom(r);
                    phamVi = r.Data!.PhamVi;
                    noiDung = _exporter.XuatHopDong(r.Data);
                    break;
                }
                case BaoCaoTab.DienNuoc:
                {
                    var r = await GetDienNuocAsync(actorId, filter, cancellationToken);
                    if (!r.Success) return ServiceResult<BaoCaoFileDto>.FailFrom(r);
                    phamVi = r.Data!.PhamVi;
                    noiDung = _exporter.XuatDienNuoc(r.Data);
                    break;
                }
                default:
                    return ServiceResult<BaoCaoFileDto>.Fail("Tab báo cáo không hợp lệ.");
            }

            var tenFile = string.Format(
                CultureInfo.InvariantCulture,
                "BaoCao_{0}_{1}{2:00}_{3}{4:00}.xlsx",
                BaoCaoTabs.Slug(tab), phamVi.TuNam, phamVi.TuThang, phamVi.DenNam, phamVi.DenThang);
            return ServiceResult<BaoCaoFileDto>.Ok(new BaoCaoFileDto { NoiDung = noiDung, TenFile = tenFile });
        }

        private async Task<(ServiceResult? Loi, Boi? Boi)> KiemTraAsync(int actorId, BaoCaoFilter filter, bool canKhoangKy, bool chiAdmin, CancellationToken cancellationToken)
        {
            const string khongCoQuyen = "Bạn không có quyền xem báo cáo.";
            if (actorId <= 0)
            {
                return (ServiceResult.Forbidden(khongCoQuyen), null);
            }

            var scope = await _access.GetScopeAsync(actorId, cancellationToken);
            if (scope == null)
            {
                return (ServiceResult.Forbidden(khongCoQuyen), null);
            }

            if (chiAdmin && !scope.IsAdmin)
            {
                return (ServiceResult.Forbidden("Chỉ Admin được xem báo cáo doanh thu."), null);
            }

            if (!scope.IsAdmin && scope.ActiveBranchIds.Count == 0)
            {
                return (ServiceResult.Forbidden("Bạn chưa được phân công chi nhánh nào."), null);
            }

            string tenChiNhanh;
            if (filter.ChiNhanhId.HasValue)
            {
                var id = filter.ChiNhanhId.Value;
                if (id <= 0)
                {
                    return (ServiceResult.Fail("Chi nhánh không hợp lệ."), null);
                }

                if (!scope.CanAccessBranch(id)
                    || !await _access.CanPerformAsync(actorId, id, EmployeeActionCodes.ReportRead, cancellationToken))
                {
                    return (ServiceResult.Forbidden("Bạn không có quyền xem báo cáo của chi nhánh này."), null);
                }

                var ten = await _store.GetTenChiNhanhAsync(id, cancellationToken);
                if (ten == null)
                {
                    return (ServiceResult.NotFound("Không tìm thấy chi nhánh."), null);
                }

                tenChiNhanh = ten;
            }
            else
            {
                tenChiNhanh = scope.IsAdmin ? "Tất cả chi nhánh" : "Tất cả chi nhánh của tôi";
            }

            IReadOnlyList<BaoCaoKy> kys = Array.Empty<BaoCaoKy>();
            if (canKhoangKy)
            {
                var loiKhoang = BaoCaoCalculator.KiemTraKhoang(filter.TuThang, filter.TuNam, filter.DenThang, filter.DenNam);
                if (loiKhoang != null)
                {
                    return (ServiceResult.Fail(loiKhoang), null);
                }

                kys = BaoCaoCalculator.LietKeKy(new BaoCaoKy(filter.TuThang, filter.TuNam), new BaoCaoKy(filter.DenThang, filter.DenNam));
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var phamVi = new BaoCaoPhamViDto
            {
                ChiNhanhId = filter.ChiNhanhId,
                TenChiNhanh = tenChiNhanh,
                TuThang = filter.TuThang,
                TuNam = filter.TuNam,
                DenThang = filter.DenThang,
                DenNam = filter.DenNam,
                TuKyText = canKhoangKy ? new BaoCaoKy(filter.TuThang, filter.TuNam).Text : "",
                DenKyText = canKhoangKy ? new BaoCaoKy(filter.DenThang, filter.DenNam).Text : "",
                ThoiDiemTinhText = nowUtc.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
            };

            return (null, new Boi(scope.AllowedBranchIds, filter.ChiNhanhId, kys, !filter.ChiNhanhId.HasValue, phamVi, nowUtc));
        }
    }
}
