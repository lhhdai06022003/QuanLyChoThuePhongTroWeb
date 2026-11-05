using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    public class ThanhLyHopDongService : IThanhLyHopDongService
    {
        private const int GhiChuToiDa = 1000;

        private readonly IHopDongLifecycleStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _access;
        private readonly IThongBaoService _thongBao;
        private readonly IBienBanThanhLyExporter _exporter;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ThanhLyHopDongService> _logger;

        public ThanhLyHopDongService(
            IHopDongLifecycleStore store,
            IUnitOfWork unitOfWork,
            IEmployeeAccessService access,
            IThongBaoService thongBao,
            IBienBanThanhLyExporter exporter,
            TimeProvider timeProvider,
            ILogger<ThanhLyHopDongService> logger)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _access = access;
            _thongBao = thongBao;
            _exporter = exporter;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<ServiceResult<ThanhLyHopDongPreviewDto>> GetPreviewAsync(int actorId, int hopDongId, CancellationToken ct = default)
        {
            var hopDong = await _store.GetHopDongAsync(hopDongId, ct);
            if (hopDong == null)
            {
                return ServiceResult<ThanhLyHopDongPreviewDto>.NotFound(HopDongAccessGuard.KhongTimThayHopDong);
            }

            var denied = await HopDongAccessGuard.CheckAsync(_access, actorId, hopDong.PhongTro.ChiNhanhId, null, ct);
            if (denied != null)
            {
                return HopDongAccessGuard.As<ThanhLyHopDongPreviewDto>(denied);
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var chain = await LoadChainAsync(hopDong, ct);
            var invoices = await _store.GetInvoicesOfContractsAsync(chain.Select(c => c.HopDongId).ToList(), ct);
            var invoiceIds = invoices.Select(i => i.HoaDonId).ToList();
            var paid = await _store.GetPaidTotalsAsync(invoiceIds, ct);
            var requests = await _store.GetActivePaymentRequestsAsync(invoiceIds, nowUtc, ct);
            var inputs = BuildInputs(invoices, chain, paid, requests);

            var tinh = ThanhLyCalculator.Tinh(hopDong.TienCocPhong, inputs);
            var lyDoChan = ThanhLyCalculator.TimLyDoChan(hopDong.TrangThaiHopDong, hopDong.ThoiDiemKetThuc, hopDong.TienCocPhong, hopDong.HopDongId, inputs);
            var members = await _store.GetMembersAsync(hopDongId, ct);

            var dto = new ThanhLyHopDongPreviewDto
            {
                HopDongId = hopDong.HopDongId,
                MaHopDong = hopDong.MaHopDong,
                SoPhong = hopDong.PhongTro.SoPhong,
                TenChiNhanh = hopDong.PhongTro.ChiNhanh.TenChiNhanh,
                TenNguoiDaiDien = hopDong.NguoiThue.HoVaTen,
                ThanhVienDangO = members.Where(m => m.NgayChuyenDi == null).Select(m => m.NguoiThue.HoVaTen).ToList(),
                ThoiDiemBatDauUtc = hopDong.ThoiDiemBatDau,
                ThoiDiemKetThucUtc = hopDong.ThoiDiemKetThuc,
                TienCoc = hopDong.TienCocPhong,
                HoaDonNo = tinh.HoaDonNo.Select(p => new ThanhLyHoaDonDto
                {
                    HoaDonId = p.HoaDonId,
                    MaHoaDon = p.MaHoaDon,
                    HopDongId = p.HopDongId,
                    MaHopDong = p.MaHopDong,
                    Thang = p.Thang,
                    Nam = p.Nam,
                    TongTien = p.TongTien,
                    DaThu = p.DaThu,
                    ConLai = p.ConLai,
                    CanTru = p.CanTru
                }).ToList(),
                TongNo = tinh.TongNo,
                CanTru = tinh.CanTru,
                CocHoanTrucTiep = tinh.CocHoanTrucTiep,
                ConNoSauCanTru = tinh.ConNoSauCanTru,
                LyDoChan = lyDoChan.ToList()
            };

            return ServiceResult<ThanhLyHopDongPreviewDto>.Ok(dto);
        }

        public async Task<ServiceResult<ThanhLyHopDongResult>> ThanhLyAsync(int actorId, ThanhLyHopDongRequest request, CancellationToken ct = default)
        {
            var info = await _store.GetHopDongAsync(request.HopDongId, ct);
            if (info == null)
            {
                return ServiceResult<ThanhLyHopDongResult>.NotFound(HopDongAccessGuard.KhongTimThayHopDong);
            }

            var denied = await HopDongAccessGuard.CheckAsync(_access, actorId, info.PhongTro.ChiNhanhId, EmployeeActionCodes.ContractSettle, ct);
            if (denied != null)
            {
                return HopDongAccessGuard.As<ThanhLyHopDongResult>(denied);
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var phongTroId = info.PhongTroId;
            var soPhong = info.PhongTro.SoPhong;

            await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
            ThanhLyHopDongResult result;
            string maHopDong;
            try
            {
                var hopDong = await _store.LockHopDongAsync(request.HopDongId, ct);
                if (hopDong == null)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<ThanhLyHopDongResult>.NotFound(HopDongAccessGuard.KhongTimThayHopDong);
                }

                if (hopDong.TrangThaiHopDong != TrangThaiHopDong.DangHoatDong)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<ThanhLyHopDongResult>.Fail("Hợp đồng đã được thanh lý hoặc gia hạn. Vui lòng tải lại trang.");
                }

                maHopDong = hopDong.MaHopDong;
                var chain = await LoadChainAsync(hopDong, ct);
                var invoices = await _store.LockInvoicesOfContractsAsync(chain.Select(c => c.HopDongId).ToList(), ct);
                var invoiceIds = invoices.Select(i => i.HoaDonId).ToList();
                var paid = await _store.GetPaidTotalsAsync(invoiceIds, ct);
                var requests = await _store.GetActivePaymentRequestsAsync(invoiceIds, nowUtc, ct);
                var inputs = BuildInputs(invoices, chain, paid, requests);

                var lyDoChan = ThanhLyCalculator.TimLyDoChan(hopDong.TrangThaiHopDong, hopDong.ThoiDiemKetThuc, hopDong.TienCocPhong, hopDong.HopDongId, inputs);
                if (lyDoChan.Count > 0)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<ThanhLyHopDongResult>.Fail(string.Join(" ", lyDoChan));
                }

                var tinh = ThanhLyCalculator.Tinh(hopDong.TienCocPhong, inputs);
                if (tinh.TongNo != request.TongNoLucMo)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<ThanhLyHopDongResult>.Fail(
                        $"Số nợ đã thay đổi từ {InvoicePaymentPolicy.FormatVnd(request.TongNoLucMo)} đ thành {InvoicePaymentPolicy.FormatVnd(tinh.TongNo)} đ. Vui lòng tải lại trang.");
                }

                if (tinh.ConNoSauCanTru > 0 && !request.XacNhanConNo)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<ThanhLyHopDongResult>.Fail(
                        $"Cần xác nhận khách còn nợ {InvoicePaymentPolicy.FormatVnd(tinh.ConNoSauCanTru)} đ sau khi cấn trừ cọc.");
                }

                var ghiChuCanTru = $"Cấn trừ tiền cọc hợp đồng {hopDong.MaHopDong}";
                foreach (var phanBo in tinh.HoaDonNo.Where(p => p.CanTru > 0))
                {
                    _store.AddLedgerEntry(new LichSuThanhToan
                    {
                        HoaDonId = phanBo.HoaDonId,
                        MaGiaoDich = $"{InvoicePaymentPolicy.CanTruMaGiaoDichPrefix}HD{hopDong.HopDongId}-{phanBo.HoaDonId}",
                        SoTienThanhToan = phanBo.CanTru,
                        PhuongThucThanhToan = PhuongThucThanhToan.CanTruTienCoc,
                        NgayThanhToan = nowUtc,
                        NgayXacNhan = nowUtc,
                        NguoiXacNhanId = actorId,
                        GhiChu = ghiChuCanTru
                    });

                    foreach (var yeuCau in requests.Where(y => y.HoaDonId == phanBo.HoaDonId && y.TrangThai == TrangThaiYeuCauThanhToan.ChoThanhToan))
                    {
                        yeuCau.Huy(actorId, nowUtc, "Đã cấn trừ tiền cọc khi thanh lý");
                    }

                    var hoaDon = invoices.First(i => i.HoaDonId == phanBo.HoaDonId);
                    hoaDon.CapNhatTrangThaiThanhToan(phanBo.DaThu + phanBo.CanTru, actorId, ghiChuCanTru, nowUtc);
                }

                hopDong.TrangThaiHopDong = TrangThaiHopDong.DaKetThuc;
                hopDong.NgayCapNhat = nowUtc;

                if (!await _store.IsRoomRentedExcludingContractAsync(hopDong.PhongTroId, hopDong.HopDongId, ct))
                {
                    var phong = await _store.GetPhongTroForUpdateAsync(hopDong.PhongTroId, ct);
                    if (phong != null)
                    {
                        phong.TrangThai = TrangThaiPhong.Trong;
                        phong.NgayCapNhat = nowUtc;
                    }
                }

                var ketThucUtc = hopDong.ThoiDiemKetThuc!.Value;
                foreach (var member in await _store.GetActiveMembersAsync(hopDong.HopDongId, ct))
                {
                    member.NgayChuyenDi = ketThucUtc;
                }

                foreach (var dichVu in await _store.GetActiveServicesOfRoomAsync(hopDong.PhongTroId, ct))
                {
                    dichVu.NgayKetThuc = dichVu.NgayBatDau > ketThucUtc ? dichVu.NgayBatDau : ketThucUtc;
                }

                await _unitOfWork.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                result = new ThanhLyHopDongResult
                {
                    HopDongId = hopDong.HopDongId,
                    CanTru = tinh.CanTru,
                    CocHoanTrucTiep = tinh.CocHoanTrucTiep,
                    ConNoSauCanTru = tinh.ConNoSauCanTru
                };
            }
            catch (OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogError(ex, "Lỗi khi thanh lý hợp đồng {HopDongId}.", request.HopDongId);
                return ServiceResult<ThanhLyHopDongResult>.Fail("Lỗi hệ thống khi thanh lý hợp đồng.");
            }

            try
            {
                await _thongBao.GuiChoNguoiPhuTrachPhongAsync(
                    phongTroId,
                    $"Đã thanh lý hợp đồng {maHopDong}",
                    $"Phòng {soPhong}: cấn trừ cọc {InvoicePaymentPolicy.FormatVnd(result.CanTru)} đ, hoàn cọc trực tiếp {InvoicePaymentPolicy.FormatVnd(result.CocHoanTrucTiep)} đ, khách còn nợ {InvoicePaymentPolicy.FormatVnd(result.ConNoSauCanTru)} đ.",
                    "HopDong",
                    $"/QuanLyNhaTro/HopDong/ChiTiet/{result.HopDongId}",
                    ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không gửi được thông báo thanh lý hợp đồng {HopDongId}.", result.HopDongId);
            }

            return ServiceResult<ThanhLyHopDongResult>.Ok(result, "Đã thanh lý hợp đồng.");
        }

        public async Task<ServiceResult<byte[]>> XuatBienBanAsync(int actorId, int hopDongId, string? ghiChu, CancellationToken ct = default)
        {
            var hopDong = await _store.GetHopDongAsync(hopDongId, ct);
            if (hopDong == null)
            {
                return ServiceResult<byte[]>.NotFound(HopDongAccessGuard.KhongTimThayHopDong);
            }

            var denied = await HopDongAccessGuard.CheckAsync(_access, actorId, hopDong.PhongTro.ChiNhanhId, null, ct);
            if (denied != null)
            {
                return HopDongAccessGuard.As<byte[]>(denied);
            }

            var chain = await LoadChainAsync(hopDong, ct);
            var chiSoHienTai = HopDongCodeChain.RenewalIndex(hopDong.MaHopDong);
            var daGiaHan = chain.Any(c => HopDongCodeChain.RenewalIndex(c.MaHopDong) > chiSoHienTai);
            if (hopDong.TrangThaiHopDong != TrangThaiHopDong.DaKetThuc || daGiaHan)
            {
                return ServiceResult<byte[]>.Fail("Hợp đồng chưa thanh lý, không có biên bản.");
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var maTheoHopDong = chain.ToDictionary(c => c.HopDongId, c => c.MaHopDong);
            var invoices = (await _store.GetInvoicesOfContractsAsync(chain.Select(c => c.HopDongId).ToList(), ct))
                .Where(i => i.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui)
                .OrderBy(i => i.Nam).ThenBy(i => i.Thang).ThenBy(i => i.HoaDonId)
                .ToList();
            var invoiceIds = invoices.Select(i => i.HoaDonId).ToList();
            var paid = await _store.GetPaidTotalsAsync(invoiceIds, ct);
            var offsets = await _store.GetOffsetTotalsAsync(invoiceIds, ct);
            var members = await _store.GetMembersAsync(hopDongId, ct);

            var dongs = new List<BienBanHoaDonDto>();
            foreach (var invoice in invoices)
            {
                var daThu = paid.TryGetValue(invoice.HoaDonId, out var p) ? p : 0m;
                var canTru = offsets.TryGetValue(invoice.HoaDonId, out var o) ? o : 0m;
                var conLai = Math.Max(0m, invoice.TongTien - daThu);
                if (canTru <= 0 && conLai <= 0)
                {
                    continue;
                }

                dongs.Add(new BienBanHoaDonDto
                {
                    MaHoaDon = invoice.MaHoaDon,
                    MaHopDong = maTheoHopDong.TryGetValue(invoice.HopDongId, out var ma) ? ma : hopDong.MaHopDong,
                    Thang = invoice.Thang,
                    Nam = invoice.Nam,
                    TongTien = invoice.TongTien,
                    DaThuKhac = daThu - canTru,
                    CanTru = canTru,
                    ConLai = conLai
                });
            }

            var tongCanTru = dongs.Sum(d => d.CanTru);
            var chiNhanh = hopDong.PhongTro.ChiNhanh;
            var ghiChuTrim = ghiChu?.Trim();
            if (string.IsNullOrEmpty(ghiChuTrim))
            {
                ghiChuTrim = null;
            }
            else if (ghiChuTrim.Length > GhiChuToiDa)
            {
                ghiChuTrim = ghiChuTrim.Substring(0, GhiChuToiDa);
            }

            var dto = new BienBanThanhLyDto
            {
                MaHopDong = hopDong.MaHopDong,
                TenChiNhanh = chiNhanh.TenChiNhanh,
                DiaChiChiNhanh = chiNhanh.DiaChi,
                SoDienThoaiChiNhanh = chiNhanh.SoDienThoai,
                SoPhong = hopDong.PhongTro.SoPhong,
                TenNguoiDaiDien = hopDong.NguoiThue.HoVaTen,
                CccdNguoiDaiDien = hopDong.NguoiThue.CCCD,
                SdtNguoiDaiDien = hopDong.NguoiThue.SoDienThoai,
                ThanhVien = members.Select(m => m.NguoiThue.HoVaTen).ToList(),
                ThoiDiemBatDauUtc = hopDong.ThoiDiemBatDau,
                ThoiDiemKetThucUtc = hopDong.ThoiDiemKetThuc ?? hopDong.NgayCapNhat ?? nowUtc,
                NgayThanhLyUtc = hopDong.NgayCapNhat ?? nowUtc,
                TienCoc = hopDong.TienCocPhong,
                TongCanTru = tongCanTru,
                CocHoanTrucTiep = Math.Max(0m, hopDong.TienCocPhong - tongCanTru),
                NoConLai = dongs.Sum(d => d.ConLai),
                NgayInUtc = nowUtc,
                HoaDons = dongs,
                GhiChu = ghiChuTrim
            };

            return ServiceResult<byte[]>.Ok(_exporter.ExportPdf(dto));
        }

        // Chuỗi gia hạn: hợp đồng chưa xóa, không DaHuy, cùng mã gốc (luôn gồm chính hợp đồng đang xét).
        private async Task<List<HopDong>> LoadChainAsync(HopDong hopDong, CancellationToken ct)
        {
            var goc = HopDongCodeChain.RootOf(hopDong.MaHopDong);
            var chain = (await _store.GetChainAsync(goc, ct))
                .Where(x => HopDongCodeChain.BelongsTo(x.MaHopDong, goc) && x.TrangThaiHopDong != TrangThaiHopDong.DaHuy)
                .ToList();
            if (chain.All(x => x.HopDongId != hopDong.HopDongId))
            {
                chain.Add(hopDong);
            }

            return chain;
        }

        private static List<ThanhLyHoaDonInput> BuildInputs(
            IReadOnlyList<HoaDon> invoices,
            IReadOnlyList<HopDong> chain,
            IReadOnlyDictionary<int, decimal> paid,
            IReadOnlyList<YeuCauThanhToanHoaDon> requests)
        {
            var maTheoHopDong = chain.ToDictionary(c => c.HopDongId, c => c.MaHopDong);
            return invoices.Select(i => new ThanhLyHoaDonInput(
                i.HoaDonId,
                i.MaHoaDon,
                i.HopDongId,
                maTheoHopDong.TryGetValue(i.HopDongId, out var ma) ? ma : string.Empty,
                i.Thang,
                i.Nam,
                i.TongTien,
                paid.TryGetValue(i.HoaDonId, out var daThu) ? daThu : 0m,
                i.TrangThaiPhatHanh,
                requests.Any(y => y.HoaDonId == i.HoaDonId && y.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu),
                i.TongTienLaSoNguyen())).ToList();
        }
    }
}
