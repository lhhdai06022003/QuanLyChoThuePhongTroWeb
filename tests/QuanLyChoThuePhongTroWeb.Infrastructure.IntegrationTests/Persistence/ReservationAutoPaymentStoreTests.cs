using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.ReservationSettlement;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Reservations;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Ghi tiền giữ chỗ do hệ thống tự nhận (spec giữ chỗ §6, §7): mã TTPTGC{id}, cộng dồn, chuyển trạng thái, chống trùng,
    // chặn mã nhân viên nhập tay trùng khóa hệ thống. Mỗi test tự dựng dữ liệu riêng và dọn lại.
    [Collection("PostgreSqlCollection")]
    public class ReservationAutoPaymentStoreTests : IAsyncLifetime
    {
        private const decimal HoldAmount = 1_000_000m;

        private readonly PostgreSqlFixture _fixture;
        private readonly List<int> _branchIds = new();
        private readonly List<int> _userIds = new();
        private readonly List<int> _roomIds = new();

        public ReservationAutoPaymentStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            await using var db = _fixture.CreateDbContext();
            var holdIds = await db.YeuCauGiuChos.Where(h => _roomIds.Contains(h.PhongTroId))
                .Select(h => h.YeuCauGiuChoId).ToListAsync();
            var paymentIds = await db.YeuCauThanhToanGiuChos.Where(p => holdIds.Contains(p.YeuCauGiuChoId))
                .Select(p => p.YeuCauThanhToanGiuChoId).ToListAsync();
            await db.GiaoDichGiuChos.Where(g => paymentIds.Contains(g.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
            await db.MinhChungThanhToanGiuChos.Where(m => paymentIds.Contains(m.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
            await db.LichSuTrangThaiYeuCauThanhToanGiuChos.Where(l => paymentIds.Contains(l.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
            await db.YeuCauThanhToanGiuChos.Where(p => paymentIds.Contains(p.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
            await db.LichSuTrangThaiYeuCauGiuChos.Where(l => holdIds.Contains(l.YeuCauGiuChoId)).ExecuteDeleteAsync();
            await db.YeuCauGiuChos.Where(h => holdIds.Contains(h.YeuCauGiuChoId)).ExecuteDeleteAsync();
            await db.PhongTros.Where(p => _roomIds.Contains(p.PhongTroId)).ExecuteDeleteAsync();
            await db.KhachVangLais.Where(k => _userIds.Contains(k.NguoiDungId)).ExecuteDeleteAsync();
            await db.NguoiDungs.Where(u => _userIds.Contains(u.NguoiDungId)).ExecuteDeleteAsync();
            await db.ChiNhanhs.Where(c => _branchIds.Contains(c.ChiNhanhId)).ExecuteDeleteAsync();
        }

        private sealed record Seed(int HoldId, int PaymentId, int StaffId, string Code, DateTime Now, DateTime Deadline);

        // Giữ chỗ ChoThanhToan hạn thanh toán sau 1 ngày, đã duyệt qua ReservationStore.ApproveAsync (nên có mã TTPTGC{id}).
        private async Task<Seed> SeedApprovedHoldAsync()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var now = DateTime.UtcNow;
            int holdId;
            int staffId;
            await using (var db = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh
                {
                    MaChiNhanh = "A" + suffix[..7], TenChiNhanh = "Auto " + suffix,
                    DiaChi = "A", MoTa = "A", SoDienThoai = "0900000001"
                };
                var guest = new NguoiDung { TenDangNhap = "ag_" + suffix, MatKhauHash = "test", Role = Role.KhachVangLai };
                var staff = new NguoiDung { TenDangNhap = "as_" + suffix, MatKhauHash = "test", Role = Role.NhanVien };
                var profile = new KhachVangLai
                {
                    NguoiDung = guest, HoTen = "Khách tự động", SoDienThoai = "0912345678", Email = "auto@example.com"
                };
                db.ChiNhanhs.Add(branch);
                db.NguoiDungs.AddRange(guest, staff);
                db.KhachVangLais.Add(profile);
                await db.SaveChangesAsync();
                var room = new PhongTro
                {
                    ChiNhanhId = branch.ChiNhanhId, SoPhong = "A" + suffix, GiaThue = 1_000_000m, DienTich = 20,
                    MoTa = "Test", TrangThai = TrangThaiPhong.Trong, DuocDangTin = true
                };
                db.PhongTros.Add(room);
                await db.SaveChangesAsync();
                _branchIds.Add(branch.ChiNhanhId);
                _userIds.AddRange(new[] { guest.NguoiDungId, staff.NguoiDungId });
                _roomIds.Add(room.PhongTroId);
                staffId = staff.NguoiDungId;

                var store = new ReservationStore(db);
                var created = await store.CreateAsync(guest.NguoiDungId, room.PhongTroId, null, now);
                Assert.Equal(ReservationError.None, created.Error);
                holdId = created.Id!.Value;
            }

            var deadline = now.AddDays(1);
            await using (var db = _fixture.CreateDbContext())
            {
                Assert.Equal(ReservationError.None, await new ReservationStore(db).ApproveAsync(
                    staffId, holdId, HoldAmount, deadline, now.AddDays(2), now));
            }

            await using var read = _fixture.CreateDbContext();
            var payment = await read.YeuCauThanhToanGiuChos.AsNoTracking().SingleAsync(p => p.YeuCauGiuChoId == holdId);
            return new Seed(holdId, payment.YeuCauThanhToanGiuChoId, staffId, ReservationTransferHandler.TransferCode(holdId), now, payment.HanThanhToan);
        }

        private async Task<SystemReservationRecordResult> RecordAsync(
            Seed seed, string key, decimal amount, DateTime? receivedAt = null, string? code = null)
        {
            await using var db = _fixture.CreateDbContext();
            return await new ReservationPaymentStore(db).RecordSystemTransferAsync(
                code ?? seed.Code, key, amount, receivedAt ?? seed.Now.AddMinutes(1), "Ghi nhận tự động " + key, seed.Now.AddMinutes(2));
        }

        private async Task<List<GiaoDichGiuCho>> RowsAsync(Seed seed)
        {
            await using var db = _fixture.CreateDbContext();
            return await db.GiaoDichGiuChos.AsNoTracking().Where(g => g.YeuCauThanhToanGiuChoId == seed.PaymentId)
                .OrderBy(g => g.GiaoDichGiuChoId).ToListAsync();
        }

        private async Task<(TrangThaiYeuCauGiuCho Hold, TrangThaiYeuCauThanhToan Payment)> StatesAsync(Seed seed)
        {
            await using var db = _fixture.CreateDbContext();
            var hold = await db.YeuCauGiuChos.AsNoTracking().SingleAsync(h => h.YeuCauGiuChoId == seed.HoldId);
            var payment = await db.YeuCauThanhToanGiuChos.AsNoTracking().SingleAsync(p => p.YeuCauThanhToanGiuChoId == seed.PaymentId);
            return (hold.TrangThai, payment.TrangThai);
        }

        [Fact]
        public async Task Approve_UsesThePaymentCodeOfTheReservation_ForBothColumns()
        {
            var seed = await SeedApprovedHoldAsync();

            await using var db = _fixture.CreateDbContext();
            var payment = await db.YeuCauThanhToanGiuChos.AsNoTracking().SingleAsync(p => p.YeuCauThanhToanGiuChoId == seed.PaymentId);

            Assert.Equal($"TTPTGC{seed.HoldId}", payment.MaYeuCau);
            Assert.Equal(payment.MaYeuCau, payment.NoiDungChuyenKhoan);
        }

        [Fact]
        public async Task FullAmountOnTime_RecordsASystemRow_AndTheHoldBecomesEffective()
        {
            var seed = await SeedApprovedHoldAsync();
            var receivedAt = seed.Now.AddMinutes(1);

            var result = await RecordAsync(seed, "SEPAY-REF1", HoldAmount, receivedAt);

            Assert.Equal(SystemReservationRecordStatus.Recorded, result.Status);
            Assert.True(result.VuaCoHieuLuc);
            Assert.False(result.CanHoanTien);
            Assert.Equal(0m, result.DaNhanTruoc);
            Assert.Equal(HoldAmount, result.DaNhanSau);
            Assert.Equal(HoldAmount, result.SoTienGiuCho);
            Assert.Equal("DangGiuCho", result.TrangThaiGiuCho);
            Assert.Equal(seed.HoldId, result.YeuCauGiuChoId);
            Assert.Equal("Khách tự động", result.TenKhach);
            Assert.Equal("auto@example.com", result.EmailKhach);
            Assert.Equal(seed.Deadline, result.HanThanhToanUtc);
            Assert.False(result.ConMinhChungChoXacNhan);

            var row = Assert.Single(await RowsAsync(seed));
            Assert.Null(row.NguoiXacNhanId);
            Assert.Null(row.MinhChungThanhToanGiuChoId);
            Assert.Equal(PhuongThucThanhToan.ChuyenKhoan, row.PhuongThuc);
            Assert.Null(row.GhiChu);
            Assert.Equal("SEPAY-REF1", row.MaGiaoDich);
            Assert.Equal(HoldAmount, row.SoTienThucNhan);
            Assert.Equal(receivedAt, row.NgayThucNhan, TimeSpan.FromMilliseconds(1));
            Assert.Equal(result.GiaoDichGiuChoId, row.GiaoDichGiuChoId);

            var states = await StatesAsync(seed);
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, states.Hold);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, states.Payment);

            await using var db = _fixture.CreateDbContext();
            var holdHistory = await db.LichSuTrangThaiYeuCauGiuChos.AsNoTracking()
                .Where(l => l.YeuCauGiuChoId == seed.HoldId && l.TrangThaiSau == TrangThaiYeuCauGiuCho.DangGiuCho).SingleAsync();
            Assert.Equal(LoaiTacNhan.HeThong, holdHistory.LoaiTacNhan);
            Assert.Null(holdHistory.NguoiThucHienId);
            Assert.Equal("Ghi nhận tự động SEPAY-REF1", holdHistory.LyDo);
            var paymentHistory = await db.LichSuTrangThaiYeuCauThanhToanGiuChos.AsNoTracking()
                .Where(l => l.YeuCauThanhToanGiuChoId == seed.PaymentId && l.TrangThaiSau == TrangThaiYeuCauThanhToan.DaHoanTat).SingleAsync();
            Assert.Equal(LoaiTacNhan.HeThong, paymentHistory.LoaiTacNhan);
            Assert.Null(paymentHistory.NguoiThucHienId);
            Assert.Equal("Ghi nhận tự động SEPAY-REF1", paymentHistory.LyDo);
        }

        [Fact]
        public async Task PartialThenTheRest_MovesThroughWaitingToEffective()
        {
            var seed = await SeedApprovedHoldAsync();

            var first = await RecordAsync(seed, "VNPAY-T1", 400_000m);
            var afterFirst = await StatesAsync(seed);
            var second = await RecordAsync(seed, "VNPAY-T2", 600_000m);
            var afterSecond = await StatesAsync(seed);

            Assert.False(first.VuaCoHieuLuc);
            Assert.Equal("ChoXacNhanTien", first.TrangThaiGiuCho);
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoXacNhanTien, afterFirst.Hold);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, afterFirst.Payment);
            Assert.True(second.VuaCoHieuLuc);
            Assert.Equal(400_000m, second.DaNhanTruoc);
            Assert.Equal(HoldAmount, second.DaNhanSau);
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, afterSecond.Hold);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, afterSecond.Payment);
            Assert.Equal(2, (await RowsAsync(seed)).Count);
        }

        [Fact]
        public async Task Overpay_RecordsTheFullAmountReceived()
        {
            var seed = await SeedApprovedHoldAsync();

            var result = await RecordAsync(seed, "SEPAY-OVER", 1_300_000m);

            Assert.Equal(SystemReservationRecordStatus.Recorded, result.Status);
            Assert.Equal(1_300_000m, result.DaNhanSau);
            Assert.True(result.DaNhanSau > result.SoTienGiuCho);
            Assert.False(result.CanHoanTien);
            Assert.Equal(1_300_000m, Assert.Single(await RowsAsync(seed)).SoTienThucNhan);
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, (await StatesAsync(seed)).Hold);
        }

        [Fact]
        public async Task AfterTheDeadline_RecordsALateRefund_AndExpiresTheHoldAndTheRequest()
        {
            var seed = await SeedApprovedHoldAsync();

            var result = await RecordAsync(seed, "SEPAY-LATE", HoldAmount, seed.Deadline.AddMinutes(5));

            Assert.Equal(SystemReservationRecordStatus.Recorded, result.Status);
            Assert.True(result.CanHoanTien);
            Assert.False(result.VuaCoHieuLuc);
            Assert.Equal("HetHan", result.TrangThaiGiuCho);
            Assert.Equal(ReservationPaymentMarkers.LateRefundRequired, Assert.Single(await RowsAsync(seed)).GhiChu);
            var states = await StatesAsync(seed);
            Assert.Equal(TrangThaiYeuCauGiuCho.HetHan, states.Hold);
            Assert.Equal(TrangThaiYeuCauThanhToan.HetHan, states.Payment);
        }

        [Fact]
        public async Task OnACancelledHold_RecordsALateRefund_AndLeavesTheStatusAlone()
        {
            var seed = await SeedApprovedHoldAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                await db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == seed.HoldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(h => h.TrangThai, TrangThaiYeuCauGiuCho.DaHuy));
            }

            var result = await RecordAsync(seed, "VNPAY-CANCELLED", HoldAmount);

            Assert.Equal(SystemReservationRecordStatus.Recorded, result.Status);
            Assert.True(result.CanHoanTien);
            Assert.Equal("DaHuy", result.TrangThaiGiuCho);
            Assert.Equal(ReservationPaymentMarkers.LateRefundRequired, Assert.Single(await RowsAsync(seed)).GhiChu);
            Assert.Equal(TrangThaiYeuCauGiuCho.DaHuy, (await StatesAsync(seed)).Hold);
        }

        [Fact]
        public async Task SameKeyTwice_IsADuplicate_AndStaysOneRow()
        {
            var seed = await SeedApprovedHoldAsync();

            var first = await RecordAsync(seed, "SEPAY-DUP", 400_000m);
            var second = await RecordAsync(seed, "SEPAY-DUP", 400_000m);

            Assert.Equal(SystemReservationRecordStatus.Recorded, first.Status);
            Assert.Equal(SystemReservationRecordStatus.Duplicate, second.Status);
            Assert.Single(await RowsAsync(seed));
        }

        [Fact]
        public async Task LegacyCodeOrUnknownCode_IsNotFound_AndExistsReflectsTheCode()
        {
            var seed = await SeedApprovedHoldAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                await db.YeuCauThanhToanGiuChos.Where(p => p.YeuCauThanhToanGiuChoId == seed.PaymentId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.MaYeuCau, "GC" + seed.HoldId + "OLD")
                        .SetProperty(p => p.NoiDungChuyenKhoan, "GC" + seed.HoldId + "OLD"));
            }

            var result = await RecordAsync(seed, "SEPAY-NF", HoldAmount);

            Assert.Equal(SystemReservationRecordStatus.NotFound, result.Status);
            Assert.Empty(await RowsAsync(seed));
            await using var db2 = _fixture.CreateDbContext();
            var store = new ReservationPaymentStore(db2);
            Assert.False(await store.PaymentRequestExistsAsync(seed.Code));
            Assert.True(await store.PaymentRequestExistsAsync("GC" + seed.HoldId + "OLD"));
        }

        [Fact]
        public async Task TwoContextsWithTheSameKey_WriteOneRow()
        {
            var seed = await SeedApprovedHoldAsync();

            var results = await Task.WhenAll(
                RecordAsync(seed, "SEPAY-PAR", HoldAmount),
                RecordAsync(seed, "SEPAY-PAR", HoldAmount));

            var recorded = results.Count(r => r.Status == SystemReservationRecordStatus.Recorded);
            Assert.Equal(1, recorded);
            Assert.All(results.Where(r => r.Status != SystemReservationRecordStatus.Recorded),
                r => Assert.True(r.Status is SystemReservationRecordStatus.Duplicate or SystemReservationRecordStatus.Conflict));
            Assert.Single(await RowsAsync(seed));
        }

        [Fact]
        public async Task AutomaticAndManualInParallel_KeepRowsAndStatusConsistent()
        {
            var seed = await SeedApprovedHoldAsync();

            // Luồng tay bắt cả lỗi serialize bị EF bọc trong InvalidOperationException: thua thì trả Conflict, không ném ra.
            async Task<PaymentError> ManualAsync()
            {
                await using var db = _fixture.CreateDbContext();
                return await new ReservationPaymentStore(db).ConfirmReceivedAsync(
                    seed.StaffId, seed.PaymentId, null, "MANUAL-PAR", 400_000m, seed.Now.AddMinutes(1), seed.Now.AddMinutes(2));
            }

            var autoTask = RecordAsync(seed, "SEPAY-PAR2", 600_000m);
            var manualTask = ManualAsync();
            await Task.WhenAll(autoTask, manualTask);

            var autoResult = await autoTask;
            var manualResult = await manualTask;
            Assert.True(manualResult is PaymentError.None or PaymentError.Conflict);
            var autoOk = autoResult.Status == SystemReservationRecordStatus.Recorded;
            var manualOk = manualResult == PaymentError.None;
            Assert.True(autoOk || manualOk);
            var rows = await RowsAsync(seed);
            Assert.Equal((autoOk ? 1 : 0) + (manualOk ? 1 : 0), rows.Count);
            var total = rows.Sum(r => r.SoTienThucNhan);
            var states = await StatesAsync(seed);
            Assert.Equal(total >= HoldAmount ? TrangThaiYeuCauGiuCho.DangGiuCho : TrangThaiYeuCauGiuCho.ChoXacNhanTien, states.Hold);
        }

        [Theory]
        [InlineData("SEPAY-REF1", "ref1")]
        [InlineData("VNPAY-14123456", "14123456")]
        [InlineData("SEPAY-FT26ABC", " ft26 abc ")]
        public async Task ManualConfirmWithACodeTheSystemAlreadyRecorded_IsRefused(string systemKey, string manualCode)
        {
            var seed = await SeedApprovedHoldAsync();
            await RecordAsync(seed, systemKey, 400_000m);

            await using var db = _fixture.CreateDbContext();
            var error = await new ReservationPaymentStore(db).ConfirmReceivedAsync(
                seed.StaffId, seed.PaymentId, null, manualCode.Trim(), 400_000m, seed.Now.AddMinutes(1), seed.Now.AddMinutes(2));

            Assert.Equal(PaymentError.SystemRecorded, error);
            Assert.Single(await RowsAsync(seed));
        }

        [Fact]
        public async Task ManualConfirmWithAnotherCode_StillWorks()
        {
            var seed = await SeedApprovedHoldAsync();
            await RecordAsync(seed, "SEPAY-REF1", 400_000m);

            await using var db = _fixture.CreateDbContext();
            var error = await new ReservationPaymentStore(db).ConfirmReceivedAsync(
                seed.StaffId, seed.PaymentId, null, "OTHER-REF", 600_000m, seed.Now.AddMinutes(1), seed.Now.AddMinutes(2));

            Assert.Equal(PaymentError.None, error);
            Assert.Equal(2, (await RowsAsync(seed)).Count);
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, (await StatesAsync(seed)).Hold);
        }

        // Khuyến nghị 1 của reviewer: nhân viên xác nhận tay trước bằng mã gốc, sau đó SePay/VNPay gửi lại đúng giao dịch đó.
        [Theory]
        [InlineData("ft26 abc", "SEPAY-FT26ABC")]
        [InlineData("FT26ABC", "SEPAY-FT26ABC")]
        [InlineData("14123456", "VNPAY-14123456")]
        public async Task SystemTransferAfterAManualConfirmWithTheSameRawCode_IsADuplicate(string manualCode, string systemKey)
        {
            var seed = await SeedApprovedHoldAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                Assert.Equal(PaymentError.None, await new ReservationPaymentStore(db).ConfirmReceivedAsync(
                    seed.StaffId, seed.PaymentId, null, manualCode, 400_000m, seed.Now.AddMinutes(1), seed.Now.AddMinutes(2)));
            }

            var result = await RecordAsync(seed, systemKey, 400_000m);

            Assert.Equal(SystemReservationRecordStatus.Duplicate, result.Status);
            var row = Assert.Single(await RowsAsync(seed));
            Assert.Equal(manualCode, row.MaGiaoDich);
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoXacNhanTien, (await StatesAsync(seed)).Hold);
        }

        [Fact]
        public async Task SameRawCodeConfirmedManuallyOnAnotherReservation_DoesNotBlockTheSystemRow()
        {
            var other = await SeedApprovedHoldAsync();
            var seed = await SeedApprovedHoldAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                Assert.Equal(PaymentError.None, await new ReservationPaymentStore(db).ConfirmReceivedAsync(
                    other.StaffId, other.PaymentId, null, "FT26XYZ", 400_000m, other.Now.AddMinutes(1), other.Now.AddMinutes(2)));
            }

            var result = await RecordAsync(seed, "SEPAY-FT26XYZ", 400_000m);

            Assert.Equal(SystemReservationRecordStatus.Recorded, result.Status);
            Assert.Single(await RowsAsync(seed));
        }

        [Fact]
        public async Task SePayIdKey_HasNoRawCode_AndIsNotMatchedAgainstManualRows()
        {
            var seed = await SeedApprovedHoldAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                Assert.Equal(PaymentError.None, await new ReservationPaymentStore(db).ConfirmReceivedAsync(
                    seed.StaffId, seed.PaymentId, null, "ID-777", 400_000m, seed.Now.AddMinutes(1), seed.Now.AddMinutes(2)));
            }

            var result = await RecordAsync(seed, "SEPAY-ID-777", 400_000m);

            Assert.Equal(SystemReservationRecordStatus.Recorded, result.Status);
            Assert.Equal(2, (await RowsAsync(seed)).Count);
        }

        [Fact]
        public async Task PendingEvidence_IsFlagged_AndLeftUntouched()
        {
            var seed = await SeedApprovedHoldAsync();
            int evidenceId;
            await using (var db = _fixture.CreateDbContext())
            {
                var evidence = new MinhChungThanhToanGiuCho
                {
                    YeuCauThanhToanGiuChoId = seed.PaymentId,
                    UrlHinhAnh = "https://res.cloudinary.com/demo/proof.jpg",
                    PublicIdHinhAnh = "hold-payment-proofs/x",
                    SoTienKhaiBao = HoldAmount,
                    NgayChuyenTien = seed.Now,
                    TrangThai = TrangThaiMinhChungThanhToan.ChoXacNhan,
                    NgayTao = seed.Now
                };
                db.MinhChungThanhToanGiuChos.Add(evidence);
                await db.SaveChangesAsync();
                evidenceId = evidence.MinhChungThanhToanGiuChoId;
            }

            var result = await RecordAsync(seed, "SEPAY-EV", HoldAmount);

            Assert.True(result.ConMinhChungChoXacNhan);
            await using var read = _fixture.CreateDbContext();
            var evidenceAfter = await read.MinhChungThanhToanGiuChos.AsNoTracking().SingleAsync(e => e.MinhChungThanhToanGiuChoId == evidenceId);
            Assert.Equal(TrangThaiMinhChungThanhToan.ChoXacNhan, evidenceAfter.TrangThai);
            Assert.Null(evidenceAfter.NguoiDoiChieuId);
        }

        [Fact]
        public async Task Settlement_CountsTheAutomaticRows_AndTheLateOnesAsMandatoryRefund()
        {
            var onTime = await SeedApprovedHoldAsync();
            await RecordAsync(onTime, "SEPAY-ST1", HoldAmount);
            var late = await SeedApprovedHoldAsync();
            await RecordAsync(late, "SEPAY-ST2", 700_000m, late.Deadline.AddMinutes(5));

            await using var db = _fixture.CreateDbContext();
            var settlement = new ReservationSettlementStore(db);
            var onTimeSummary = await settlement.GetAsync(onTime.HoldId);
            var lateSummary = await settlement.GetAsync(late.HoldId);

            Assert.NotNull(onTimeSummary);
            Assert.Equal(HoldAmount, onTimeSummary.ConfirmedAmount);
            Assert.Equal(0m, onTimeSummary.MandatoryRefundAmount);
            Assert.NotNull(lateSummary);
            Assert.Equal(700_000m, lateSummary.ConfirmedAmount);
            Assert.Equal(700_000m, lateSummary.MandatoryRefundAmount);
        }
    }
}
