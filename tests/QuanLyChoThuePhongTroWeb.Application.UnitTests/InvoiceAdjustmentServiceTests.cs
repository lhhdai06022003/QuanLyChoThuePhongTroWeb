using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoiceAdjustmentServiceTests
    {
        private const int Actor = 7;
        private const int Chi = 1;
        private const int Phong = 101;
        private const int HopDong = 10;
        private const int HoaDonId = 500;
        private const int PeriodId = 40;
        private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime OldHan = Now.AddHours(10);

        private sealed class FakeIssuanceStore : IInvoiceIssuanceStore
        {
            public Dictionary<int, HoaDon> Invoices { get; } = new();
            public List<string> Log { get; } = new();

            public Task<IReadOnlyList<int>> LockContractsAsync(IReadOnlyList<int> hopDongIds, CancellationToken ct = default)
            {
                Log.Add("LockContracts");
                return Task.FromResult<IReadOnlyList<int>>(hopDongIds);
            }

            public Task<HoaDon?> GetInvoiceForUpdateAsync(int hoaDonId, CancellationToken ct = default)
            {
                Log.Add("GetInvoiceForUpdate");
                return Task.FromResult(Invoices.TryGetValue(hoaDonId, out var h) && !h.IsDeleted ? h : null);
            }

            public Task<TrangThaiGhiNhan?> LockMeterPeriodAsync(int meterPeriodId, CancellationToken ct = default)
            {
                Log.Add("LockMeterPeriod");
                return Task.FromResult<TrangThaiGhiNhan?>(TrangThaiGhiNhan.DaDuyet);
            }

            public Task<int> CountCancelledInvoicesAsync(int hopDongId, int thang, int nam, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<bool> HasOtherActiveInvoiceAsync(int hopDongId, int thang, int nam, int excludeHoaDonId, CancellationToken ct = default) => throw new NotSupportedException();
            public Task AddInvoiceAsync(HoaDon hoaDon, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableIncidentsInPeriodAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime nextMonthStartUtc, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<HoaDon?> GetActiveInvoiceForTenantRoomPeriodForUpdateAsync(int phongTroId, int nguoiThueId, int thang, int nam, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<(int? MeterPeriodId, int ChiNhanhId)?> GetInvoiceLockTargetsAsync(int hoaDonId, CancellationToken ct = default) => throw new NotSupportedException();
            public Task LockMeterPeriodsForRoomsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<int>> GetContractIdsForTenantRoomAsync(int phongTroId, int nguoiThueId, CancellationToken ct = default) => throw new NotSupportedException();
        }

        private sealed class Harness
        {
            public FakeAutoInvoiceStore Auto { get; } = new();
            public FakeIssuanceStore Issuance { get; } = new();
            public FakeInvoiceIssuanceService IssuanceService { get; } = new();
            public FakeMeterReadingWorkflowService Meter { get; } = new();
            public FakeMeterImageStore MeterStore { get; } = new();
            public FakeInvoicePaymentStore Payments { get; } = new();
            public FakeInvoicePublicationStore Publication { get; } = new();
            public FakeHoaDonStore HoaDonStore { get; } = new();
            public RecordingUnitOfWork Uow { get; } = new();
            public FakeEmployeeAccessService Access { get; } = new();
            public FakeThongBaoService ThongBao { get; } = new();
            public RecordingEmailService Email { get; } = new();
            public CapturingLogger<InvoiceAdjustmentService> Logger { get; } = new();
            public InvoiceAdjustmentService Service { get; }
            public HoaDon Invoice { get; }

            public Harness(TrangThaiPhatHanhHoaDon status = TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon pay = TrangThaiHoaDon.ChuaThanhToan,
                DateTime? han = null, bool grantAll = true, IProvisionalInvoiceEmailDispatcher? dispatcher = null)
            {
                var hasHan = han ?? OldHan;
                Invoice = new HoaDon
                {
                    HoaDonId = HoaDonId, HopDongId = HopDong, MaHoaDon = "HD-500", Thang = 10, Nam = 2026, TongTien = 1_000_000m,
                    TrangThaiPhatHanh = status, TrangThaiHoaDon = pay, HanPhanHoi = hasHan, DichVuDienNuocCuaPhongId = PeriodId,
                    HopDong = new HopDong { HopDongId = HopDong, PhongTroId = Phong },
                    DichVuDienNuocCuaPhong = new DichVuDienNuocCuaPhong
                    {
                        DichVuDienNuocCuaPhongId = PeriodId, PhongTroId = Phong, ChiSoDienCu = 100, ChiSoDienMoi = 120, ChiSoNuocCu = 50, ChiSoNuocMoi = 60,
                        TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
                    },
                    ChiTietHoaDonDichVus = new List<ChiTietHoaDon> { new() { ChiTietHoaDonId = 1, HoaDonId = HoaDonId, TenDichVu = "Tiền phòng", DonGia = 1_000_000m, SoLuong = 1, TongTien = 1_000_000m } }
                };
                Issuance.Invoices[HoaDonId] = Invoice;
                Auto.AdjustmentSnapshots[HoaDonId] = new AutoInvoiceAdjustmentSnapshot
                {
                    HoaDonId = HoaDonId, MaHoaDon = "HD-500", HopDongId = HopDong, ChiNhanhId = Chi, PhongTroId = Phong, Thang = 10, Nam = 2026,
                    TrangThaiPhatHanh = status, TrangThaiHoaDon = pay, HanPhanHoi = hasHan, DichVuDienNuocCuaPhongId = PeriodId,
                    ChiSoDienCu = 100, ChiSoDienMoi = 120, ChiSoNuocCu = 50, ChiSoNuocMoi = 60,
                    TongTien = 1_000_000m, DonGiaDien = 3_500m, DonGiaNuoc = 15_000m,
                    TienThuePhong = 3_000_000m, HopDongBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    ChiTiets = new[] { new AutoInvoiceInvoiceLine(1, "Tiền phòng", 1_000_000m, 1m, 1_000_000m, null, null, null) }
                };
                HoaDonStore.Branches[Chi] = new ChiNhanh { ChiNhanhId = Chi, TenChiNhanh = "CN1", MaChiNhanh = "CN1", SoGioPhanHoiHoaDon = 24 };
                HoaDonStore.Details[HoaDonId] = new HoaDonChiTietRes
                {
                    HoaDonId = HoaDonId, MaHoaDon = "HD-500", TenPhong = "101", TenNguoiThue = "A", TenChiNhanh = "CN1",
                    SoDienThoaiChiNhanh = "0900000000", Thang = 10, Nam = 2026, TongTien = 1_000_000m,
                    ChiTietHoaDons = new List<ChiTietHoaDonRes> { new() { TenDichVu = "Tiền phòng", TongTien = 1_000_000m } }
                };
                Publication.Recipients[HopDong] = new InvoiceRecipientInfo { NguoiThueId = 9, HoVaTen = "A", Email = "khach@example.com", NguoiDungId = 3 };
                Meter.OnAdjustReadings = (req, actor) =>
                {
                    // Mô phỏng lõi chỉ số thật: cập nhật kỳ và tính lại dòng của hóa đơn nháp.
                    Invoice.DichVuDienNuocCuaPhong!.ChiSoDienMoi = req.ChiSoDienMoi;
                    Invoice.DichVuDienNuocCuaPhong.ChiSoNuocMoi = req.ChiSoNuocMoi;
                    Invoice.TongTien = 1_070_000m;
                    Invoice.ChiTietHoaDonDichVus.First().TongTien = 1_070_000m;
                    return ServiceResult<MeterAdjustmentResult>.Ok(new MeterAdjustmentResult
                    {
                        ChiSoDienCu = 120, ChiSoDienMoi = req.ChiSoDienMoi, ChiSoNuocCu = 60, ChiSoNuocMoi = req.ChiSoNuocMoi
                    });
                };
                if (grantAll)
                {
                    foreach (var action in new[] { EmployeeActionCodes.MeterReview, EmployeeActionCodes.InvoiceDraft, EmployeeActionCodes.InvoiceSubmit, EmployeeActionCodes.InvoiceCancel, EmployeeActionCodes.InvoiceRead })
                    {
                        Access.Permissions.Add((Actor, Chi, action));
                    }
                }

                Service = new InvoiceAdjustmentService(
                    Auto, Issuance, IssuanceService, Meter, MeterStore, Payments, Publication, HoaDonStore, Uow, Access, ThongBao, Email,
                    new HoaDonCalculatorService(), Logger,
                    new FixedTimeProvider(new DateTimeOffset(Now)),
                    dispatcher);
            }

            public Task<ServiceResult<InvoiceAdjustmentResult>> Adjust(decimal dien = 130m, decimal nuoc = 60m, int actor = Actor)
                => Service.AdjustAsync(actor, new AdjustInvoiceRequest { HoaDonId = HoaDonId, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc });

            // Điều chỉnh có danh sách dòng; chỉ số null = giữ nguyên.
            public Task<ServiceResult<InvoiceAdjustmentResult>> AdjustLines(IReadOnlyList<AdjustInvoiceLine> lines, decimal? dien = null, decimal? nuoc = null, int actor = Actor)
                => Service.AdjustAsync(actor, new AdjustInvoiceRequest { HoaDonId = HoaDonId, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc, ChiTiets = lines });

            // Đặt các dòng hiện có của hóa đơn (và ảnh chụp để màn điều chỉnh đọc).
            public void SetLines(params ChiTietHoaDon[] lines)
            {
                Invoice.ChiTietHoaDonDichVus = lines.ToList();
                Invoice.TongTien = lines.Sum(l => l.TongTien);
                var snap = Auto.AdjustmentSnapshots[HoaDonId];
                Auto.AdjustmentSnapshots[HoaDonId] = new AutoInvoiceAdjustmentSnapshot
                {
                    HoaDonId = snap.HoaDonId, MaHoaDon = snap.MaHoaDon, HopDongId = snap.HopDongId, ChiNhanhId = snap.ChiNhanhId, PhongTroId = snap.PhongTroId,
                    Thang = snap.Thang, Nam = snap.Nam, TrangThaiPhatHanh = snap.TrangThaiPhatHanh, TrangThaiHoaDon = snap.TrangThaiHoaDon,
                    HanPhanHoi = snap.HanPhanHoi, DichVuDienNuocCuaPhongId = snap.DichVuDienNuocCuaPhongId,
                    ChiSoDienCu = snap.ChiSoDienCu, ChiSoDienMoi = snap.ChiSoDienMoi, ChiSoNuocCu = snap.ChiSoNuocCu, ChiSoNuocMoi = snap.ChiSoNuocMoi,
                    TongTien = Invoice.TongTien, DonGiaDien = snap.DonGiaDien, DonGiaNuoc = snap.DonGiaNuoc, TienThuePhong = snap.TienThuePhong,
                    HopDongBatDau = snap.HopDongBatDau, HopDongKetThuc = snap.HopDongKetThuc,
                    ChiTiets = lines.Select(l => new AutoInvoiceInvoiceLine(l.ChiTietHoaDonId, l.TenDichVu, l.DonGia, l.SoLuong, l.TongTien, l.DichVuId, null, null)).ToList()
                };
            }
        }

        // ---------- Quyền ----------

        [Theory]
        [InlineData(EmployeeActionCodes.MeterReview)]
        [InlineData(EmployeeActionCodes.InvoiceDraft)]
        [InlineData(EmployeeActionCodes.InvoiceSubmit)]
        public async Task Adjust_MissingMeterReviewInvoiceDraftOrInvoiceSubmit_IsForbidden_AndNothingStarts(string missing)
        {
            var h = new Harness();
            h.Access.Permissions.Remove((Actor, Chi, missing));

            var result = await h.Adjust();

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Equal("Bạn không có quyền điều chỉnh hóa đơn tại chi nhánh này.", result.Message);
            Assert.Empty(h.Uow.Transactions);
            Assert.Empty(h.Meter.AdjustCalls);
        }

        [Fact]
        public async Task Adjust_SentInvoice_WithoutInvoiceCancel_IsForbidden_AndRollsBack()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.Access.Permissions.Remove((Actor, Chi, EmployeeActionCodes.InvoiceCancel));

            var result = await h.Adjust();

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            var tx = Assert.Single(h.Uow.Transactions);
            Assert.True(tx.RolledBack);
            Assert.False(tx.Committed);
            Assert.Empty(h.Meter.AdjustCalls);
        }

        [Fact]
        public async Task Adjust_StaffOfAnotherBranch_IsForbidden()
        {
            var h = new Harness(grantAll: false);
            foreach (var action in new[] { EmployeeActionCodes.MeterReview, EmployeeActionCodes.InvoiceSubmit })
            {
                h.Access.Permissions.Add((Actor, 99, action));
            }

            var result = await h.Adjust();

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Empty(h.Uow.Transactions);
        }

        [Fact]
        public async Task Adjust_UnknownInvoice_IsNotFound_AndInvalidArgumentsAreRejected()
        {
            var h = new Harness();

            var missing = await h.Service.AdjustAsync(Actor, new AdjustInvoiceRequest { HoaDonId = 999, ChiSoDienMoi = 130, ChiSoNuocMoi = 60 });
            var negative = await h.Adjust(dien: -1m);
            var decimals = await h.Adjust(dien: 130.1234m);
            var noActor = await h.Adjust(actor: 0);

            Assert.Equal(ServiceErrorKind.NotFound, missing.ErrorKind);
            Assert.False(negative.Success);
            Assert.False(decimals.Success);
            Assert.False(noActor.Success);
            Assert.Empty(h.Uow.Transactions);
        }

        // ---------- Lý do khóa ----------

        [Fact]
        public async Task Adjust_ManualInvoiceWithoutDeadline_IsRejected()
        {
            var h = new Harness();
            h.Invoice.HanPhanHoi = null;
            h.Auto.AdjustmentSnapshots[HoaDonId] = Snap(h, han: null);

            var result = await h.Adjust();

            Assert.False(result.Success);
            Assert.Equal("Chỉ hóa đơn tự động mới dùng được Điều chỉnh và gửi lại.", result.Message);
            Assert.True(Assert.Single(h.Uow.Transactions).RolledBack);
            Assert.Empty(h.Meter.AdjustCalls);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaHuy)]
        public async Task Adjust_StatusOtherThanChoDuyetOrDaGui_IsRejected(TrangThaiPhatHanhHoaDon status)
        {
            var h = new Harness(status);

            var result = await h.Adjust();

            Assert.Equal("Hóa đơn không ở trạng thái điều chỉnh được.", result.Message);
        }

        [Fact]
        public async Task Adjust_LaterPeriodClosed_IsRejected_AndRollsBack()
        {
            var h = new Harness();
            h.MeterStore.SubsequentPeriods.Add((Phong, 10, 2026));

            var result = await h.Adjust();

            Assert.Equal("Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể điều chỉnh.", result.Message);
            Assert.True(Assert.Single(h.Uow.Transactions).RolledBack);
            Assert.Empty(h.Meter.AdjustCalls);
            Assert.Empty(h.ThongBao.Sent);
            Assert.Empty(h.Email.ProvisionalCalls);
        }

        [Fact]
        public async Task Adjust_SentInvoiceWithCollectedMoney_IsRejected()
        {
            var partial = new Harness(TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan);
            var ledger = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            ledger.Payments.Ledger.Add(new LichSuThanhToan { HoaDonId = HoaDonId, SoTienThanhToan = 50_000m, MaGiaoDich = "GD1" });

            var byStatus = await partial.Adjust();
            var byLedger = await ledger.Adjust();

            Assert.Contains("Hóa đơn đã có khoản thu", byStatus.Message);
            Assert.Contains("Hóa đơn đã có khoản thu", byLedger.Message);
        }

        [Fact]
        public async Task Adjust_SentInvoiceWithProofAwaitingReview_IsRejected()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            var request = YeuCauThanhToanHoaDon.Tao(HoaDonId, "YC1", 1_000_000m, Now.AddDays(1), Actor, Now);
            request.TrangThai = TrangThaiYeuCauThanhToan.DangDoiChieu;
            h.Payments.Requests.Add(request);

            var result = await h.Adjust();

            Assert.Contains("Khách đã nộp minh chứng, cần đối chiếu trước", result.Message);
        }

        [Fact]
        public async Task Adjust_NoChange_IsRejectedWithoutCallingMeterCore()
        {
            var h = new Harness();

            var result = await h.Adjust(dien: 120m, nuoc: 60m);

            Assert.Equal("Không có thay đổi", result.Message);
            Assert.Empty(h.Meter.AdjustCalls);
            Assert.True(Assert.Single(h.Uow.Transactions).RolledBack);
        }

        [Fact]
        public async Task Adjust_ReadingBelowOldValue_IsRejected()
        {
            var h = new Harness();

            Assert.Equal("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.", (await h.Adjust(dien: 99m)).Message);
            Assert.Equal("Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.", (await h.Adjust(nuoc: 49m)).Message);
        }

        // ---------- Nhánh ChoDuyet ----------

        [Fact]
        public async Task Adjust_Provisional_ReopensRecalculatesAndResendsWithNewDeadline()
        {
            var h = new Harness();

            var result = await h.Adjust();

            Assert.True(result.Success, result.Message);
            Assert.False(result.Data!.DaTaoHoaDonThayThe);
            Assert.Equal(HoaDonId, result.Data.HoaDonIdMoi);
            Assert.Equal(1_070_000m, result.Data.TongTienMoi);
            Assert.Equal(Now.AddHours(24), result.Data.HanPhanHoiUtc);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, h.Invoice.TrangThaiPhatHanh);
            Assert.Equal(Now.AddHours(24), h.Invoice.HanPhanHoi);
            var tx = Assert.Single(h.Uow.Transactions);
            Assert.True(tx.Committed);
            Assert.False(tx.RolledBack);
            Assert.Equal(new[] { "LockMeterPeriod", "LockContracts", "GetInvoiceForUpdate" }, h.Issuance.Log.Take(3).ToArray());

            var call = Assert.Single(h.Meter.AdjustCalls);
            Assert.Equal(PeriodId, call.Request.DichVuDienNuocCuaPhongId);
            Assert.Equal(130m, call.Request.ChiSoDienMoi);
            Assert.Equal("Điều chỉnh hóa đơn theo yêu cầu khách thuê", call.Request.LyDo);

            var history = h.Invoice.LichSuTrangThaiHoaDons.ToList();
            Assert.Equal(2, history.Count);
            Assert.All(history, e => Assert.StartsWith("[DIEU_CHINH]", e.LyDo));
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, history[0].TrangThaiPhatHanhMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, history[1].TrangThaiPhatHanhMoi);

            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Khach", notice.Kind);
            Assert.Equal(9, notice.TargetId);
            Assert.Contains("đã được điều chỉnh", notice.TieuDe);
            Assert.Equal($"/KhachThue/HoaDon?hoaDonId={HoaDonId}", notice.Link);
            var mail = Assert.Single(h.Email.ProvisionalCalls);
            Assert.True(mail.Data.LaDieuChinh);
            Assert.Equal("khach@example.com", mail.ToEmail);
            Assert.Equal(130m, mail.Data.ChiSoDienMoi);
            Assert.Equal(Now.AddHours(24), mail.Data.HanTuChotUtc);
        }

        [Fact]
        public async Task Adjust_Provisional_UsesCurrentBranchResponseHours()
        {
            var h = new Harness();
            h.HoaDonStore.Branches[Chi].SoGioPhanHoiHoaDon = 48;

            var result = await h.Adjust();

            Assert.Equal(Now.AddHours(48), result.Data!.HanPhanHoiUtc);
        }

        [Fact]
        public async Task Adjust_MeterCoreFails_RollsBackWithoutNotifications()
        {
            var h = new Harness();
            h.Meter.OnAdjustReadings = (_, _) => ServiceResult<MeterAdjustmentResult>.Fail("Kỳ 11/2026 đang có chỉ số điện mới nhỏ hơn.");

            var result = await h.Adjust();

            Assert.False(result.Success);
            Assert.Equal("Kỳ 11/2026 đang có chỉ số điện mới nhỏ hơn.", result.Message);
            var tx = Assert.Single(h.Uow.Transactions);
            Assert.True(tx.RolledBack);
            Assert.False(tx.Committed);
            Assert.True(h.Auto.DiscardCalls >= 1);
            Assert.Empty(h.ThongBao.Sent);
            Assert.Empty(h.Email.ProvisionalCalls);
        }

        [Fact]
        public async Task Adjust_SaveThrows_RollsBackAndLogsOneError_WithoutNotifications()
        {
            var h = new Harness();
            h.Uow.FailOnSave = n => n == 1 ? new InvalidOperationException("deadlock") : null;

            var result = await h.Adjust();

            Assert.False(result.Success);
            Assert.Equal("Không thể điều chỉnh hóa đơn. Vui lòng thử lại.", result.Message);
            Assert.True(Assert.Single(h.Uow.Transactions).RolledBack);
            Assert.Single(h.Logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(h.ThongBao.Sent);
        }

        [Fact]
        public async Task Adjust_NotificationAndEmailFailures_StillSucceed_WithWarningsOnly()
        {
            var h = new Harness();
            h.ThongBao.ThrowOnSend = true;
            h.Email.ThrowOnProvisional = true;

            var result = await h.Adjust();

            Assert.True(result.Success);
            Assert.True(Assert.Single(h.Uow.Transactions).Committed);
            Assert.Equal(2, h.Logger.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(h.Logger.Entries, e => e.Level >= LogLevel.Error);
        }

        // ---------- Email gửi nền ----------

        [Fact]
        public async Task Adjust_WithDispatcher_QueuesEmailInBackground_InsteadOfSendingInline()
        {
            var dispatcher = new RecordingProvisionalEmailDispatcher();
            var h = new Harness(dispatcher: dispatcher);

            var result = await h.Adjust();

            Assert.True(result.Success, result.Message);
            // Request không tự gọi SMTP; email chờ ở bộ gửi nền.
            Assert.Empty(h.Email.ProvisionalCalls);
            var batch = Assert.Single(dispatcher.Batches);
            var pending = Assert.Single(batch);
            Assert.Equal(HoaDonId, pending.HoaDonId);
            Assert.Equal("khach@example.com", pending.ToEmail);
            Assert.True(pending.Data.LaDieuChinh);
            Assert.Equal("HD-500", pending.Data.MaHoaDon);
            Assert.Equal((100m, 130m, 50m, 60m), (pending.Data.ChiSoDienCu, pending.Data.ChiSoDienMoi, pending.Data.ChiSoNuocCu, pending.Data.ChiSoNuocMoi));
            Assert.Equal(Now.AddHours(24), pending.Data.HanTuChotUtc);
            // Thông báo trong cổng vẫn gửi ngay trong request.
            Assert.Equal($"/KhachThue/HoaDon?hoaDonId={HoaDonId}", Assert.Single(h.ThongBao.Sent).Link);
        }

        [Fact]
        public async Task Adjust_WithDispatcher_QueuesEmailOnlyAfterTheAdjustmentIsCommitted()
        {
            var dispatcher = new RecordingProvisionalEmailDispatcher();
            var h = new Harness(dispatcher: dispatcher);
            h.Uow.FailOnSave = n => n == 1 ? new InvalidOperationException("deadlock") : null;

            var result = await h.Adjust();

            Assert.False(result.Success);
            Assert.Empty(dispatcher.Batches);
            Assert.Empty(h.Email.ProvisionalCalls);
        }

        [Fact]
        public async Task Adjust_WithDispatcher_RejectedAdjustment_QueuesNothing()
        {
            var dispatcher = new RecordingProvisionalEmailDispatcher();
            var h = new Harness(dispatcher: dispatcher);

            var noChange = await h.Adjust(dien: 120m, nuoc: 60m);
            var forbidden = await h.Adjust(actor: 99);

            Assert.False(noChange.Success);
            Assert.False(forbidden.Success);
            Assert.Empty(dispatcher.Batches);
        }

        [Fact]
        public async Task Adjust_WithDispatcher_RecipientWithoutEmail_QueuesNothing_ButStillSucceeds()
        {
            var dispatcher = new RecordingProvisionalEmailDispatcher();
            var h = new Harness(dispatcher: dispatcher);
            h.Publication.Recipients[HopDong] = new InvoiceRecipientInfo { NguoiThueId = 9, HoVaTen = "A", Email = null, NguoiDungId = 3 };

            var result = await h.Adjust();

            Assert.True(result.Success, result.Message);
            Assert.Empty(dispatcher.Batches);
            Assert.Single(h.ThongBao.Sent);
        }

        [Fact]
        public async Task Adjust_WithDispatcher_WhenQueueingThrows_StillSucceeds_WithWarningOnly()
        {
            var dispatcher = new RecordingProvisionalEmailDispatcher { ThrowOnEnqueue = true };
            var h = new Harness(dispatcher: dispatcher);

            var result = await h.Adjust();

            Assert.True(result.Success);
            Assert.True(Assert.Single(h.Uow.Transactions).Committed);
            Assert.Single(h.Logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(h.Logger.Entries, e => e.Level >= LogLevel.Error);
        }

        // ---------- Nhánh DaGui ----------

        [Fact]
        public async Task Adjust_Sent_CancelsOldAndWaitingRequest_CreatesReplacementAndResends()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            var waiting = YeuCauThanhToanHoaDon.Tao(HoaDonId, "YC1", 1_000_000m, Now.AddDays(1), Actor, Now.AddHours(-1));
            h.Payments.Requests.Add(waiting);
            var replacement = new HoaDon
            {
                HoaDonId = 501, HopDongId = HopDong, MaHoaDon = "HD-500-R1", Thang = 10, Nam = 2026, TongTien = 1_070_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon> { new() { TenDichVu = "Tiền phòng", TongTien = 1_070_000m } }
            };
            replacement.KhoiTaoNhap(Actor);
            h.Issuance.Invoices[501] = replacement;
            h.HoaDonStore.Details[501] = h.HoaDonStore.Details[HoaDonId];
            h.IssuanceService.OnCreateDraftsInCurrentTransaction = (req, actor) => ServiceResult<InvoiceDraftBatchResult>.Ok(new InvoiceDraftBatchResult
            {
                CreatedCount = 1,
                Items = new[]
                {
                    new InvoiceDraftItemResult
                    {
                        PhongTroId = Phong, SoPhong = "101", HopDongId = HopDong, MaHopDong = "HD-01",
                        Status = InvoiceDraftItemStatus.Created, HoaDonId = 501, MaHoaDon = "HD-500-R1"
                    }
                }
            });

            var result = await h.Adjust();

            Assert.True(result.Success, result.Message);
            Assert.True(result.Data!.DaTaoHoaDonThayThe);
            Assert.Equal(501, result.Data.HoaDonIdMoi);
            Assert.Equal("HD-500-R1", result.Data.MaHoaDonMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, h.Invoice.TrangThaiPhatHanh);
            Assert.True(h.Invoice.IsDeleted);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, waiting.TrangThai);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, replacement.TrangThaiPhatHanh);
            Assert.Equal(Now.AddHours(24), replacement.HanPhanHoi);
            var call = Assert.Single(h.IssuanceService.CreateInCurrentTransactionCalls);
            Assert.StartsWith("[DIEU_CHINH]", call.LyDoKhoiTao);
            Assert.Equal(new[] { Phong }, call.Request.PhongTroIds);
            Assert.True(Assert.Single(h.Uow.Transactions).Committed);
            Assert.True(Assert.Single(h.Email.ProvisionalCalls).Data.LaDieuChinh);
            Assert.Equal("/KhachThue/HoaDon?hoaDonId=501", Assert.Single(h.ThongBao.Sent).Link);
        }

        [Fact]
        public async Task Adjust_Sent_WhenReplacementBelongsToAnotherContract_RollsBack()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.IssuanceService.OnCreateDraftsInCurrentTransaction = (_, _) => ServiceResult<InvoiceDraftBatchResult>.Ok(new InvoiceDraftBatchResult
            {
                CreatedCount = 1,
                Items = new[]
                {
                    new InvoiceDraftItemResult { PhongTroId = Phong, HopDongId = 77, Status = InvoiceDraftItemStatus.Created, HoaDonId = 501, MaHoaDon = "X" },
                    new InvoiceDraftItemResult { PhongTroId = Phong, HopDongId = HopDong, Status = InvoiceDraftItemStatus.Invalid, Message = "Dữ liệu tính tiền phòng không hợp lệ." }
                }
            });

            var result = await h.Adjust();

            Assert.False(result.Success);
            Assert.Equal("Không tạo được hóa đơn thay thế: Dữ liệu tính tiền phòng không hợp lệ.", result.Message);
            var tx = Assert.Single(h.Uow.Transactions);
            Assert.True(tx.RolledBack);
            Assert.False(tx.Committed);
            Assert.Empty(h.ThongBao.Sent);
        }

        [Fact]
        public async Task Adjust_Sent_WhenDraftCoreFails_RollsBack()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.IssuanceService.OnCreateDraftsInCurrentTransaction = (_, _) => ServiceResult<InvoiceDraftBatchResult>.Fail("Bạn không có quyền tạo hóa đơn nháp tại chi nhánh này.");

            var result = await h.Adjust();

            Assert.False(result.Success);
            Assert.True(Assert.Single(h.Uow.Transactions).RolledBack);
        }

        // ---------- GetAdjustmentInfoAsync ----------

        [Fact]
        public async Task Info_WithoutInvoiceRead_IsForbidden_BeforeAnyOtherData()
        {
            var h = new Harness();
            h.Access.Permissions.Remove((Actor, Chi, EmployeeActionCodes.InvoiceRead));

            var result = await h.Service.GetAdjustmentInfoAsync(Actor, HoaDonId);

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Null(result.Data);
        }

        [Fact]
        public async Task Info_ReportsReadingsAndOpenState_WhenAdjustable()
        {
            var h = new Harness();

            var result = await h.Service.GetAdjustmentInfoAsync(Actor, HoaDonId);

            Assert.True(result.Success);
            Assert.True(result.Data!.CoTheDieuChinh);
            Assert.Null(result.Data.LyDoKhoa);
            Assert.Equal((100m, 120m, 50m, 60m), (result.Data.ChiSoDienCu, result.Data.ChiSoDienMoi, result.Data.ChiSoNuocCu, result.Data.ChiSoNuocMoi));
            Assert.Equal(OldHan, result.Data.HanPhanHoiUtc);
            Assert.True(h.Auto.DiscardCalls >= 1);
        }

        [Fact]
        public async Task Info_ReportsFirstMatchingLockReason()
        {
            var manual = new Harness();
            manual.Auto.AdjustmentSnapshots[HoaDonId] = Snap(manual, han: null);
            var closedLater = new Harness();
            closedLater.MeterStore.SubsequentPeriods.Add((Phong, 10, 2026));
            var noPermission = new Harness();
            noPermission.Access.Permissions.Remove((Actor, Chi, EmployeeActionCodes.InvoiceSubmit));

            Assert.Equal("Chỉ hóa đơn tự động mới dùng được Điều chỉnh và gửi lại.", (await manual.Service.GetAdjustmentInfoAsync(Actor, HoaDonId)).Data!.LyDoKhoa);
            Assert.Equal("Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể điều chỉnh.", (await closedLater.Service.GetAdjustmentInfoAsync(Actor, HoaDonId)).Data!.LyDoKhoa);
            Assert.Equal("Bạn không có quyền điều chỉnh hóa đơn tại chi nhánh này.", (await noPermission.Service.GetAdjustmentInfoAsync(Actor, HoaDonId)).Data!.LyDoKhoa);
        }

        private static AutoInvoiceAdjustmentSnapshot Snap(Harness h, DateTime? han) => new()
        {
            HoaDonId = HoaDonId, MaHoaDon = "HD-500", HopDongId = HopDong, ChiNhanhId = Chi, PhongTroId = Phong, Thang = 10, Nam = 2026,
            TrangThaiPhatHanh = h.Invoice.TrangThaiPhatHanh, TrangThaiHoaDon = h.Invoice.TrangThaiHoaDon, HanPhanHoi = han,
            DichVuDienNuocCuaPhongId = PeriodId, ChiSoDienCu = 100, ChiSoDienMoi = 120, ChiSoNuocCu = 50, ChiSoNuocMoi = 60
        };

        // ---------- Sửa các khoản phí khi điều chỉnh ----------

        private static ChiTietHoaDon Line(int id, string ten, decimal donGia, decimal soLuong, decimal tong, int? dichVuId = null)
            => new() { ChiTietHoaDonId = id, HoaDonId = HoaDonId, TenDichVu = ten, DonGia = donGia, SoLuong = soLuong, TongTien = tong, DichVuId = dichVuId };

        private static AdjustInvoiceLine Dong(int? id, string ten, decimal donGia, decimal soLuong)
            => new() { ChiTietHoaDonId = id, TenDichVu = ten, DonGia = donGia, SoLuong = soLuong };

        // Tiền phòng tính theo 15/30 ngày (SoLuong = 1, thành tiền thấp hơn đơn giá), điện, rác: tổng 1.670.000.
        private static ChiTietHoaDon[] BaseLines() => new[]
        {
            Line(1, "Tiền thuê phòng (thực tế ở 15/30 ngày)", 3_000_000m, 1m, 1_500_000m),
            Line(2, "Tiền điện (100 → 120)", 3_500m, 20m, 70_000m),
            Line(3, "Rác", 50_000m, 2m, 100_000m)
        };

        private static AdjustInvoiceLine[] BaseDongs() => new[]
        {
            Dong(1, "Tiền thuê phòng (thực tế ở 15/30 ngày)", 3_000_000m, 1m),
            Dong(2, "Tiền điện (100 → 120)", 3_500m, 20m),
            Dong(3, "Rác", 50_000m, 2m)
        };

        [Fact]
        public async Task Adjust_Provisional_EditedLinesOnly_ReplacesLinesKeepsProratedAmount_AndResends()
        {
            var h = new Harness();
            h.SetLines(BaseLines());
            var dongs = new[]
            {
                Dong(1, "Tiền thuê phòng (thực tế ở 15/30 ngày)", 3_000_000m, 1m),   // không đổi: giữ thành tiền theo ngày ở
                Dong(2, "Tiền điện (100 → 120)", 3_500m, 20m),                         // không đổi
                Dong(3, "Rác", 50_000m, 3m),                                           // đổi số lượng: tính lại
                Dong(null, "Phí giữ xe", 100_000m, 1m)                                 // dòng mới
            };

            var result = await h.AdjustLines(dongs);

            Assert.True(result.Success, result.Message);
            Assert.Empty(h.Meter.AdjustCalls);                                         // chỉ số không đổi nên không gọi lõi chỉ số
            var lines = h.Invoice.ChiTietHoaDonDichVus.ToList();
            Assert.Equal(4, lines.Count);
            Assert.Equal(1_500_000m, lines.Single(l => l.TenDichVu.StartsWith("Tiền thuê phòng")).TongTien);
            Assert.Equal(150_000m, lines.Single(l => l.TenDichVu == "Rác").TongTien);
            Assert.Equal(100_000m, lines.Single(l => l.TenDichVu == "Phí giữ xe").TongTien);
            Assert.Equal(1_820_000m, h.Invoice.TongTien);
            Assert.Equal(1_820_000m, result.Data!.TongTienMoi);
            Assert.False(result.Data.DaTaoHoaDonThayThe);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, h.Invoice.TrangThaiPhatHanh);
            Assert.Equal(Now.AddHours(24), h.Invoice.HanPhanHoi);
            Assert.True(Assert.Single(h.Uow.Transactions).Committed);

            var history = h.Invoice.LichSuTrangThaiHoaDons.ToList();
            Assert.Equal(2, history.Count);
            Assert.Contains("(sửa 1 dòng, thêm 1 dòng)", history[1].LyDo);

            var mail = Assert.Single(h.Email.ProvisionalCalls);
            Assert.True(mail.Data.LaDieuChinh);
            // Chỉ số gửi khách là chỉ số của kỳ (cũ → mới), không phải "giá trị trước điều chỉnh".
            Assert.Equal((100m, 120m, 50m, 60m), (mail.Data.ChiSoDienCu, mail.Data.ChiSoDienMoi, mail.Data.ChiSoNuocCu, mail.Data.ChiSoNuocMoi));
        }

        [Fact]
        public async Task Adjust_Provisional_ReadingsAndLines_AdjustsReadingsThenAppliesSubmittedLines()
        {
            var h = new Harness();
            h.SetLines(BaseLines());
            var dongs = new[]
            {
                Dong(1, "Tiền thuê phòng (thực tế ở 15/30 ngày)", 3_000_000m, 1m),
                Dong(2, "Tiền điện (100 → 130)", 3_500m, 30m)                           // dòng điện đã được màn điều chỉnh tính lại theo chỉ số mới
            };

            var result = await h.AdjustLines(dongs, dien: 130m, nuoc: 60m);

            Assert.True(result.Success, result.Message);
            var call = Assert.Single(h.Meter.AdjustCalls);
            Assert.Equal((130m, 60m), (call.Request.ChiSoDienMoi, call.Request.ChiSoNuocMoi));
            var lines = h.Invoice.ChiTietHoaDonDichVus.ToList();
            Assert.Equal(2, lines.Count);
            // Dòng gửi lên là danh sách cuối cùng: thành tiền tiền phòng lấy từ dòng đã lưu trước khi lõi chỉ số chạy, không phải bản bị tính lại.
            Assert.Equal(1_500_000m, lines[0].TongTien);
            Assert.Equal(105_000m, lines[1].TongTien);
            Assert.Equal(1_605_000m, h.Invoice.TongTien);
            var mail = Assert.Single(h.Email.ProvisionalCalls);
            Assert.Equal((100m, 130m), (mail.Data.ChiSoDienCu, mail.Data.ChiSoDienMoi));
            Assert.Contains("(sửa 1 dòng, xóa 1 dòng)", h.Invoice.LichSuTrangThaiHoaDons.Last().LyDo);
        }

        [Fact]
        public async Task Adjust_ReadingsOnly_KeepsRecalculatedLines_AndDoesNotTouchLineList()
        {
            var h = new Harness();

            var result = await h.Adjust();

            Assert.True(result.Success, result.Message);
            Assert.Single(h.Meter.AdjustCalls);
            Assert.Equal(1_070_000m, h.Invoice.TongTien);
            Assert.DoesNotContain("(sửa", h.Invoice.LichSuTrangThaiHoaDons.Last().LyDo);
        }

        [Fact]
        public async Task Adjust_IdenticalLinesAndSameReadings_IsRejectedAsNoChange()
        {
            var h = new Harness();
            h.SetLines(BaseLines());

            var result = await h.AdjustLines(BaseDongs(), dien: 120m, nuoc: 60m);

            Assert.False(result.Success);
            Assert.Equal("Không có thay đổi", result.Message);
            Assert.Empty(h.Meter.AdjustCalls);
            Assert.True(Assert.Single(h.Uow.Transactions).RolledBack);
        }

        [Fact]
        public async Task Adjust_RenamedLineOnly_CountsAsChange_AndKeepsStoredAmount()
        {
            var h = new Harness();
            h.SetLines(BaseLines());
            var dongs = BaseDongs();
            dongs[0] = Dong(1, "Tiền thuê phòng tháng 10", 3_000_000m, 1m);

            var result = await h.AdjustLines(dongs);

            Assert.True(result.Success, result.Message);
            Assert.Equal(1_670_000m, h.Invoice.TongTien);
            Assert.Equal("Tiền thuê phòng tháng 10", h.Invoice.ChiTietHoaDonDichVus.First().TenDichVu);
        }

        [Fact]
        public async Task Adjust_UnknownOrDuplicateLineId_IsTreatedAsNewLine_RecalculatedFromPriceAndQuantity()
        {
            var h = new Harness();
            h.SetLines(BaseLines());
            var dongs = new[]
            {
                Dong(999, "Tiền thuê phòng", 3_000_000m, 1m),   // id không thuộc hóa đơn: tính lại
                Dong(1, "Tiền thuê phòng (thực tế ở 15/30 ngày)", 3_000_000m, 1m),
                Dong(1, "Tiền thuê phòng (thực tế ở 15/30 ngày)", 3_000_000m, 1m)   // id lặp: chỉ lần đầu giữ thành tiền cũ
            };

            var result = await h.AdjustLines(dongs);

            Assert.True(result.Success, result.Message);
            var totals = h.Invoice.ChiTietHoaDonDichVus.Select(l => l.TongTien).ToArray();
            Assert.Equal(new[] { 3_000_000m, 1_500_000m, 3_000_000m }, totals);
        }

        [Fact]
        public async Task Adjust_KeepsServiceLinkOfExistingLines_AndLeavesNewLinesUnlinked()
        {
            var h = new Harness();
            h.SetLines(
                Line(1, "Tiền phòng", 1_000_000m, 1m, 1_000_000m),
                Line(2, "Internet", 100_000m, 1m, 100_000m, dichVuId: 7));
            var dongs = new[]
            {
                Dong(1, "Tiền phòng", 1_000_000m, 1m),
                Dong(2, "Internet", 100_000m, 2m),
                Dong(null, "Phí khác", 20_000m, 1m)
            };

            var result = await h.AdjustLines(dongs);

            Assert.True(result.Success, result.Message);
            var lines = h.Invoice.ChiTietHoaDonDichVus.ToList();
            Assert.Equal(7, lines.Single(l => l.TenDichVu == "Internet").DichVuId);
            Assert.Null(lines.Single(l => l.TenDichVu == "Phí khác").DichVuId);
            Assert.Equal(1_220_000m, h.Invoice.TongTien);
        }

        [Theory]
        [InlineData("", 100, 1, "Tên dịch vụ không được để trống.")]
        [InlineData("   ", 100, 1, "Tên dịch vụ không được để trống.")]
        [InlineData("Rác", -1, 1, "Đơn giá và số lượng phải lớn hơn hoặc bằng 0.")]
        [InlineData("Rác", 100, -1, "Đơn giá và số lượng phải lớn hơn hoặc bằng 0.")]
        [InlineData("Rác", 100.5, 1, "Đơn giá phải là số nguyên VND.")]
        [InlineData("Rác", 100, 1.2345, "Số lượng không được có quá 3 chữ số thập phân.")]
        [InlineData("Rác", 1e12, 1, "Số tiền của một dòng không được vượt quá 999.999.999.999 đ.")]
        public async Task Adjust_InvalidLine_IsRejectedBeforeAnyTransaction(string ten, double donGia, double soLuong, string expected)
        {
            var h = new Harness();

            var result = await h.AdjustLines(new[] { Dong(null, ten, (decimal)donGia, (decimal)soLuong) });

            Assert.False(result.Success);
            Assert.Equal(expected, result.Message);
            Assert.Empty(h.Uow.Transactions);
            Assert.Empty(h.Meter.AdjustCalls);
            Assert.Empty(h.Email.ProvisionalCalls);
        }

        [Fact]
        public async Task Adjust_EmptyLineList_NameTooLongOrTooManyLines_AreRejectedBeforeAnyTransaction()
        {
            var h = new Harness();

            var empty = await h.AdjustLines(Array.Empty<AdjustInvoiceLine>());
            var longName = await h.AdjustLines(new[] { Dong(null, new string('x', 501), 1_000m, 1m) });
            var tooMany = await h.AdjustLines(Enumerable.Range(1, 51).Select(i => Dong(null, "Phí " + i, 1_000m, 1m)).ToArray());

            Assert.Equal("Hóa đơn phải có ít nhất một khoản phí.", empty.Message);
            Assert.Equal("Tên khoản phí không được vượt quá 500 ký tự.", longName.Message);
            Assert.Equal("Hóa đơn tối đa 50 dòng khoản phí.", tooMany.Message);
            Assert.Empty(h.Uow.Transactions);
        }

        [Fact]
        public async Task Adjust_AllLinesZero_IsRejectedWithTotalMessage_AndRollsBack()
        {
            var h = new Harness();
            h.SetLines(BaseLines());
            var dongs = new[] { Dong(1, "Tiền thuê phòng", 0m, 1m), Dong(3, "Rác", 50_000m, 0m) };

            var result = await h.AdjustLines(dongs);

            Assert.False(result.Success);
            Assert.Equal("Tổng tiền hóa đơn phải lớn hơn 0.", result.Message);
            var tx = Assert.Single(h.Uow.Transactions);
            Assert.True(tx.RolledBack);
            Assert.False(tx.Committed);
            Assert.Empty(h.ThongBao.Sent);
            Assert.Empty(h.Email.ProvisionalCalls);
        }

        [Fact]
        public async Task Adjust_Sent_WithLines_ReplacementGetsSubmittedLines_AndKeepsProratedAmount()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.SetLines(BaseLines());
            var replacement = new HoaDon
            {
                HoaDonId = 501, HopDongId = HopDong, MaHoaDon = "HD-500-R1", Thang = 10, Nam = 2026, TongTien = 9_999m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                // Dòng do lõi tạo nháp tính lại từ kỳ: sẽ bị thay bằng danh sách người dùng đã chốt.
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon> { new() { ChiTietHoaDonId = 77, HoaDonId = 501, TenDichVu = "Tiền phòng", DonGia = 3_000_000m, SoLuong = 1, TongTien = 9_999m } }
            };
            replacement.KhoiTaoNhap(Actor);
            h.Issuance.Invoices[501] = replacement;
            h.HoaDonStore.Details[501] = h.HoaDonStore.Details[HoaDonId];
            h.IssuanceService.OnCreateDraftsInCurrentTransaction = (_, _) => ServiceResult<InvoiceDraftBatchResult>.Ok(new InvoiceDraftBatchResult
            {
                CreatedCount = 1,
                Items = new[]
                {
                    new InvoiceDraftItemResult { PhongTroId = Phong, HopDongId = HopDong, Status = InvoiceDraftItemStatus.Created, HoaDonId = 501, MaHoaDon = "HD-500-R1" }
                }
            });
            var dongs = new[]
            {
                Dong(1, "Tiền thuê phòng (thực tế ở 15/30 ngày)", 3_000_000m, 1m),
                Dong(3, "Rác", 50_000m, 4m)
            };

            var result = await h.AdjustLines(dongs);

            Assert.True(result.Success, result.Message);
            Assert.True(result.Data!.DaTaoHoaDonThayThe);
            Assert.Empty(h.Meter.AdjustCalls);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, h.Invoice.TrangThaiPhatHanh);
            var lines = replacement.ChiTietHoaDonDichVus.ToList();
            Assert.Equal(2, lines.Count);
            Assert.Equal(1_500_000m, lines[0].TongTien);   // thành tiền theo ngày ở lấy từ dòng của hóa đơn cũ
            Assert.Equal(200_000m, lines[1].TongTien);
            Assert.Equal(1_700_000m, replacement.TongTien);
            Assert.Equal(1_700_000m, result.Data.TongTienMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, replacement.TrangThaiPhatHanh);
            Assert.Contains("(sửa 1 dòng, xóa 1 dòng)", replacement.LichSuTrangThaiHoaDons.Last().LyDo);
        }

        [Fact]
        public async Task Adjust_Sent_LinesOnly_StillCancelsOldInvoice_WithoutCallingMeterCore()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            var replacement = new HoaDon
            {
                HoaDonId = 501, HopDongId = HopDong, MaHoaDon = "HD-500-R1", Thang = 10, Nam = 2026, TongTien = 1_000_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon> { new() { ChiTietHoaDonId = 77, HoaDonId = 501, TenDichVu = "Tiền phòng", TongTien = 1_000_000m } }
            };
            replacement.KhoiTaoNhap(Actor);
            h.Issuance.Invoices[501] = replacement;
            h.HoaDonStore.Details[501] = h.HoaDonStore.Details[HoaDonId];
            h.IssuanceService.OnCreateDraftsInCurrentTransaction = (_, _) => ServiceResult<InvoiceDraftBatchResult>.Ok(new InvoiceDraftBatchResult
            {
                CreatedCount = 1,
                Items = new[] { new InvoiceDraftItemResult { PhongTroId = Phong, HopDongId = HopDong, Status = InvoiceDraftItemStatus.Created, HoaDonId = 501, MaHoaDon = "HD-500-R1" } }
            });

            var result = await h.AdjustLines(new[] { Dong(1, "Tiền phòng", 1_200_000m, 1m) });

            Assert.True(result.Success, result.Message);
            Assert.Empty(h.Meter.AdjustCalls);
            Assert.True(h.Invoice.IsDeleted);
            Assert.Equal(1_200_000m, replacement.TongTien);
            Assert.Equal(501, result.Data!.HoaDonIdMoi);
        }

        // ---------- Thông tin điều chỉnh có các dòng khoản phí ----------

        [Fact]
        public async Task Info_ReportsLinesWithKindUnitAndTotal()
        {
            var h = new Harness();
            h.SetLines(
                Line(1, "Tiền thuê phòng", 3_000_000m, 1m, 3_000_000m),
                Line(2, "Tiền điện (100 → 120)", 3_500m, 20m, 70_000m),
                Line(3, "Tiền nước (50 → 60)", 15_000m, 10m, 150_000m),
                Line(4, "Điện thoại cố định", 30_000m, 1m, 30_000m));
            // Dịch vụ gắn loại: phải theo loại dịch vụ chứ không theo tên.
            var snap = h.Auto.AdjustmentSnapshots[HoaDonId];
            h.Auto.AdjustmentSnapshots[HoaDonId] = new AutoInvoiceAdjustmentSnapshot
            {
                HoaDonId = snap.HoaDonId, MaHoaDon = snap.MaHoaDon, HopDongId = snap.HopDongId, ChiNhanhId = snap.ChiNhanhId, PhongTroId = snap.PhongTroId,
                Thang = snap.Thang, Nam = snap.Nam, TrangThaiPhatHanh = snap.TrangThaiPhatHanh, TrangThaiHoaDon = snap.TrangThaiHoaDon, HanPhanHoi = snap.HanPhanHoi,
                DichVuDienNuocCuaPhongId = snap.DichVuDienNuocCuaPhongId, ChiSoDienCu = snap.ChiSoDienCu, ChiSoDienMoi = snap.ChiSoDienMoi,
                ChiSoNuocCu = snap.ChiSoNuocCu, ChiSoNuocMoi = snap.ChiSoNuocMoi, TongTien = snap.TongTien,
                ChiTiets = snap.ChiTiets.Concat(new[] { new AutoInvoiceInvoiceLine(5, "Nước sinh hoạt thêm", 12_000m, 1m, 12_000m, 3, LoaiDichVu.Nuoc, "m3") }).ToList()
            };

            var result = await h.Service.GetAdjustmentInfoAsync(Actor, HoaDonId);

            Assert.True(result.Success);
            var info = result.Data!;
            Assert.Equal(3_250_000m, info.TongTien);
            Assert.Equal(
                new[] { ("khac", ""), ("dien", "kWh"), ("nuoc", "m³"), ("khac", ""), ("nuoc", "m3") },
                info.ChiTiets.Select(l => (l.Loai, l.DonVi)).ToArray());
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, info.ChiTiets.Select(l => l.ChiTietHoaDonId).ToArray());
        }

        // ---------- Xem trước dòng điện/nước ----------

        [Fact]
        public async Task Preview_FullMonthContract_ComputesLinesFromNewReadings()
        {
            var h = new Harness();

            var result = await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 130m, 70m);

            Assert.True(result.Success, result.Message);
            var dien = result.Data!.Dien!;
            Assert.Equal("Tiền điện (100 → 130)", dien.TenDichVu);
            Assert.Equal((3_500m, 30m, 105_000m, "dien", "kWh"), (dien.DonGia, dien.SoLuong, dien.TongTien, dien.Loai, dien.DonVi));
            var nuoc = result.Data.Nuoc!;
            Assert.Equal("Tiền nước (50 → 70)", nuoc.TenDichVu);
            Assert.Equal((15_000m, 20m, 300_000m, "nuoc", "m³"), (nuoc.DonGia, nuoc.SoLuong, nuoc.TongTien, nuoc.Loai, nuoc.DonVi));
            Assert.Equal((30m, 20m), (result.Data.TieuThuDien, result.Data.TieuThuNuoc));
            Assert.Empty(h.Uow.Transactions);
            Assert.Empty(h.Meter.AdjustCalls);
            Assert.True(h.Auto.DiscardCalls >= 1);
        }

        [Fact]
        public async Task Preview_ContractStartedMidMonth_UsesDayProportion()
        {
            var h = new Harness();
            var snap = h.Auto.AdjustmentSnapshots[HoaDonId];
            h.Auto.AdjustmentSnapshots[HoaDonId] = new AutoInvoiceAdjustmentSnapshot
            {
                HoaDonId = snap.HoaDonId, MaHoaDon = snap.MaHoaDon, HopDongId = snap.HopDongId, ChiNhanhId = snap.ChiNhanhId, PhongTroId = snap.PhongTroId,
                Thang = snap.Thang, Nam = snap.Nam, TrangThaiPhatHanh = snap.TrangThaiPhatHanh, TrangThaiHoaDon = snap.TrangThaiHoaDon, HanPhanHoi = snap.HanPhanHoi,
                DichVuDienNuocCuaPhongId = snap.DichVuDienNuocCuaPhongId, ChiSoDienCu = snap.ChiSoDienCu, ChiSoDienMoi = snap.ChiSoDienMoi,
                ChiSoNuocCu = snap.ChiSoNuocCu, ChiSoNuocMoi = snap.ChiSoNuocMoi, DonGiaDien = snap.DonGiaDien, DonGiaNuoc = snap.DonGiaNuoc,
                TienThuePhong = snap.TienThuePhong,
                // 16/10/2026 giờ Việt Nam: ở 16/31 ngày.
                HopDongBatDau = new DateTime(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc)
            };

            var result = await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 130m, 60m);

            Assert.True(result.Success, result.Message);
            var dien = result.Data!.Dien!;
            Assert.Equal("Tiền điện (tỷ lệ 16/31 ngày, 100 → 130)", dien.TenDichVu);
            Assert.Equal(15.484m, dien.SoLuong);
            Assert.Equal(54_194m, dien.TongTien);
        }

        [Fact]
        public async Task Preview_NoConsumption_ReturnsNoLines()
        {
            var h = new Harness();

            var result = await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 100m, 50m);

            Assert.True(result.Success, result.Message);
            Assert.Null(result.Data!.Dien);
            Assert.Null(result.Data.Nuoc);
            Assert.Equal((0m, 0m), (result.Data.TieuThuDien, result.Data.TieuThuNuoc));
        }

        [Fact]
        public async Task Preview_InvalidReadings_AreRejected()
        {
            var h = new Harness();

            Assert.Equal("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.", (await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 99m, 60m)).Message);
            Assert.Equal("Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.", (await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 130m, 49m)).Message);
            Assert.Equal("Chỉ số mới không được âm.", (await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, -1m, 60m)).Message);
            Assert.Equal("Chỉ số mới không được có quá 3 chữ số thập phân.", (await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 130.1234m, 60m)).Message);
            Assert.False((await h.Service.PreviewMeterLinesAsync(0, HoaDonId, 130m, 60m)).Success);
        }

        [Fact]
        public async Task Preview_WithoutInvoiceRead_IsForbidden_AndUnknownInvoiceIsNotFound()
        {
            var h = new Harness();
            h.Access.Permissions.Remove((Actor, Chi, EmployeeActionCodes.InvoiceRead));

            var forbidden = await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 130m, 60m);
            var missing = await h.Service.PreviewMeterLinesAsync(Actor, 999, 130m, 60m);

            Assert.Equal(ServiceErrorKind.Forbidden, forbidden.ErrorKind);
            Assert.Null(forbidden.Data);
            Assert.Equal(ServiceErrorKind.NotFound, missing.ErrorKind);
        }
    }
}
