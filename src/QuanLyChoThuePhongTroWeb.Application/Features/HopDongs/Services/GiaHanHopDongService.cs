using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    public class GiaHanHopDongService : IGiaHanHopDongService
    {
        private const int SoLanThuSinhMa = 5;

        private readonly IHopDongLifecycleStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _access;
        private readonly IThongBaoService _thongBao;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<GiaHanHopDongService> _logger;

        public GiaHanHopDongService(
            IHopDongLifecycleStore store,
            IUnitOfWork unitOfWork,
            IEmployeeAccessService access,
            IThongBaoService thongBao,
            TimeProvider timeProvider,
            ILogger<GiaHanHopDongService> logger)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _access = access;
            _thongBao = thongBao;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<ServiceResult<GiaHanHopDongFormDto>> GetFormAsync(int actorId, int hopDongId, CancellationToken ct = default)
        {
            var hopDong = await _store.GetHopDongAsync(hopDongId, ct);
            if (hopDong == null)
            {
                return ServiceResult<GiaHanHopDongFormDto>.NotFound(HopDongAccessGuard.KhongTimThayHopDong);
            }

            var denied = await HopDongAccessGuard.CheckAsync(_access, actorId, hopDong.PhongTro.ChiNhanhId, null, ct);
            if (denied != null)
            {
                return HopDongAccessGuard.As<GiaHanHopDongFormDto>(denied);
            }

            if (hopDong.TrangThaiHopDong != TrangThaiHopDong.DangHoatDong)
            {
                return ServiceResult<GiaHanHopDongFormDto>.Fail("Chỉ gia hạn được hợp đồng đang hoạt động.");
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var macDinh = GiaHanDefaults.Tinh(hopDong.ThoiDiemBatDau, hopDong.ThoiDiemKetThuc, nowUtc);
            var goc = HopDongCodeChain.RootOf(hopDong.MaHopDong);
            var soGiaHan = await _store.CountActiveRenewalsAsync(goc, ct);
            var dieuKhoan = await _store.GetTermsAsync(hopDongId, ct);
            var members = await _store.GetMembersAsync(hopDongId, ct);

            return ServiceResult<GiaHanHopDongFormDto>.Ok(new GiaHanHopDongFormDto
            {
                HopDongId = hopDong.HopDongId,
                MaHopDong = hopDong.MaHopDong,
                MaHopDongMoiDuKien = HopDongCodeChain.BuildRenewal(goc, soGiaHan + 1),
                SoPhong = hopDong.PhongTro.SoPhong,
                TenChiNhanh = hopDong.PhongTro.ChiNhanh.TenChiNhanh,
                TenNguoiDaiDien = hopDong.NguoiThue.HoVaTen,
                ThoiDiemBatDauUtc = hopDong.ThoiDiemBatDau,
                ThoiDiemKetThucUtc = hopDong.ThoiDiemKetThuc,
                TienThue = hopDong.TienThuePhong,
                TienCoc = hopDong.TienCocPhong,
                ThangBatDauMacDinh = macDinh.Thang,
                NamBatDauMacDinh = macDinh.Nam,
                NgayKetThucMacDinh = macDinh.NgayKetThuc,
                DieuKhoanHienTai = dieuKhoan.Select(d => d.TieuDe).ToList(),
                ThanhVienDangO = members.Where(m => m.NgayChuyenDi == null).Select(m => m.NguoiThue.HoVaTen).ToList()
            });
        }

        public async Task<ServiceResult<GiaHanHopDongResult>> GiaHanAsync(int actorId, GiaHanHopDongRequest request, CancellationToken ct = default)
        {
            if (request.ThangBatDau < 1 || request.ThangBatDau > 12 || request.NamBatDau < 2000 || request.NamBatDau > 2100)
            {
                return ServiceResult<GiaHanHopDongResult>.Fail("Tháng bắt đầu mới không hợp lệ.");
            }

            if (request.NgayKetThucMoi <= new DateOnly(request.NamBatDau, request.ThangBatDau, 1))
            {
                return ServiceResult<GiaHanHopDongResult>.Fail("Ngày kết thúc mới phải sau ngày bắt đầu mới.");
            }

            if (request.TienThue <= 0)
            {
                return ServiceResult<GiaHanHopDongResult>.Fail("Giá thuê mới phải lớn hơn 0.");
            }

            if (request.TienCoc < 0)
            {
                return ServiceResult<GiaHanHopDongResult>.Fail("Tiền cọc không được âm.");
            }

            // Cùng quy tắc với thanh lý (chặn cọc lẻ): tiền VND phải là số đồng nguyên.
            if (request.TienThue != decimal.Truncate(request.TienThue))
            {
                return ServiceResult<GiaHanHopDongResult>.Fail("Giá thuê mới phải là số đồng nguyên, không có số lẻ.");
            }

            if (request.TienCoc != decimal.Truncate(request.TienCoc))
            {
                return ServiceResult<GiaHanHopDongResult>.Fail("Tiền cọc phải là số đồng nguyên, không có số lẻ.");
            }

            var info = await _store.GetHopDongAsync(request.HopDongId, ct);
            if (info == null)
            {
                return ServiceResult<GiaHanHopDongResult>.NotFound(HopDongAccessGuard.KhongTimThayHopDong);
            }

            var denied = await HopDongAccessGuard.CheckAsync(_access, actorId, info.PhongTro.ChiNhanhId, EmployeeActionCodes.ContractRenew, ct);
            if (denied != null)
            {
                return HopDongAccessGuard.As<GiaHanHopDongResult>(denied);
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var newStartUtc = GiaHanDefaults.DauThangVnUtc(request.ThangBatDau, request.NamBatDau);
            var newEndUtc = GiaHanDefaults.CuoiNgayVnUtc(request.NgayKetThucMoi);
            var oldEndUtc = newStartUtc.AddSeconds(-1);
            var phongTroId = info.PhongTroId;
            var nguoiThueId = info.NguoiThueId;
            var soPhong = info.PhongTro.SoPhong;
            var maCu = info.MaHopDong;

            await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
            HopDong moi;
            try
            {
                var cu = await _store.LockHopDongAsync(request.HopDongId, ct);
                if (cu == null)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<GiaHanHopDongResult>.NotFound(HopDongAccessGuard.KhongTimThayHopDong);
                }

                if (cu.TrangThaiHopDong != TrangThaiHopDong.DangHoatDong)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<GiaHanHopDongResult>.Fail("Hợp đồng đã được thanh lý hoặc gia hạn. Vui lòng tải lại trang.");
                }

                if (newStartUtc <= cu.ThoiDiemBatDau)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<GiaHanHopDongResult>.Fail("Tháng bắt đầu mới phải sau ngày bắt đầu hợp đồng cũ.");
                }

                if (await _store.HasInvoiceFromPeriodAsync(cu.HopDongId, request.ThangBatDau, request.NamBatDau, ct))
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<GiaHanHopDongResult>.Fail(
                        $"Hợp đồng cũ đã có hóa đơn từ T{request.ThangBatDau}/{request.NamBatDau} trở đi. Chọn tháng bắt đầu sau kỳ hóa đơn cuối.");
                }

                if (await _store.IsRoomRentedExcludingContractAsync(cu.PhongTroId, cu.HopDongId, ct)
                    || await _store.IsTenantActiveInAnotherContractAsync(cu.NguoiThueId, cu.HopDongId, ct))
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<GiaHanHopDongResult>.Fail("Phòng hoặc người đại diện đang có hợp đồng hoạt động khác, không gia hạn được.");
                }

                var goc = HopDongCodeChain.RootOf(cu.MaHopDong);
                var n = await _store.CountActiveRenewalsAsync(goc, ct) + 1;
                string? maMoi = null;
                for (var lan = 0; lan < SoLanThuSinhMa; lan++, n++)
                {
                    var thu = HopDongCodeChain.BuildRenewal(goc, n);
                    if (!await _store.ExistsContractWithCodeAsync(thu, ct))
                    {
                        maMoi = thu;
                        break;
                    }
                }

                if (maMoi == null)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<GiaHanHopDongResult>.Fail("Không sinh được mã hợp đồng gia hạn, vui lòng thử lại.");
                }

                cu.ThoiDiemKetThuc = oldEndUtc;
                cu.TrangThaiHopDong = TrangThaiHopDong.DaKetThuc;
                cu.NgayCapNhat = nowUtc;

                moi = new HopDong
                {
                    MaHopDong = maMoi,
                    PhongTroId = cu.PhongTroId,
                    NguoiThueId = cu.NguoiThueId,
                    ThoiDiemBatDau = newStartUtc,
                    ThoiDiemKetThuc = newEndUtc,
                    TienThuePhong = request.TienThue,
                    TienCocPhong = request.TienCoc,
                    TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                    NgayTao = nowUtc,
                    IsDeleted = false
                };
                await _store.AddHopDongAsync(moi, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var thanhVienMoi = new List<ChiTietThanhVienHopDong>();
                foreach (var thanhVien in await _store.GetActiveMembersAsync(cu.HopDongId, ct))
                {
                    thanhVien.NgayChuyenDi = oldEndUtc;
                    thanhVienMoi.Add(new ChiTietThanhVienHopDong
                    {
                        HopDongId = moi.HopDongId,
                        NguoiThueId = thanhVien.NguoiThueId,
                        NgayVao = newStartUtc,
                        IsDeleted = false
                    });
                }

                if (thanhVienMoi.Count > 0)
                {
                    await _store.AddMembersAsync(thanhVienMoi, ct);
                }

                List<HopDongDieuKhoan> dieuKhoanMoi;
                if (request.DieuKhoanMauIds == null)
                {
                    dieuKhoanMoi = (await _store.GetTermsAsync(cu.HopDongId, ct))
                        .Select(d => new HopDongDieuKhoan { HopDongId = moi.HopDongId, TieuDe = d.TieuDe, NoiDung = d.NoiDung, ThuTu = d.ThuTu })
                        .ToList();
                }
                else if (request.DieuKhoanMauIds.Count == 0)
                {
                    dieuKhoanMoi = new List<HopDongDieuKhoan>();
                }
                else
                {
                    var thuTu = 1;
                    dieuKhoanMoi = (await _store.GetActiveTermTemplatesByIdsAsync(request.DieuKhoanMauIds, ct))
                        .Select(t => new HopDongDieuKhoan { HopDongId = moi.HopDongId, TieuDe = t.TieuDe, NoiDung = t.NoiDung, ThuTu = thuTu++ })
                        .ToList();
                }

                if (dieuKhoanMoi.Count > 0)
                {
                    await _store.AddTermsAsync(dieuKhoanMoi, ct);
                }

                await _unitOfWork.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogError(ex, "Lỗi khi gia hạn hợp đồng {HopDongId}.", request.HopDongId);
                return ServiceResult<GiaHanHopDongResult>.Fail("Lỗi hệ thống khi gia hạn hợp đồng.");
            }

            try
            {
                await _thongBao.GuiChoNguoiPhuTrachPhongAsync(
                    phongTroId,
                    $"Đã gia hạn hợp đồng {maCu} → {moi.MaHopDong}",
                    $"Phòng {soPhong}: hợp đồng mới {moi.MaHopDong} từ {Ngay(newStartUtc)} đến {Ngay(newEndUtc)}.",
                    "HopDong",
                    $"/QuanLyNhaTro/HopDong/ChiTiet/{moi.HopDongId}",
                    ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không gửi được thông báo gia hạn cho nhân viên, hợp đồng {HopDongId}.", moi.HopDongId);
            }

            try
            {
                await _thongBao.GuiChoKhachThueAsync(
                    nguoiThueId,
                    "Hợp đồng đã được gia hạn",
                    $"Hợp đồng mới {moi.MaHopDong} từ {Ngay(newStartUtc)} đến {Ngay(newEndUtc)}.",
                    "HopDong",
                    "/KhachThue/HopDong",
                    ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không gửi được thông báo gia hạn cho khách thuê, hợp đồng {HopDongId}.", moi.HopDongId);
            }

            return ServiceResult<GiaHanHopDongResult>.Ok(
                new GiaHanHopDongResult { HopDongMoiId = moi.HopDongId, MaHopDongMoi = moi.MaHopDong },
                "Đã gia hạn hợp đồng.");
        }

        private static string Ngay(DateTime utc) => utc.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }
}
