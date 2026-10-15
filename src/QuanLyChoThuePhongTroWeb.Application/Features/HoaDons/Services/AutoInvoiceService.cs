using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Formatting;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public class AutoInvoiceService : IAutoInvoiceService
    {
        private const string HuongDanThieuDonGia = "Đơn giá của kỳ được lưu lúc tạo kỳ; lập hóa đơn theo luồng tay và sửa dòng điện/nước ở bản nháp.";

        private readonly IAutoInvoiceStore _store;
        private readonly IMeterReadingWorkflowService _workflowService;
        private readonly IInvoiceIssuanceService _issuanceService;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IThongBaoService _thongBaoService;
        private readonly AutoInvoiceOptions _options;
        private readonly ILogger<AutoInvoiceService> _logger;

        public AutoInvoiceService(
            IAutoInvoiceStore store,
            IMeterReadingWorkflowService workflowService,
            IInvoiceIssuanceService issuanceService,
            IEmployeeAccessService employeeAccessService,
            IThongBaoService thongBaoService,
            AutoInvoiceOptions options,
            ILogger<AutoInvoiceService> logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _issuanceService = issuanceService ?? throw new ArgumentNullException(nameof(issuanceService));
            _employeeAccessService = employeeAccessService ?? throw new ArgumentNullException(nameof(employeeAccessService));
            _thongBaoService = thongBaoService ?? throw new ArgumentNullException(nameof(thongBaoService));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ServiceResult<AutoInvoiceRunResult>> RunForRoomAsync(int actorId, AutoInvoiceTrigger trigger, int phongTroId, int thang, int nam)
        {
            AutoInvoiceRoomContext? ctx = null;
            try
            {
                if (actorId <= 0 || phongTroId <= 0 || thang < 1 || thang > 12 || nam < 2000)
                {
                    _logger.LogInformation("Hóa đơn tự động bỏ qua do tham số không hợp lệ (phòng {PhongTroId}, kỳ {Thang}/{Nam}).", phongTroId, thang, nam);
                    return Done(AutoInvoiceOutcome.DungImLang);
                }

                _store.DiscardPendingChanges();
                ctx = await _store.GetRoomContextAsync(phongTroId, thang, nam, _options.SoKyLichSuToiDa, CancellationToken.None);
                if (ctx == null)
                {
                    _logger.LogInformation("Hóa đơn tự động bỏ qua: không tìm thấy phòng {PhongTroId}.", phongTroId);
                    return Done(AutoInvoiceOutcome.DungImLang);
                }

                var decision = AutoInvoiceReadingPolicy.EvaluateRoom(ctx, _options);
                if (!decision.DuDieuKien)
                {
                    return await DungKhongDuDieuKienAsync(ctx, decision, trigger, string.Empty);
                }

                // Người thực hiện (spec mục 8): khách tải ảnh thì dùng nhân viên dự phòng của chi nhánh.
                int nguoiThucHien;
                if (trigger == AutoInvoiceTrigger.KhachTaiAnh)
                {
                    var fallback = await _store.FindFallbackActorAsync(ctx.ChiNhanhId, CancellationToken.None);
                    if (fallback == null)
                    {
                        return await DungCoThongBaoAsync(ctx, AutoInvoiceReason.ChiNhanhKhongCoNhanVien,
                            "Chi nhánh chưa có nhân viên được phân công để thực hiện tự động");
                    }

                    nguoiThucHien = fallback.Value;
                }
                else
                {
                    nguoiThucHien = actorId;
                }

                foreach (var action in new[] { EmployeeActionCodes.MeterReview, EmployeeActionCodes.InvoiceDraft })
                {
                    if (!await _employeeAccessService.CanPerformAsync(nguoiThucHien, ctx.ChiNhanhId, action, CancellationToken.None))
                    {
                        return await DungCoThongBaoAsync(ctx, AutoInvoiceReason.NguoiThucHienThieuQuyen,
                            "Người thực hiện thiếu quyền Meter.Review hoặc Invoice.Draft tại chi nhánh");
                    }
                }

                // Bước 1: tự xác nhận ảnh đạt ngưỡng.
                foreach (var meter in new[] { decision.Dien!, decision.Nuoc! })
                {
                    if (meter.AnhTuXacNhanId == null)
                    {
                        continue;
                    }

                    var confirm = await _workflowService.ConfirmImageAsync(new ConfirmMeterImageRequest
                    {
                        AnhChiSoDongHoId = meter.AnhTuXacNhanId.Value,
                        GiaTriXacNhan = meter.ChiSoMoi!.Value,
                        GhiChu = AutoInvoiceReadingPolicy.GhiChuTuXacNhan(meter.DoTinCay ?? 0d)
                    }, nguoiThucHien, CancellationToken.None);
                    _store.DiscardPendingChanges();

                    if (!confirm.Success)
                    {
                        var recovered = await ReloadAsync(phongTroId, thang, nam);
                        if (recovered == null || recovered.CoHoaDonHoatDong)
                        {
                            _logger.LogInformation("Hóa đơn tự động dừng sau khi xác nhận ảnh thất bại: đã có hóa đơn (phòng {PhongTroId}).", phongTroId);
                            return Done(AutoInvoiceOutcome.DungImLang, new[] { AutoInvoiceReason.DaCoHoaDon });
                        }

                        var coChinhThuc = recovered.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet
                            || recovered.Images.Any(i => i.LoaiDongHo == meter.LoaiDongHo && i.DuocChonLamChiSoChinhThuc);
                        if (!coChinhThuc)
                        {
                            return await DungCoThongBaoAsync(ctx, AutoInvoiceReason.LoiKhiTao, confirm.Message);
                        }
                    }
                }

                // Bước 2: chốt kỳ bằng ảnh chính thức.
                var afterConfirm = await ReloadAsync(phongTroId, thang, nam);
                if (afterConfirm == null || afterConfirm.CoHoaDonHoatDong)
                {
                    return Done(AutoInvoiceOutcome.DungImLang, new[] { AutoInvoiceReason.DaCoHoaDon });
                }

                if (afterConfirm.TrangThaiGhiNhan != TrangThaiGhiNhan.DaDuyet)
                {
                    var approve = await _workflowService.ApprovePeriodsAsync(new ApproveMeterPeriodsRequest
                    {
                        Thang = thang,
                        Nam = nam,
                        DanhSachPhong = new[]
                        {
                            new ApproveMeterPeriodItem
                            {
                                PhongTroId = phongTroId,
                                DienMode = MeterReadingSubmissionMode.OfficialImage,
                                NuocMode = MeterReadingSubmissionMode.OfficialImage
                            }
                        }
                    }, nguoiThucHien, CancellationToken.None);
                    _store.DiscardPendingChanges();

                    if (!approve.Success)
                    {
                        var recovered = await ReloadAsync(phongTroId, thang, nam);
                        if (recovered == null || recovered.CoHoaDonHoatDong)
                        {
                            return Done(AutoInvoiceOutcome.DungImLang, new[] { AutoInvoiceReason.DaCoHoaDon });
                        }

                        if (recovered.TrangThaiGhiNhan != TrangThaiGhiNhan.DaDuyet)
                        {
                            return await DungCoThongBaoAsync(ctx, AutoInvoiceReason.LoiKhiTao, approve.Message);
                        }
                    }
                }

                // Đánh giá lại ngay trước khi tạo hóa đơn: dữ liệu có thể đã đổi từ lúc đánh giá đầu chuỗi.
                var latest = await ReloadAsync(phongTroId, thang, nam);
                if (latest == null)
                {
                    _logger.LogInformation("Hóa đơn tự động dừng: không còn tìm thấy phòng {PhongTroId} trước bước tạo hóa đơn.", phongTroId);
                    return Done(AutoInvoiceOutcome.DungImLang, new[] { AutoInvoiceReason.DaCoHoaDon });
                }

                var latestDecision = AutoInvoiceReadingPolicy.EvaluateRoom(latest, _options);
                if (!latestDecision.DuDieuKien)
                {
                    return await DungKhongDuDieuKienAsync(latest, latestDecision, trigger, " khi đánh giá lại");
                }

                // Bước 3: tạo hóa đơn nháp (cách tính tiền giữ nguyên của luồng tay), lịch sử mang nhãn [TU_DONG].
                var drafts = await _issuanceService.CreateDraftsAsync(new CreateInvoiceDraftsRequest
                {
                    ChiNhanhId = latest.ChiNhanhId,
                    Thang = thang,
                    Nam = nam,
                    PhongTroIds = new[] { phongTroId }
                }, nguoiThucHien,
                    lyDoKhoiTao: AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.TuDong, "Tạo hóa đơn nháp tự động"),
                    ct: CancellationToken.None);
                _store.DiscardPendingChanges();

                if (!drafts.Success)
                {
                    return await DungCoThongBaoAsync(latest, AutoInvoiceReason.LoiKhiTao, drafts.Message);
                }

                var hopDongId = latestDecision.HopDong!.HopDongId;
                var items = drafts.Data?.Items ?? Array.Empty<InvoiceDraftItemResult>();
                var created = items.FirstOrDefault(i => i.Status == InvoiceDraftItemStatus.Created && i.HopDongId == hopDongId);
                if (created == null || created.HoaDonId == null || created.HoaDonId <= 0)
                {
                    var forContract = items.FirstOrDefault(i => i.HopDongId == hopDongId);
                    if (forContract?.Status == InvoiceDraftItemStatus.SkippedHasActiveInvoice)
                    {
                        _logger.LogInformation("Hóa đơn tự động dừng: hợp đồng {HopDongId} đã có hóa đơn kỳ {Thang}/{Nam}.", hopDongId, thang, nam);
                        return Done(AutoInvoiceOutcome.DungImLang, new[] { AutoInvoiceReason.DaCoHoaDon });
                    }

                    var moTa = forContract?.Message;
                    return await DungCoThongBaoAsync(latest, AutoInvoiceReason.LoiKhiTao,
                        string.IsNullOrWhiteSpace(moTa) ? "Không tạo được hóa đơn nháp" : moTa);
                }

                // Dừng ở nháp: chỉ báo nhân viên phụ trách phòng kiểm tra rồi gửi duyệt, không gửi gì cho khách.
                var hoaDonId = created.HoaDonId.Value;
                await TryNotifyAsync("nhân viên", () => _thongBaoService.GuiChoNguoiPhuTrachPhongAsync(
                    phongTroId,
                    $"Phòng {latest.SoPhong} kỳ {thang:00}/{nam}: có hóa đơn tự động chờ gửi duyệt",
                    $"Hóa đơn {created.MaHoaDon} đã được tạo ở trạng thái nháp. Kiểm tra rồi bấm Gửi duyệt.",
                    "HoaDon",
                    $"/QuanLyNhaTro/QuanLyHoaDon?chiNhanhId={latest.ChiNhanhId}&thang={thang}&nam={nam}&hoaDonId={hoaDonId}"));

                _logger.LogInformation("Đã tạo hóa đơn nháp tự động {HoaDonId} (phòng {PhongTroId}, kỳ {Thang}/{Nam}, trigger {Trigger}).",
                    hoaDonId, phongTroId, thang, nam, trigger);
                return ServiceResult<AutoInvoiceRunResult>.Ok(new AutoInvoiceRunResult
                {
                    Outcome = AutoInvoiceOutcome.DaTaoNhap,
                    HoaDonId = hoaDonId,
                    NguoiThucHienId = nguoiThucHien
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi ngoài dự kiến khi tự động lập hóa đơn (phòng {PhongTroId}, kỳ {Thang}/{Nam}, trigger {Trigger}).",
                    phongTroId, thang, nam, trigger);
                try
                {
                    if (ctx != null)
                    {
                        _store.DiscardPendingChanges();
                        await BaoDungAsync(ctx, new[] { AutoInvoiceReason.LoiKhiTao }, new[] { "Lỗi hệ thống khi lập hóa đơn tự động" }, null);
                    }
                }
                catch (Exception notifyEx)
                {
                    _logger.LogWarning(notifyEx, "Không báo được lỗi hóa đơn tự động cho nhân viên (phòng {PhongTroId}).", phongTroId);
                }

                return Done(AutoInvoiceOutcome.DungCoThongBao, new[] { AutoInvoiceReason.LoiKhiTao });
            }
        }

        private static ServiceResult<AutoInvoiceRunResult> Done(AutoInvoiceOutcome outcome, IReadOnlyList<AutoInvoiceReason>? lyDos = null)
        {
            return ServiceResult<AutoInvoiceRunResult>.Ok(new AutoInvoiceRunResult
            {
                Outcome = outcome,
                LyDos = lyDos ?? Array.Empty<AutoInvoiceReason>()
            });
        }

        private async Task<AutoInvoiceRoomContext?> ReloadAsync(int phongTroId, int thang, int nam)
        {
            _store.DiscardPendingChanges();
            return await _store.GetRoomContextAsync(phongTroId, thang, nam, _options.SoKyLichSuToiDa, CancellationToken.None);
        }

        // Phòng không đủ điều kiện: lý do im lặng chỉ ghi log; lý do khác báo nhân viên (và khách nếu lý do cho phép).
        private async Task<ServiceResult<AutoInvoiceRunResult>> DungKhongDuDieuKienAsync(
            AutoInvoiceRoomContext ctx, AutoInvoiceRoomDecision decision, AutoInvoiceTrigger trigger, string giaiDoan)
        {
            if (decision.LyDos.All(AutoInvoiceReadingPolicy.IsSilent))
            {
                _logger.LogInformation("Hóa đơn tự động dừng im lặng{GiaiDoan} (phòng {PhongTroId}, kỳ {Thang}/{Nam}, trigger {Trigger}): {LyDos}",
                    giaiDoan, ctx.PhongTroId, ctx.Thang, ctx.Nam, trigger, string.Join(", ", decision.LyDos));
                return Done(AutoInvoiceOutcome.DungImLang, decision.LyDos);
            }

            _logger.LogWarning("Hóa đơn tự động dừng{GiaiDoan} (phòng {PhongTroId}, kỳ {Thang}/{Nam}, trigger {Trigger}): {MoTa}",
                giaiDoan, ctx.PhongTroId, ctx.Thang, ctx.Nam, trigger, string.Join("; ", decision.MoTaLyDos));
            await BaoDungAsync(ctx, decision.LyDos, decision.MoTaLyDos, decision);
            return Done(AutoInvoiceOutcome.DungCoThongBao, decision.LyDos);
        }

        private async Task<ServiceResult<AutoInvoiceRunResult>> DungCoThongBaoAsync(
            AutoInvoiceRoomContext ctx, AutoInvoiceReason reason, string moTa)
        {
            _logger.LogWarning("Hóa đơn tự động dừng (phòng {PhongTroId}, kỳ {Thang}/{Nam}): {Reason} - {MoTa}",
                ctx.PhongTroId, ctx.Thang, ctx.Nam, reason, moTa);
            await BaoDungAsync(ctx, new[] { reason }, new[] { moTa }, null);
            return Done(AutoInvoiceOutcome.DungCoThongBao, new[] { reason });
        }

        private async Task BaoDungAsync(
            AutoInvoiceRoomContext ctx,
            IReadOnlyList<AutoInvoiceReason> lyDos,
            IReadOnlyList<string> moTas,
            AutoInvoiceRoomDecision? decision)
        {
            _store.DiscardPendingChanges();

            await TryNotifyAsync("nhân viên", () => _thongBaoService.GuiChoNguoiPhuTrachPhongAsync(
                ctx.PhongTroId,
                $"Phòng {ctx.SoPhong} kỳ {ctx.Thang:00}/{ctx.Nam}: chưa tạo được hóa đơn tự động",
                "Lý do: " + string.Join("; ", moTas) + (lyDos.Contains(AutoInvoiceReason.ThieuDonGia) ? " " + HuongDanThieuDonGia : string.Empty),
                "HoaDon",
                $"/QuanLyNhaTro/ChotDienNuoc?chiNhanhId={ctx.ChiNhanhId}&thang={ctx.Thang}&nam={ctx.Nam}&phongTroId={ctx.PhongTroId}"));

            if (decision == null || ctx.HopDongHieuLuc.Count != 1)
            {
                return;
            }

            var nguoiThueId = ctx.HopDongHieuLuc[0].NguoiThueId;
            foreach (var meter in new[] { decision.Dien, decision.Nuoc })
            {
                if (meter?.LyDo == null || !AutoInvoiceReadingPolicy.CanBaoKhach(meter.LyDo.Value))
                {
                    continue;
                }

                // Số bất thường chỉ nhờ khách chụp lại khi số đến từ OCR; số nhân viên đã xác nhận hoặc kỳ đã chốt thì chỉ báo nhân viên.
                var coGiaTri = meter.LyDo is AutoInvoiceReason.ChiSoGiam or AutoInvoiceReason.NghiSaiChuSo or AutoInvoiceReason.TieuThuThapBatThuong;
                if (coGiaTri && meter.AnhTuXacNhanId == null)
                {
                    continue;
                }

                var tenDongHo = meter.LoaiDongHo == LoaiDongHo.Dien ? "điện" : "nước";
                string tieuDe;
                string noiDung;
                if (meter.LyDo == AutoInvoiceReason.ChiSoGiam)
                {
                    tieuDe = "Chỉ số đồng hồ thấp hơn kỳ trước";
                    noiDung = $"Chỉ số {tenDongHo} kỳ {ctx.Thang:00}/{ctx.Nam} của phòng {ctx.SoPhong} đọc được là {FormatSo(meter.ChiSoMoi)} nhỏ hơn chỉ số cũ {FormatSo(meter.ChiSoCu)}. Vui lòng chụp lại rõ mặt đồng hồ";
                    noiDung += string.IsNullOrWhiteSpace(ctx.SoDienThoaiChiNhanh)
                        ? "."
                        : $" hoặc liên hệ {ctx.SoDienThoaiChiNhanh}.";
                }
                else if (meter.LyDo is AutoInvoiceReason.NghiSaiChuSo or AutoInvoiceReason.TieuThuThapBatThuong)
                {
                    tieuDe = "Chỉ số đồng hồ có thể chưa đúng";
                    noiDung = $"Chỉ số {tenDongHo} kỳ {ctx.Thang:00}/{ctx.Nam} của phòng {ctx.SoPhong} đọc được là {FormatSo(meter.ChiSoMoi)}, chênh lệch nhiều so với các tháng trước. Vui lòng chụp lại rõ mặt đồng hồ";
                    noiDung += string.IsNullOrWhiteSpace(ctx.SoDienThoaiChiNhanh)
                        ? "."
                        : $" hoặc liên hệ {ctx.SoDienThoaiChiNhanh}.";
                }
                else
                {
                    tieuDe = "Cần chụp lại ảnh đồng hồ";
                    noiDung = $"Ảnh chỉ số {tenDongHo} kỳ {ctx.Thang:00}/{ctx.Nam} của phòng {ctx.SoPhong} chưa đọc được rõ. Vui lòng chụp lại và gửi lại ảnh.";
                }

                await TryNotifyAsync("khách thuê", () => _thongBaoService.GuiChoKhachThueAsync(
                    nguoiThueId, tieuDe, noiDung, "DienNuoc", "/KhachThue/ChiSoDienNuoc"));
            }
        }

        private async Task TryNotifyAsync(string doiTuong, Func<Task<ServiceResult>> send)
        {
            try
            {
                var result = await send();
                _store.DiscardPendingChanges();
                if (!result.Success)
                {
                    _logger.LogInformation("Không gửi được thông báo hóa đơn tự động cho {DoiTuong}: {Message}", doiTuong, result.Message);
                }
            }
            catch (Exception ex)
            {
                _store.DiscardPendingChanges();
                _logger.LogWarning(ex, "Lỗi khi gửi thông báo hóa đơn tự động cho {DoiTuong}.", doiTuong);
            }
        }

        private static string FormatSo(decimal? value) => value.HasValue ? ChiSo.DinhDang(value.Value) : string.Empty;
    }
}
