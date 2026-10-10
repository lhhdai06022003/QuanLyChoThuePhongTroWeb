using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
                DateTime? han = null, bool grantAll = true)
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
                    ChiTietHoaDonDichVus = new List<ChiTietHoaDon> { new() { TenDichVu = "Tiền phòng", TongTien = 1_000_000m } }
                };
                Issuance.Invoices[HoaDonId] = Invoice;
                Auto.AdjustmentSnapshots[HoaDonId] = new AutoInvoiceAdjustmentSnapshot
                {
                    HoaDonId = HoaDonId, MaHoaDon = "HD-500", HopDongId = HopDong, ChiNhanhId = Chi, PhongTroId = Phong, Thang = 10, Nam = 2026,
                    TrangThaiPhatHanh = status, TrangThaiHoaDon = pay, HanPhanHoi = hasHan, DichVuDienNuocCuaPhongId = PeriodId,
                    ChiSoDienCu = 100, ChiSoDienMoi = 120, ChiSoNuocCu = 50, ChiSoNuocMoi = 60
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
                    Invoice.TongTien = 1_070_000m;
                    Invoice.ChiTietHoaDonDichVus.First().TongTien = 1_070_000m;
                    return ServiceResult<MeterAdjustmentResult>.Ok(new MeterAdjustmentResult
                    {
                        ChiSoDienCu = 120, ChiSoDienMoi = req.ChiSoDienMoi, ChiSoNuocCu = 60, ChiSoNuocMoi = req.ChiSoNuocMoi
                    });
                };
                if (grantAll)
                {
                    foreach (var action in new[] { EmployeeActionCodes.MeterReview, EmployeeActionCodes.InvoiceSubmit, EmployeeActionCodes.InvoiceCancel, EmployeeActionCodes.InvoiceRead })
                    {
                        Access.Permissions.Add((Actor, Chi, action));
                    }
                }

                Service = new InvoiceAdjustmentService(
                    Auto, Issuance, IssuanceService, Meter, MeterStore, Payments, Publication, HoaDonStore, Uow, Access, ThongBao, Email, Logger,
                    new FixedTimeProvider(new DateTimeOffset(Now)));
            }

            public Task<ServiceResult<InvoiceAdjustmentResult>> Adjust(decimal dien = 130m, decimal nuoc = 60m, int actor = Actor)
                => Service.AdjustAsync(actor, new AdjustInvoiceRequest { HoaDonId = HoaDonId, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc });
        }

        // ---------- Quyền ----------

        [Theory]
        [InlineData(EmployeeActionCodes.MeterReview)]
        [InlineData(EmployeeActionCodes.InvoiceSubmit)]
        public async Task Adjust_MissingMeterReviewOrInvoiceSubmit_IsForbidden_AndNothingStarts(string missing)
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
    }
}
