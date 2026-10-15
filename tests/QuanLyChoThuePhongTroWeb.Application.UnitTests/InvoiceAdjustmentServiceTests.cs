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
        // Actor là nhân viên (không có Invoice.Finalize), AdminActor có thêm Invoice.Finalize.
        private const int Actor = 7;
        private const int AdminActor = 1;
        private const int Chi = 1;
        private const int Phong = 101;
        private const int HopDong = 10;
        private const int HoaDonId = 500;
        private const int PeriodId = 40;
        private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime OldHan = Now.AddHours(10);
        private const string TrangThaiDaDoi = "Hóa đơn vừa đổi trạng thái, vui lòng tải lại.";
        private const string ChiAdminDaChot = "Chỉ Quản trị viên mới điều chỉnh được hóa đơn đã chốt.";

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
            public CapturingLogger<InvoiceAdjustmentService> Logger { get; } = new();
            public FakeMeterConsumptionHistoryStore HistoryStore { get; } = new();
            public InvoiceAdjustmentService Service { get; }
            public HoaDon Invoice { get; }

            public Harness(TrangThaiPhatHanhHoaDon status = TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon pay = TrangThaiHoaDon.ChuaThanhToan,
                DateTime? han = null, bool grantAll = true)
            {
                var hasHan = han ?? OldHan;
                var daChot = status == TrangThaiPhatHanhHoaDon.DaChot;
                Invoice = new HoaDon
                {
                    HoaDonId = HoaDonId, HopDongId = HopDong, MaHoaDon = "HD-500", Thang = 10, Nam = 2026, TongTien = 1_000_000m,
                    TrangThaiPhatHanh = status, TrangThaiHoaDon = pay, HanPhanHoi = hasHan, DichVuDienNuocCuaPhongId = PeriodId,
                    NguoiChotId = daChot ? 2 : null, NgayChot = daChot ? Now.AddDays(-1) : null,
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
                    foreach (var action in new[] { EmployeeActionCodes.MeterReview, EmployeeActionCodes.InvoiceDraft, EmployeeActionCodes.InvoiceCancel, EmployeeActionCodes.InvoiceRead })
                    {
                        Access.Permissions.Add((Actor, Chi, action));
                        Access.Permissions.Add((AdminActor, Chi, action));
                    }

                    Access.Permissions.Add((AdminActor, Chi, EmployeeActionCodes.InvoiceFinalize));
                }

                Service = new InvoiceAdjustmentService(
                    Auto, Issuance, IssuanceService, Meter, MeterStore, Payments, Publication, HoaDonStore, Uow, Access, ThongBao,
                    new HoaDonCalculatorService(), HistoryStore, new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.AutoInvoiceOptions(), Logger,
                    new FixedTimeProvider(new DateTimeOffset(Now)));
            }

            // hienTai null = trạng thái màn hình khớp trạng thái hiện tại của hóa đơn.
            public Task<ServiceResult<InvoiceAdjustmentResult>> Adjust(decimal dien = 130m, decimal nuoc = 60m, int actor = Actor, TrangThaiPhatHanhHoaDon? hienTai = null)
                => Service.AdjustAsync(actor, new AdjustInvoiceRequest
                {
                    HoaDonId = HoaDonId, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc,
                    TrangThaiPhatHanhHienTai = (int)(hienTai ?? Invoice.TrangThaiPhatHanh)
                });

            // Điều chỉnh có danh sách dòng; chỉ số null = giữ nguyên.
            public Task<ServiceResult<InvoiceAdjustmentResult>> AdjustLines(IReadOnlyList<AdjustInvoiceLine> lines, decimal? dien = null, decimal? nuoc = null, int actor = Actor)
                => Service.AdjustAsync(actor, new AdjustInvoiceRequest
                {
                    HoaDonId = HoaDonId, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc, ChiTiets = lines,
                    TrangThaiPhatHanhHienTai = (int)Invoice.TrangThaiPhatHanh
                });

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

            // Lõi tạo nháp trả một bản thay thế -R1 ở Nhap cho nhánh DaGui.
            public HoaDon SetupReplacement(decimal tongTien = 1_070_000m, List<ChiTietHoaDon>? lines = null)
            {
                var replacement = new HoaDon
                {
                    HoaDonId = 501, HopDongId = HopDong, MaHoaDon = "HD-500-R1", Thang = 10, Nam = 2026, TongTien = tongTien,
                    TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                    ChiTietHoaDonDichVus = lines ?? new List<ChiTietHoaDon> { new() { ChiTietHoaDonId = 77, HoaDonId = 501, TenDichVu = "Tiền phòng", TongTien = tongTien } }
                };
                replacement.KhoiTaoNhap(Actor);
                Issuance.Invoices[501] = replacement;
                IssuanceService.OnCreateDraftsInCurrentTransaction = (_, _) => ServiceResult<InvoiceDraftBatchResult>.Ok(new InvoiceDraftBatchResult
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
                return replacement;
            }
        }

        // ---------- Quyền ----------

        [Theory]
        [InlineData(EmployeeActionCodes.MeterReview)]
        [InlineData(EmployeeActionCodes.InvoiceDraft)]
        public async Task Adjust_MissingMeterReviewOrInvoiceDraft_IsForbidden_AndNothingStarts(string missing)
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
            foreach (var action in new[] { EmployeeActionCodes.MeterReview, EmployeeActionCodes.InvoiceDraft })
            {
                h.Access.Permissions.Add((Actor, 99, action));
            }

            var result = await h.Adjust();

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Empty(h.Uow.Transactions);
        }

        [Fact]
        public async Task NhanVienDieuChinhDaChot_Forbidden()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaChot);

            var result = await h.Adjust();
            var info = await h.Service.GetAdjustmentInfoAsync(Actor, HoaDonId);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Equal(ChiAdminDaChot, result.Message);
            var tx = Assert.Single(h.Uow.Transactions);
            Assert.True(tx.RolledBack);
            Assert.False(tx.Committed);
            Assert.Empty(h.Meter.AdjustCalls);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, h.Invoice.TrangThaiPhatHanh);
            Assert.Equal(2, h.Invoice.NguoiChotId);
            Assert.False(info.Data!.CoTheDieuChinh);
            Assert.Equal(ChiAdminDaChot, info.Data.LyDoKhoa);
        }

        [Fact]
        public async Task Adjust_UnknownInvoice_IsNotFound_AndInvalidArgumentsAreRejected()
        {
            var h = new Harness();

            var missing = await h.Service.AdjustAsync(Actor, new AdjustInvoiceRequest { HoaDonId = 999, ChiSoDienMoi = 130, ChiSoNuocMoi = 60, TrangThaiPhatHanhHienTai = 1 });
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
        public async Task HoaDonLapTay_DieuChinhDuoc()
        {
            // Hóa đơn lập tay không có hạn phản hồi vẫn điều chỉnh được.
            var h = new Harness();
            h.Invoice.HanPhanHoi = null;
            h.Auto.AdjustmentSnapshots[HoaDonId] = Snap(h, han: null);

            var info = await h.Service.GetAdjustmentInfoAsync(Actor, HoaDonId);
            var result = await h.Adjust();

            Assert.True(info.Data!.CoTheDieuChinh, info.Data.LyDoKhoa);
            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, h.Invoice.TrangThaiPhatHanh);
            Assert.Null(h.Invoice.HanPhanHoi);
            Assert.True(Assert.Single(h.Uow.Transactions).Committed);
            Assert.Single(h.Meter.AdjustCalls);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaHuy)]
        public async Task Adjust_StatusOtherThanChoDuyetDaChotOrDaGui_IsRejected(TrangThaiPhatHanhHoaDon status)
        {
            var h = new Harness(status);

            var result = await h.Adjust(actor: AdminActor);
            var info = await h.Service.GetAdjustmentInfoAsync(AdminActor, HoaDonId);

            Assert.Equal("Hóa đơn không ở trạng thái điều chỉnh được.", result.Message);
            Assert.Equal("Hóa đơn không ở trạng thái điều chỉnh được.", info.Data!.LyDoKhoa);
            Assert.Empty(h.Meter.AdjustCalls);
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
        }

        [Fact]
        public async Task DaGuiCoKhoanThu_BiKhoa()
        {
            var partial = new Harness(TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan);
            var ledger = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            ledger.Payments.Ledger.Add(new LichSuThanhToan { HoaDonId = HoaDonId, SoTienThanhToan = 50_000m, MaGiaoDich = "GD1" });

            var byStatus = await partial.Adjust();
            var byLedger = await ledger.Adjust();
            var info = await ledger.Service.GetAdjustmentInfoAsync(Actor, HoaDonId);

            Assert.Equal("Hóa đơn đã có khoản thu, không thể điều chỉnh.", byStatus.Message);
            Assert.Equal("Hóa đơn đã có khoản thu, không thể điều chỉnh.", byLedger.Message);
            Assert.True(Assert.Single(ledger.Uow.Transactions).RolledBack);
            Assert.Empty(ledger.IssuanceService.CreateInCurrentTransactionCalls);
            Assert.False(ledger.Invoice.IsDeleted);
            Assert.False(info.Data!.CoTheDieuChinh);
            Assert.Equal("Hóa đơn đã có khoản thu, không thể điều chỉnh.", info.Data.LyDoKhoa);
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

        // ---------- Trạng thái đổi giữa lúc mở màn và lúc lưu ----------

        [Fact]
        public async Task TrangThaiDaDoi_TuChoi()
        {
            // Màn hình thấy Chờ duyệt nhưng hóa đơn đã được chốt.
            var daChot = new Harness(TrangThaiPhatHanhHoaDon.DaChot);
            // Màn hình thấy Đã chốt nhưng hóa đơn đã được công bố.
            var daGui = new Harness(TrangThaiPhatHanhHoaDon.DaGui);

            var r1 = await daChot.Adjust(actor: AdminActor, hienTai: TrangThaiPhatHanhHoaDon.ChoDuyet);
            var r2 = await daGui.Adjust(actor: AdminActor, hienTai: TrangThaiPhatHanhHoaDon.DaChot);

            foreach (var (h, r, status) in new[] { (daChot, r1, TrangThaiPhatHanhHoaDon.DaChot), (daGui, r2, TrangThaiPhatHanhHoaDon.DaGui) })
            {
                Assert.False(r.Success);
                Assert.Equal(TrangThaiDaDoi, r.Message);
                var tx = Assert.Single(h.Uow.Transactions);
                Assert.True(tx.RolledBack);
                Assert.False(tx.Committed);
                Assert.Empty(h.Meter.AdjustCalls);
                Assert.Empty(h.IssuanceService.CreateInCurrentTransactionCalls);
                Assert.Equal(status, h.Invoice.TrangThaiPhatHanh);
                Assert.False(h.Invoice.IsDeleted);
                Assert.Empty(h.Invoice.LichSuTrangThaiHoaDons);
                Assert.Empty(h.ThongBao.Sent);
            }
        }

        // ---------- Nhánh ChoDuyet ----------

        [Fact]
        public async Task NhanVienDieuChinhChoDuyet_VanChoDuyet()
        {
            var h = new Harness();

            var result = await h.Adjust();

            Assert.True(result.Success, result.Message);
            Assert.Equal("Đã điều chỉnh hóa đơn, hóa đơn vẫn chờ duyệt.", result.Message);
            Assert.False(result.Data!.DaTaoHoaDonThayThe);
            Assert.Equal(HoaDonId, result.Data.HoaDonIdMoi);
            Assert.Equal(1_070_000m, result.Data.TongTienMoi);
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, result.Data.TrangThaiPhatHanhMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, h.Invoice.TrangThaiPhatHanh);
            Assert.Null(h.Invoice.HanPhanHoi);
            var tx = Assert.Single(h.Uow.Transactions);
            Assert.True(tx.Committed);
            Assert.False(tx.RolledBack);
            Assert.Equal(new[] { "LockMeterPeriod", "LockContracts", "GetInvoiceForUpdate" }, h.Issuance.Log.Take(3).ToArray());

            var call = Assert.Single(h.Meter.AdjustCalls);
            Assert.Equal(PeriodId, call.Request.DichVuDienNuocCuaPhongId);
            Assert.Equal(130m, call.Request.ChiSoDienMoi);
            Assert.Equal("Điều chỉnh hóa đơn", call.Request.LyDo);

            var history = h.Invoice.LichSuTrangThaiHoaDons.ToList();
            Assert.Equal(2, history.Count);
            Assert.All(history, e => Assert.StartsWith("[DIEU_CHINH]", e.LyDo));
            Assert.All(history, e => Assert.Equal(Actor, e.NguoiThucHienId));
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, history[0].TrangThaiPhatHanhMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, history[1].TrangThaiPhatHanhMoi);

            // Hóa đơn chưa công bố: không báo khách.
            Assert.Empty(h.ThongBao.Sent);
        }

        [Fact]
        public async Task AdminDieuChinhDaChot_VanDaChot()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaChot);

            var result = await h.Adjust(actor: AdminActor);

            Assert.True(result.Success, result.Message);
            Assert.Equal("Đã điều chỉnh hóa đơn đã chốt.", result.Message);
            Assert.False(result.Data!.DaTaoHoaDonThayThe);
            Assert.Equal(HoaDonId, result.Data.HoaDonIdMoi);
            Assert.Equal(1_070_000m, result.Data.TongTienMoi);
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.DaChot, result.Data.TrangThaiPhatHanhMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, h.Invoice.TrangThaiPhatHanh);
            Assert.Equal(AdminActor, h.Invoice.NguoiChotId);
            Assert.NotNull(h.Invoice.NgayChot);
            Assert.Null(h.Invoice.HanPhanHoi);
            Assert.False(h.Invoice.IsDeleted);
            Assert.True(Assert.Single(h.Uow.Transactions).Committed);
            Assert.Single(h.Meter.AdjustCalls);

            var history = h.Invoice.LichSuTrangThaiHoaDons.ToList();
            Assert.Equal(
                new[]
                {
                    (TrangThaiPhatHanhHoaDon.DaChot, TrangThaiPhatHanhHoaDon.Nhap),
                    (TrangThaiPhatHanhHoaDon.Nhap, TrangThaiPhatHanhHoaDon.ChoDuyet),
                    (TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiPhatHanhHoaDon.DaChot)
                },
                history.Select(e => (e.TrangThaiPhatHanhCu!.Value, e.TrangThaiPhatHanhMoi)).ToArray());
            Assert.All(history, e => Assert.StartsWith("[DIEU_CHINH]", e.LyDo));
            Assert.Empty(h.ThongBao.Sent);
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

        // ---------- Nhánh DaGui ----------

        [Fact]
        public async Task DieuChinhDaGui_TaoThayTheChoDuyet_BaoKhachCong()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            var waiting = YeuCauThanhToanHoaDon.Tao(HoaDonId, "YC1", 1_000_000m, Now.AddDays(1), Actor, Now.AddHours(-1));
            h.Payments.Requests.Add(waiting);
            var replacement = h.SetupReplacement();

            var result = await h.Adjust();

            Assert.True(result.Success, result.Message);
            Assert.Equal("Đã hủy hóa đơn cũ và tạo hóa đơn thay thế chờ duyệt.", result.Message);
            Assert.True(result.Data!.DaTaoHoaDonThayThe);
            Assert.Equal(501, result.Data.HoaDonIdMoi);
            Assert.Equal("HD-500-R1", result.Data.MaHoaDonMoi);
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, result.Data.TrangThaiPhatHanhMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, h.Invoice.TrangThaiPhatHanh);
            Assert.True(h.Invoice.IsDeleted);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, waiting.TrangThai);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, replacement.TrangThaiPhatHanh);
            Assert.Null(replacement.HanPhanHoi);
            Assert.Contains("Tạo hóa đơn thay thế sau điều chỉnh", replacement.LichSuTrangThaiHoaDons.Last().LyDo);
            Assert.StartsWith("[DIEU_CHINH]", replacement.LichSuTrangThaiHoaDons.Last().LyDo);
            var call = Assert.Single(h.IssuanceService.CreateInCurrentTransactionCalls);
            Assert.StartsWith("[DIEU_CHINH]", call.LyDoKhoiTao);
            Assert.Equal(new[] { Phong }, call.Request.PhongTroIds);
            Assert.True(Assert.Single(h.Uow.Transactions).Committed);

            // Chỉ báo khách qua cổng, nội dung nói rõ bản cũ đã hủy và bản mới chờ duyệt.
            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Khach", notice.Kind);
            Assert.Equal(9, notice.TargetId);
            Assert.Equal("Hóa đơn tháng 10/2026 đã được điều chỉnh", notice.TieuDe);
            Assert.Equal("Hóa đơn HD-500 đã được hủy để điều chỉnh, hóa đơn mới sẽ được gửi sau khi duyệt.", notice.NoiDung);
            Assert.Equal("HoaDon", notice.LinhVuc);
            Assert.Equal("/KhachThue/HoaDon", notice.Link);
        }

        [Fact]
        public async Task Adjust_Sent_NotificationFailure_StillSucceeds_WithWarningOnly()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.SetupReplacement();
            h.ThongBao.ThrowOnSend = true;

            var result = await h.Adjust();

            Assert.True(result.Success, result.Message);
            Assert.True(Assert.Single(h.Uow.Transactions).Committed);
            Assert.Single(h.Logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(h.Logger.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact]
        public async Task Adjust_Sent_WithoutRecipient_StillSucceeds_WithWarningOnly()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.SetupReplacement();
            h.Publication.Recipients.Remove(HopDong);

            var result = await h.Adjust();

            Assert.True(result.Success, result.Message);
            Assert.Empty(h.ThongBao.Sent);
            Assert.Single(h.Logger.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task Adjust_Sent_WithoutRecipient_MessageAsksStaffToContactCustomer()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.SetupReplacement();
            h.Publication.Recipients.Remove(HopDong);

            var result = await h.Adjust();

            Assert.True(result.Success, result.Message);
            Assert.False(result.Data!.KhachDaDuocBao);
            Assert.Equal("Đã hủy hóa đơn cũ và tạo hóa đơn thay thế chờ duyệt. Chưa báo được khách qua cổng, hãy liên hệ khách.", result.Message);
        }

        [Fact]
        public async Task Adjust_Sent_NotificationFailure_MessageAsksStaffToContactCustomer()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.SetupReplacement();
            h.ThongBao.ThrowOnSend = true;

            var result = await h.Adjust();

            Assert.False(result.Data!.KhachDaDuocBao);
            Assert.Contains("Chưa báo được khách qua cổng, hãy liên hệ khách.", result.Message);
        }

        [Fact]
        public async Task Adjust_Sent_CustomerNotified_MessageHasNoContactHint()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.SetupReplacement();

            var result = await h.Adjust();

            Assert.True(result.Data!.KhachDaDuocBao);
            Assert.DoesNotContain("Chưa báo được khách", result.Message);
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
            Assert.Empty(h.ThongBao.Sent);
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
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, result.Data.TrangThaiPhatHanhValue);
            Assert.Equal((100m, 120m, 50m, 60m), (result.Data.ChiSoDienCu, result.Data.ChiSoDienMoi, result.Data.ChiSoNuocCu, result.Data.ChiSoNuocMoi));
            // Hạn phản hồi chỉ có ý nghĩa với hóa đơn đã gửi.
            Assert.Null(result.Data.HanPhanHoiUtc);
            Assert.True(h.Auto.DiscardCalls >= 1);
        }

        [Fact]
        public async Task Info_AdminOnDaChot_IsAdjustable()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaChot);

            var result = await h.Service.GetAdjustmentInfoAsync(AdminActor, HoaDonId);

            Assert.True(result.Data!.CoTheDieuChinh, result.Data.LyDoKhoa);
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.DaChot, result.Data.TrangThaiPhatHanhValue);
            Assert.Null(result.Data.HanPhanHoiUtc);
        }

        [Fact]
        public async Task Info_ReportsFirstMatchingLockReason()
        {
            var closedLater = new Harness();
            closedLater.MeterStore.SubsequentPeriods.Add((Phong, 10, 2026));
            var noPermission = new Harness();
            noPermission.Access.Permissions.Remove((Actor, Chi, EmployeeActionCodes.InvoiceDraft));
            var noPeriod = new Harness();
            var snap = Snap(noPeriod, han: OldHan);
            noPeriod.Auto.AdjustmentSnapshots[HoaDonId] = new AutoInvoiceAdjustmentSnapshot
            {
                HoaDonId = snap.HoaDonId, MaHoaDon = snap.MaHoaDon, HopDongId = snap.HopDongId, ChiNhanhId = snap.ChiNhanhId, PhongTroId = snap.PhongTroId,
                Thang = snap.Thang, Nam = snap.Nam, TrangThaiPhatHanh = snap.TrangThaiPhatHanh, TrangThaiHoaDon = snap.TrangThaiHoaDon, DichVuDienNuocCuaPhongId = null
            };

            Assert.Equal("Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể điều chỉnh.", (await closedLater.Service.GetAdjustmentInfoAsync(Actor, HoaDonId)).Data!.LyDoKhoa);
            Assert.Equal("Bạn không có quyền điều chỉnh hóa đơn tại chi nhánh này.", (await noPermission.Service.GetAdjustmentInfoAsync(Actor, HoaDonId)).Data!.LyDoKhoa);
            Assert.Equal("Hóa đơn không gắn kỳ chỉ số điện nước.", (await noPeriod.Service.GetAdjustmentInfoAsync(Actor, HoaDonId)).Data!.LyDoKhoa);
        }

        [Fact]
        public async Task Info_SentInvoice_ReportsResponseDeadline()
        {
            var han = Now.AddHours(-30);
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui, han: han);

            var result = await h.Service.GetAdjustmentInfoAsync(Actor, HoaDonId);

            Assert.True(result.Data!.CoTheDieuChinh);
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.DaGui, result.Data.TrangThaiPhatHanhValue);
            Assert.Equal(han, result.Data.HanPhanHoiUtc);
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
        public async Task Adjust_ChoDuyet_EditedLinesOnly_ReplacesLinesKeepsProratedAmount()
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
            Assert.True(Assert.Single(h.Uow.Transactions).Committed);

            var history = h.Invoice.LichSuTrangThaiHoaDons.ToList();
            Assert.Equal(2, history.Count);
            Assert.Contains("(sửa 1 dòng, thêm 1 dòng)", history[1].LyDo);
            Assert.Empty(h.ThongBao.Sent);
        }

        [Fact]
        public async Task Adjust_ChoDuyet_ReadingsAndLines_AdjustsReadingsThenAppliesSubmittedLines()
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
            Assert.Contains("(sửa 1 dòng, xóa 1 dòng)", h.Invoice.LichSuTrangThaiHoaDons.Last().LyDo);
        }

        [Fact]
        public async Task Adjust_DaChot_EditedLines_AdminKeepsFinalizedState_WithLineSummary()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaChot);
            h.SetLines(BaseLines());
            var dongs = BaseDongs();
            dongs[2] = Dong(3, "Rác", 50_000m, 3m);

            var result = await h.AdjustLines(dongs, actor: AdminActor);

            Assert.True(result.Success, result.Message);
            Assert.Equal(1_720_000m, h.Invoice.TongTien);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, h.Invoice.TrangThaiPhatHanh);
            Assert.Contains(h.Invoice.LichSuTrangThaiHoaDons, e => e.LyDo != null && e.LyDo.Contains("(sửa 1 dòng)"));
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
        public async Task Adjust_SameLinesInAnotherOrder_IsStillNoChange()
        {
            var h = new Harness();
            h.SetLines(BaseLines());
            var dongs = BaseDongs().Reverse().ToArray();

            var result = await h.AdjustLines(dongs);

            Assert.Equal("Không có thay đổi", result.Message);
            Assert.True(Assert.Single(h.Uow.Transactions).RolledBack);
        }

        [Fact]
        public async Task Adjust_SameLineIdSentTwice_SecondCopyIsANewLine()
        {
            var h = new Harness();
            h.SetLines(BaseLines());
            var dongs = BaseDongs().Append(Dong(3, "Rác", 50_000m, 2m)).ToArray();

            var result = await h.AdjustLines(dongs);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, h.Invoice.ChiTietHoaDonDichVus.Count(l => l.TenDichVu == "Rác"));
            Assert.Equal(1_770_000m, h.Invoice.TongTien);
            Assert.Contains("(thêm 1 dòng)", h.Invoice.LichSuTrangThaiHoaDons.Last().LyDo);
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
        }

        [Fact]
        public async Task Adjust_Sent_WithLines_ReplacementGetsSubmittedLines_AndKeepsProratedAmount()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.SetLines(BaseLines());
            // Dòng do lõi tạo nháp tính lại từ kỳ: sẽ bị thay bằng danh sách người dùng đã chốt.
            var replacement = h.SetupReplacement(9_999m, new List<ChiTietHoaDon> { new() { ChiTietHoaDonId = 77, HoaDonId = 501, TenDichVu = "Tiền phòng", DonGia = 3_000_000m, SoLuong = 1, TongTien = 9_999m } });
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
        public async Task Adjust_Sent_ReadingsOnly_WithSameLines_ReplacementKeepsManualLine()
        {
            // Hóa đơn cũ có dòng nhân viên thêm tay; lõi tạo nháp chỉ dựng lại tiền phòng.
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.SetLines(Line(1, "Tiền phòng", 1_000_000m, 1m, 1_000_000m), Line(2, "Phí sửa khóa", 50_000m, 1m, 50_000m));
            var replacement = h.SetupReplacement(1_070_000m);

            // Hóa đơn cũ chưa có dòng điện; màn điều chỉnh đã thêm dòng điện tính lại theo chỉ số mới.
            var result = await h.AdjustLines(new[]
            {
                Dong(1, "Tiền phòng", 1_000_000m, 1m),
                Dong(2, "Phí sửa khóa", 50_000m, 1m),
                Dong(null, "Tiền điện (100 → 130)", 3_500m, 30m)
            }, dien: 130m, nuoc: 60m);

            Assert.True(result.Success, result.Message);
            Assert.Single(h.Meter.AdjustCalls);
            var lines = replacement.ChiTietHoaDonDichVus.Where(l => !l.IsDeleted).ToList();
            Assert.Equal(new[] { "Tiền phòng", "Phí sửa khóa", "Tiền điện (100 → 130)" }, lines.Select(l => l.TenDichVu));
            Assert.Equal(1_155_000m, replacement.TongTien);
            Assert.Equal(1_155_000m, result.Data!.TongTienMoi);
        }

        // ---------- Dòng điện/nước chưa tính lại theo chỉ số mới ----------

        private const string DongDienNuocCu = "Dòng tiền điện/nước chưa được tính lại theo chỉ số mới, vui lòng tải lại.";

        [Fact]
        public async Task Adjust_Sent_NewReading_WithStaleElectricityLine_IsRejected_BeforeCancelling()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.SetLines(BaseLines());
            h.SetupReplacement();

            // Màn hình chưa tính lại được: dòng điện vẫn là "100 → 120" nhưng chỉ số điện mới là 130.
            var result = await h.AdjustLines(BaseDongs(), dien: 130m, nuoc: 60m);

            Assert.False(result.Success);
            Assert.Equal(DongDienNuocCu, result.Message);
            var tx = Assert.Single(h.Uow.Transactions);
            Assert.True(tx.RolledBack);
            Assert.False(tx.Committed);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, h.Invoice.TrangThaiPhatHanh);
            Assert.False(h.Invoice.IsDeleted);
            Assert.Empty(h.Invoice.LichSuTrangThaiHoaDons);
            Assert.Empty(h.IssuanceService.CreateInCurrentTransactionCalls);
            Assert.Empty(h.Meter.AdjustCalls);
            Assert.Equal(120m, h.Invoice.DichVuDienNuocCuaPhong!.ChiSoDienMoi);
            Assert.Empty(h.ThongBao.Sent);
        }

        [Fact]
        public async Task Adjust_ChoDuyet_NewReading_WithStaleElectricityLine_IsRejected_AndInvoiceUnchanged()
        {
            var h = new Harness();
            h.SetLines(BaseLines());
            var dongs = BaseDongs();
            dongs[2] = Dong(3, "Rác", 50_000m, 3m);   // sửa dòng khác, dòng điện vẫn là số cũ

            var result = await h.AdjustLines(dongs, dien: 130m, nuoc: 60m);

            Assert.False(result.Success);
            Assert.Equal(DongDienNuocCu, result.Message);
            Assert.True(Assert.Single(h.Uow.Transactions).RolledBack);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, h.Invoice.TrangThaiPhatHanh);
            Assert.Empty(h.Invoice.LichSuTrangThaiHoaDons);
            Assert.Empty(h.Meter.AdjustCalls);
            Assert.Equal(1_670_000m, h.Invoice.TongTien);
            Assert.Equal(new[] { 1, 2, 3 }, h.Invoice.ChiTietHoaDonDichVus.Select(l => l.ChiTietHoaDonId).ToArray());
        }

        [Fact]
        public async Task Adjust_SecondManualElectricityLine_ResentUnchanged_IsAllowed_OnlyFirstLineIsChecked()
        {
            // Dòng điện hệ thống (id 2) và dòng truy thu nhập tay (id 5, cũng nhận là điện theo tên): màn hình chỉ tính lại dòng đầu.
            ChiTietHoaDon[] Lines() => BaseLines().Append(Line(5, "Tiền điện truy thu tháng 9", 3_500m, 10m, 35_000m)).ToArray();
            var ok = new Harness();
            ok.SetLines(Lines());
            var stale = new Harness();
            stale.SetLines(Lines());

            var dongsOk = BaseDongs().Append(Dong(5, "Tiền điện truy thu tháng 9", 3_500m, 10m)).ToArray();
            dongsOk[1] = Dong(2, "Tiền điện (100 → 130)", 3_500m, 30m);
            var resultOk = await ok.AdjustLines(dongsOk, dien: 130m, nuoc: 60m);
            // Đối chứng: dòng đầu vẫn là số cũ thì vẫn chặn.
            var resultStale = await stale.AdjustLines(BaseDongs().Append(Dong(5, "Tiền điện truy thu tháng 9", 3_500m, 10m)).ToArray(), dien: 130m, nuoc: 60m);

            Assert.True(resultOk.Success, resultOk.Message);
            Assert.Single(ok.Meter.AdjustCalls);
            Assert.Equal(1_500_000m + 105_000m + 100_000m + 35_000m, ok.Invoice.TongTien);
            Assert.False(resultStale.Success);
            Assert.Equal(DongDienNuocCu, resultStale.Message);
            Assert.Empty(stale.Meter.AdjustCalls);
        }

        [Fact]
        public async Task Adjust_NoWaterLine_WaterReadingIncreased_WithoutNewWaterLine_IsRejected()
        {
            var h = new Harness();
            h.SetLines(BaseLines());   // không có dòng nước

            var result = await h.AdjustLines(BaseDongs(), dien: 120m, nuoc: 70m);

            Assert.False(result.Success);
            Assert.Equal(DongDienNuocCu, result.Message);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, h.Invoice.TrangThaiPhatHanh);
            Assert.Empty(h.Meter.AdjustCalls);
        }

        [Fact]
        public async Task Adjust_NoWaterLine_WaterReadingIncreased_WithNewWaterLine_Succeeds()
        {
            var h = new Harness();
            h.SetLines(BaseLines());
            var dongs = BaseDongs().Append(Dong(null, "Tiền nước (50 → 70)", 15_000m, 20m)).ToArray();

            var result = await h.AdjustLines(dongs, dien: 120m, nuoc: 70m);

            Assert.True(result.Success, result.Message);
            Assert.Single(h.Meter.AdjustCalls);
            Assert.Equal(1_970_000m, h.Invoice.TongTien);
        }

        [Fact]
        public async Task Adjust_Sent_NewReading_WithRecalculatedElectricityLine_Succeeds()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            h.SetLines(BaseLines());
            var replacement = h.SetupReplacement();
            var dongs = BaseDongs();
            dongs[1] = Dong(2, "Tiền điện (100 → 130)", 3_500m, 30m);

            var result = await h.AdjustLines(dongs, dien: 130m, nuoc: 60m);

            Assert.True(result.Success, result.Message);
            Assert.True(result.Data!.DaTaoHoaDonThayThe);
            Assert.Single(h.Meter.AdjustCalls);
            Assert.True(h.Invoice.IsDeleted);
            Assert.Equal(105_000m, replacement.ChiTietHoaDonDichVus.Single(l => l.TenDichVu == "Tiền điện (100 → 130)").TongTien);
            Assert.Equal(1_705_000m, replacement.TongTien);
        }

        [Fact]
        public async Task Adjust_Sent_LinesOnly_StillCancelsOldInvoice_WithoutCallingMeterCore()
        {
            var h = new Harness(TrangThaiPhatHanhHoaDon.DaGui);
            var replacement = h.SetupReplacement(1_000_000m);

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
        public async Task PreviewDieuChinh_CoCanhBao()
        {
            var h = new Harness();
            h.HistoryStore.History = new QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence.MeterConsumptionHistory(new[] { 38m, 40m, 40m, 42m, 45m, 39m }.Select(k => new QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence.MeterConsumption(k, 0m)).ToList(), 31, 31, 10);

            var batThuong = await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 191m, 50m);
            var binhThuong = await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 190m, 50m);

            Assert.True(batThuong.Success, batThuong.Message);
            var canhBao = Assert.Single(batThuong.Data!.CanhBaos);
            Assert.Contains("Điện", canhBao);
            Assert.NotNull(batThuong.Data.Dien);
            Assert.Empty(binhThuong.Data!.CanhBaos);
        }

        [Fact]
        public async Task Preview_HistoryStoreThrows_StillReturnsLines_WithoutWarnings()
        {
            var h = new Harness();
            h.HistoryStore.ThrowOnGet = new InvalidOperationException("db down");

            var result = await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 191m, 50m);

            Assert.True(result.Success, result.Message);
            Assert.NotNull(result.Data!.Dien);
            Assert.Empty(result.Data.CanhBaos);
            Assert.Single(h.Logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(h.Logger.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact]
        public async Task Preview_NoConsumption_ReturnsNoLines()
        {
            var h = new Harness();

            var result = await h.Service.PreviewMeterLinesAsync(Actor, HoaDonId, 100m, 50m);

            Assert.True(result.Success, result.Message);
            Assert.Null(result.Data!.Dien);
            Assert.Null(result.Data.Nuoc);
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
