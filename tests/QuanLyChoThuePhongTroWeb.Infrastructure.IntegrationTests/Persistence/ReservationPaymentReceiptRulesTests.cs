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
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Reservations;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Bổ sung của Tester cho đợt 3 phần 1, cùng một bộ quy tắc "ghi một khoản tiền giữ chỗ" nhưng soi từ hai phía:
    //  - luồng nhân viên xác nhận tay (ConfirmReceivedAsync) phải ghi y như trước khi tách hàm dùng chung;
    //  - luồng hệ thống tự ghi (RecordSystemTransferAsync) ở các ranh giới đúng hạn / trạng thái giữ chỗ.
    // Mỗi test tự dựng dữ liệu riêng qua ReservationStore.ApproveAsync và dọn lại.
    [Collection("PostgreSqlCollection")]
    public class ReservationPaymentReceiptRulesTests : IAsyncLifetime
    {
        private const decimal HoldAmount = 1_000_000m;
        private const string MandatoryRefund = ReservationPaymentMarkers.LateRefundRequired;

        private readonly PostgreSqlFixture _fixture;
        private readonly List<int> _branchIds = new();
        private readonly List<int> _userIds = new();
        private readonly List<int> _roomIds = new();

        public ReservationPaymentReceiptRulesTests(PostgreSqlFixture fixture)
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

        private sealed record Seed(int HoldId, int PaymentId, int StaffId, int RoomId, int BranchId, string Code, DateTime Now, DateTime Deadline);

        private async Task<Seed> SeedApprovedHoldAsync()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var now = DateTime.UtcNow;
            int holdId, staffId, roomId, branchId;
            await using (var db = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh
                {
                    MaChiNhanh = "R" + suffix[..7], TenChiNhanh = "Rules " + suffix,
                    DiaChi = "A", MoTa = "A", SoDienThoai = "0900000002"
                };
                var guest = new NguoiDung { TenDangNhap = "rg_" + suffix, MatKhauHash = "test", Role = Role.KhachVangLai };
                var staff = new NguoiDung { TenDangNhap = "rs_" + suffix, MatKhauHash = "test", Role = Role.NhanVien };
                var profile = new KhachVangLai
                {
                    NguoiDung = guest, HoTen = "Khách quy tắc", SoDienThoai = "0912345679", Email = "rules@example.com"
                };
                db.ChiNhanhs.Add(branch);
                db.NguoiDungs.AddRange(guest, staff);
                db.KhachVangLais.Add(profile);
                await db.SaveChangesAsync();
                var room = new PhongTro
                {
                    ChiNhanhId = branch.ChiNhanhId, SoPhong = "R" + suffix, GiaThue = 1_000_000m, DienTich = 20,
                    MoTa = "Test", TrangThai = TrangThaiPhong.Trong, DuocDangTin = true
                };
                db.PhongTros.Add(room);
                await db.SaveChangesAsync();
                _branchIds.Add(branch.ChiNhanhId);
                _userIds.AddRange(new[] { guest.NguoiDungId, staff.NguoiDungId });
                _roomIds.Add(room.PhongTroId);
                staffId = staff.NguoiDungId;
                roomId = room.PhongTroId;
                branchId = branch.ChiNhanhId;

                var created = await new ReservationStore(db).CreateAsync(guest.NguoiDungId, room.PhongTroId, null, now);
                Assert.Equal(ReservationError.None, created.Error);
                holdId = created.Id!.Value;
            }

            await using (var db = _fixture.CreateDbContext())
            {
                Assert.Equal(ReservationError.None, await new ReservationStore(db).ApproveAsync(
                    staffId, holdId, HoldAmount, now.AddDays(1), now.AddDays(2), now));
            }

            await using var read = _fixture.CreateDbContext();
            var payment = await read.YeuCauThanhToanGiuChos.AsNoTracking().SingleAsync(p => p.YeuCauGiuChoId == holdId);
            return new Seed(holdId, payment.YeuCauThanhToanGiuChoId, staffId, roomId, branchId,
                ReservationTransferHandler.TransferCode(holdId), now, payment.HanThanhToan);
        }

        private async Task<SystemReservationRecordResult> AutoAsync(Seed seed, string key, decimal amount, DateTime? receivedAt = null)
        {
            await using var db = _fixture.CreateDbContext();
            return await new ReservationPaymentStore(db).RecordSystemTransferAsync(
                seed.Code, key, amount, receivedAt ?? seed.Now.AddMinutes(1), "Ghi nhận tự động " + key, seed.Now.AddMinutes(2));
        }

        private async Task<PaymentError> ManualAsync(Seed seed, string code, decimal amount, DateTime? receivedAt = null,
            int? evidenceId = null, DateTime? nowOverride = null)
        {
            await using var db = _fixture.CreateDbContext();
            return await new ReservationPaymentStore(db).ConfirmReceivedAsync(
                seed.StaffId, seed.PaymentId, evidenceId, code, amount, receivedAt ?? seed.Now.AddMinutes(1), nowOverride ?? seed.Now.AddMinutes(2));
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

        private async Task<(List<LichSuTrangThaiYeuCauGiuCho> Hold, List<LichSuTrangThaiYeuCauThanhToanGiuCho> Payment)> HistoryAsync(Seed seed)
        {
            await using var db = _fixture.CreateDbContext();
            var hold = await db.LichSuTrangThaiYeuCauGiuChos.AsNoTracking()
                .Where(l => l.YeuCauGiuChoId == seed.HoldId).OrderBy(l => l.LichSuTrangThaiYeuCauGiuChoId).ToListAsync();
            var payment = await db.LichSuTrangThaiYeuCauThanhToanGiuChos.AsNoTracking()
                .Where(l => l.YeuCauThanhToanGiuChoId == seed.PaymentId).OrderBy(l => l.LichSuTrangThaiYeuCauThanhToanGiuChoId).ToListAsync();
            return (hold, payment);
        }

        // ---------- Luồng nhân viên xác nhận tay: giữ y hành vi cũ sau khi tách AddReceiptAsync ----------

        [Fact]
        public async Task Manual_FullAmountOnTime_AttributesEverythingToTheStaffMember()
        {
            var seed = await SeedApprovedHoldAsync();
            var received = seed.Now.AddMinutes(1);

            var error = await ManualAsync(seed, "BANK-FULL", HoldAmount, received);

            Assert.Equal(PaymentError.None, error);
            var row = Assert.Single(await RowsAsync(seed));
            Assert.Equal(seed.StaffId, row.NguoiXacNhanId);
            Assert.Null(row.MinhChungThanhToanGiuChoId);
            Assert.Equal("BANK-FULL", row.MaGiaoDich);
            Assert.Equal(PhuongThucThanhToan.ChuyenKhoan, row.PhuongThuc);
            Assert.Equal(received, row.NgayThucNhan, TimeSpan.FromMilliseconds(1));
            Assert.Null(row.GhiChu);
            var states = await StatesAsync(seed);
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, states.Hold);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, states.Payment);

            var history = await HistoryAsync(seed);
            var holdStep = Assert.Single(history.Hold, l => l.TrangThaiSau == TrangThaiYeuCauGiuCho.DangGiuCho);
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoThanhToan, holdStep.TrangThaiTruoc);
            Assert.Equal(LoaiTacNhan.NguoiDung, holdStep.LoaiTacNhan);
            Assert.Equal(seed.StaffId, holdStep.NguoiThucHienId);
            Assert.Null(holdStep.LyDo);
            var paymentStep = Assert.Single(history.Payment, l => l.TrangThaiSau == TrangThaiYeuCauThanhToan.DaHoanTat);
            Assert.Equal(LoaiTacNhan.NguoiDung, paymentStep.LoaiTacNhan);
            Assert.Equal(seed.StaffId, paymentStep.NguoiThucHienId);
            Assert.Null(paymentStep.LyDo);
        }

        [Fact]
        public async Task Manual_PartialThenTheRest_AccumulatesAndOnlyTheLastStepMakesTheHoldEffective()
        {
            var seed = await SeedApprovedHoldAsync();

            Assert.Equal(PaymentError.None, await ManualAsync(seed, "BANK-P1", 300_000m));
            var afterFirst = await StatesAsync(seed);
            Assert.Equal(PaymentError.None, await ManualAsync(seed, "BANK-P2", 300_000m));
            var afterSecond = await StatesAsync(seed);
            Assert.Equal(PaymentError.None, await ManualAsync(seed, "BANK-P3", 400_000m));
            var afterThird = await StatesAsync(seed);

            Assert.Equal((TrangThaiYeuCauGiuCho.ChoXacNhanTien, TrangThaiYeuCauThanhToan.DangDoiChieu), afterFirst);
            Assert.Equal((TrangThaiYeuCauGiuCho.ChoXacNhanTien, TrangThaiYeuCauThanhToan.DangDoiChieu), afterSecond);
            Assert.Equal((TrangThaiYeuCauGiuCho.DangGiuCho, TrangThaiYeuCauThanhToan.DaHoanTat), afterThird);
            Assert.Equal(3, (await RowsAsync(seed)).Count);
            Assert.All(await RowsAsync(seed), r => Assert.Equal(seed.StaffId, r.NguoiXacNhanId));
            // Mỗi bước đổi trạng thái chỉ ghi lịch sử một lần (không ghi lại khi trạng thái không đổi).
            var history = await HistoryAsync(seed);
            Assert.Equal(1, history.Hold.Count(l => l.TrangThaiSau == TrangThaiYeuCauGiuCho.ChoXacNhanTien));
            Assert.Equal(1, history.Hold.Count(l => l.TrangThaiSau == TrangThaiYeuCauGiuCho.DangGiuCho));
        }

        [Fact]
        public async Task Manual_WithEvidence_LinksAndConfirmsTheEvidence()
        {
            var seed = await SeedApprovedHoldAsync();
            int evidenceId;
            await using (var db = _fixture.CreateDbContext())
            {
                var evidence = new MinhChungThanhToanGiuCho
                {
                    YeuCauThanhToanGiuChoId = seed.PaymentId,
                    UrlHinhAnh = "https://res.cloudinary.com/demo/proof.jpg",
                    PublicIdHinhAnh = "hold-payment-proofs/m",
                    SoTienKhaiBao = HoldAmount,
                    NgayChuyenTien = seed.Now,
                    TrangThai = TrangThaiMinhChungThanhToan.ChoXacNhan,
                    NgayTao = seed.Now
                };
                db.MinhChungThanhToanGiuChos.Add(evidence);
                await db.SaveChangesAsync();
                evidenceId = evidence.MinhChungThanhToanGiuChoId;
            }

            var error = await ManualAsync(seed, "BANK-EV", HoldAmount, evidenceId: evidenceId);

            Assert.Equal(PaymentError.None, error);
            Assert.Equal(evidenceId, Assert.Single(await RowsAsync(seed)).MinhChungThanhToanGiuChoId);
            await using var read = _fixture.CreateDbContext();
            var evidenceAfter = await read.MinhChungThanhToanGiuChos.AsNoTracking().SingleAsync(e => e.MinhChungThanhToanGiuChoId == evidenceId);
            Assert.Equal(TrangThaiMinhChungThanhToan.DaXacNhan, evidenceAfter.TrangThai);
            Assert.Equal(seed.StaffId, evidenceAfter.NguoiDoiChieuId);
        }

        [Fact]
        public async Task Manual_AfterTheDeadline_FlagsALateRefund_AndExpiresWithASystemHistory()
        {
            var seed = await SeedApprovedHoldAsync();
            var late = seed.Deadline.AddMinutes(10);

            var error = await ManualAsync(seed, "BANK-LATE", HoldAmount, late, nowOverride: late);

            Assert.Equal(PaymentError.None, error);
            var row = Assert.Single(await RowsAsync(seed));
            Assert.Equal(MandatoryRefund, row.GhiChu);
            Assert.Equal(seed.StaffId, row.NguoiXacNhanId);
            var states = await StatesAsync(seed);
            Assert.Equal((TrangThaiYeuCauGiuCho.HetHan, TrangThaiYeuCauThanhToan.HetHan), states);
            var history = await HistoryAsync(seed);
            var holdStep = Assert.Single(history.Hold, l => l.TrangThaiSau == TrangThaiYeuCauGiuCho.HetHan);
            Assert.Equal(LoaiTacNhan.HeThong, holdStep.LoaiTacNhan);
            Assert.Null(holdStep.NguoiThucHienId);
            Assert.Equal("Quá hạn chuyển tiền; khoản đến muộn cần hoàn", holdStep.LyDo);
            var paymentStep = Assert.Single(history.Payment, l => l.TrangThaiSau == TrangThaiYeuCauThanhToan.HetHan);
            Assert.Equal(LoaiTacNhan.HeThong, paymentStep.LoaiTacNhan);
            Assert.Null(paymentStep.NguoiThucHienId);
        }

        [Fact]
        public async Task Manual_OnACancelledHold_FlagsALateRefund_AndLeavesTheStatesAlone()
        {
            var seed = await SeedApprovedHoldAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                await db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == seed.HoldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(h => h.TrangThai, TrangThaiYeuCauGiuCho.DaHuy));
            }

            Assert.Equal(PaymentError.None, await ManualAsync(seed, "BANK-CANCELLED", HoldAmount));

            Assert.Equal(MandatoryRefund, Assert.Single(await RowsAsync(seed)).GhiChu);
            var states = await StatesAsync(seed);
            Assert.Equal(TrangThaiYeuCauGiuCho.DaHuy, states.Hold);
            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, states.Payment);
        }

        [Fact]
        public async Task Manual_RejectionsKeepTheirErrorCodes_AndWriteNothing()
        {
            var seed = await SeedApprovedHoldAsync();
            var approvedMinute = seed.Now.AddTicks(-(seed.Now.Ticks % TimeSpan.TicksPerMinute));

            // Trước phút duyệt.
            Assert.Equal(PaymentError.ReceivedBeforeRequest,
                await ManualAsync(seed, "BANK-EARLY", HoldAmount, approvedMinute.AddSeconds(-1)));
            // Thời điểm ở tương lai quá 5 phút so với giờ xử lý.
            Assert.Equal(PaymentError.InvalidState,
                await ManualAsync(seed, "BANK-FUTURE", HoldAmount, seed.Now.AddMinutes(30), nowOverride: seed.Now));
            // Yêu cầu không tồn tại.
            await using (var db = _fixture.CreateDbContext())
            {
                Assert.Equal(PaymentError.NotFound, await new ReservationPaymentStore(db).ConfirmReceivedAsync(
                    seed.StaffId, seed.PaymentId + 900_000, null, "BANK-NF", HoldAmount, seed.Now, seed.Now));
            }
            // Minh chứng không thuộc yêu cầu.
            Assert.Equal(PaymentError.InvalidState,
                await ManualAsync(seed, "BANK-NOEV", HoldAmount, evidenceId: 900_000_000));
            Assert.Empty(await RowsAsync(seed));
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoThanhToan, (await StatesAsync(seed)).Hold);

            // Trùng mã giao dịch vẫn là DuplicateTransaction (không phải SystemRecorded).
            Assert.Equal(PaymentError.None, await ManualAsync(seed, "BANK-ONCE", 200_000m));
            Assert.Equal(PaymentError.DuplicateTransaction, await ManualAsync(seed, "BANK-ONCE", 200_000m));
            Assert.Single(await RowsAsync(seed));
        }

        // ---------- Luồng tự động: ranh giới đúng hạn (X2) và trạng thái giữ chỗ ----------

        [Fact]
        public async Task Auto_AtExactlyTheDeadline_IsStillOnTime()
        {
            var seed = await SeedApprovedHoldAsync();

            var result = await AutoAsync(seed, "SEPAY-EDGE1", HoldAmount, seed.Deadline);

            Assert.False(result.CanHoanTien);
            Assert.True(result.VuaCoHieuLuc);
            Assert.Null(Assert.Single(await RowsAsync(seed)).GhiChu);
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, (await StatesAsync(seed)).Hold);
        }

        [Fact]
        public async Task Auto_OneTickAfterTheDeadline_IsLate()
        {
            var seed = await SeedApprovedHoldAsync();

            var result = await AutoAsync(seed, "SEPAY-EDGE2", HoldAmount, seed.Deadline.AddTicks(1));

            Assert.True(result.CanHoanTien);
            Assert.False(result.VuaCoHieuLuc);
            Assert.Equal(MandatoryRefund, Assert.Single(await RowsAsync(seed)).GhiChu);
            Assert.Equal(TrangThaiYeuCauGiuCho.HetHan, (await StatesAsync(seed)).Hold);
        }

        // X2/P4: tiền chuyển đúng hạn nhưng tới sau khi ExpireUnpaidAsync đã chuyển giữ chỗ sang HetHan vẫn bị coi là phải hoàn.
        [Fact]
        public async Task Auto_TransferMadeOnTime_ButArrivingAfterTheHoldExpired_IsALateRefund()
        {
            var seed = await SeedApprovedHoldAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                await new ReservationStore(db).ExpireUnpaidAsync(seed.Deadline.AddMinutes(1));
            }
            Assert.Equal(TrangThaiYeuCauGiuCho.HetHan, (await StatesAsync(seed)).Hold);

            var result = await AutoAsync(seed, "VNPAY-ONTIME-BUT-EXPIRED", HoldAmount, seed.Deadline.AddMinutes(-5));

            Assert.Equal(SystemReservationRecordStatus.Recorded, result.Status);
            Assert.True(result.CanHoanTien);
            Assert.False(result.VuaCoHieuLuc);
            Assert.Equal("HetHan", result.TrangThaiGiuCho);
            Assert.Equal(MandatoryRefund, Assert.Single(await RowsAsync(seed)).GhiChu);
            Assert.Equal(TrangThaiYeuCauGiuCho.HetHan, (await StatesAsync(seed)).Hold);
        }

        [Theory]
        [InlineData(TrangThaiYeuCauGiuCho.TuChoi)]
        [InlineData(TrangThaiYeuCauGiuCho.DaChuyenHopDong)]
        [InlineData(TrangThaiYeuCauGiuCho.HetHan)]
        public async Task Auto_OnAHoldThatIsNoLongerActive_RecordsALateRefund_AndKeepsTheStatus(TrangThaiYeuCauGiuCho inactive)
        {
            var seed = await SeedApprovedHoldAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                await db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == seed.HoldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(h => h.TrangThai, inactive));
            }

            var result = await AutoAsync(seed, "SEPAY-INACTIVE-" + inactive, HoldAmount);

            Assert.Equal(SystemReservationRecordStatus.Recorded, result.Status);
            Assert.True(result.CanHoanTien);
            Assert.Equal(inactive.ToString(), result.TrangThaiGiuCho);
            Assert.Equal(MandatoryRefund, Assert.Single(await RowsAsync(seed)).GhiChu);
            Assert.Equal(inactive, (await StatesAsync(seed)).Hold);
            // Không phát sinh lịch sử chuyển trạng thái cho giữ chỗ đã không còn hiệu lực (ngoài các bước có từ lúc duyệt).
            var history = await HistoryAsync(seed);
            Assert.DoesNotContain(history.Hold, l => l.LyDo != null && l.LyDo.StartsWith("Ghi nhận tự động", StringComparison.Ordinal));
        }

        // Tiền tới thêm khi giữ chỗ đã hiệu lực: ghi nhận, không đổi trạng thái, không phải khoản phải hoàn.
        [Fact]
        public async Task Auto_ExtraPaymentOnAnEffectiveHold_IsRecorded_WithoutAStatusChange()
        {
            var seed = await SeedApprovedHoldAsync();
            await AutoAsync(seed, "SEPAY-BASE", HoldAmount);
            var historyBefore = await HistoryAsync(seed);

            var extra = await AutoAsync(seed, "SEPAY-EXTRA", 200_000m);

            Assert.Equal(SystemReservationRecordStatus.Recorded, extra.Status);
            Assert.False(extra.VuaCoHieuLuc);
            Assert.False(extra.CanHoanTien);
            Assert.Equal(HoldAmount, extra.DaNhanTruoc);
            Assert.Equal(HoldAmount + 200_000m, extra.DaNhanSau);
            var rows = await RowsAsync(seed);
            Assert.Equal(2, rows.Count);
            Assert.Null(rows[1].GhiChu);
            var historyAfter = await HistoryAsync(seed);
            Assert.Equal(historyBefore.Hold.Count, historyAfter.Hold.Count);
            Assert.Equal(historyBefore.Payment.Count, historyAfter.Payment.Count);
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, (await StatesAsync(seed)).Hold);
        }

        // Khoản muộn vẫn được cộng vào "đã nhận" (P5): thiếu đúng hạn rồi một khoản muộn.
        [Fact]
        public async Task Auto_LatePaymentAfterAnOnTimePartial_CountsTheEarlierMoney_AndExpiresTheHold()
        {
            var seed = await SeedApprovedHoldAsync();
            var partial = await AutoAsync(seed, "SEPAY-PART", 400_000m);
            Assert.Equal("ChoXacNhanTien", partial.TrangThaiGiuCho);

            var late = await AutoAsync(seed, "SEPAY-LATE2", 300_000m, seed.Deadline.AddMinutes(30));

            Assert.True(late.CanHoanTien);
            Assert.Equal(400_000m, late.DaNhanTruoc);
            Assert.Equal(700_000m, late.DaNhanSau);
            Assert.Equal("HetHan", late.TrangThaiGiuCho);
            var rows = await RowsAsync(seed);
            Assert.Null(rows[0].GhiChu);
            Assert.Equal(MandatoryRefund, rows[1].GhiChu);
            var states = await StatesAsync(seed);
            Assert.Equal((TrangThaiYeuCauGiuCho.HetHan, TrangThaiYeuCauThanhToan.HetHan), states);
        }

        // Phòng hoặc chi nhánh đã xóa mềm: tiền đã về nên vẫn phải ghi (kế hoạch mục 4).
        [Fact]
        public async Task Auto_StillRecords_WhenTheRoomAndBranchWereSoftDeleted()
        {
            var seed = await SeedApprovedHoldAsync();
            await using (var db = _fixture.CreateDbContext())
            {
                await db.PhongTros.Where(p => p.PhongTroId == seed.RoomId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true));
                await db.ChiNhanhs.Where(c => c.ChiNhanhId == seed.BranchId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true));
            }

            var result = await AutoAsync(seed, "SEPAY-SOFTDEL", HoldAmount);

            Assert.Equal(SystemReservationRecordStatus.Recorded, result.Status);
            Assert.Equal(seed.RoomId, result.PhongTroId);
            Assert.Equal(seed.BranchId, result.ChiNhanhId);
            Assert.Single(await RowsAsync(seed));
        }

        // Cùng khóa với khoản đã có ở một yêu cầu khác (unique toàn bảng) vẫn là Duplicate, không ghi sang yêu cầu này.
        [Fact]
        public async Task Auto_KeyAlreadyUsedByAnotherPaymentRequest_IsADuplicate()
        {
            var first = await SeedApprovedHoldAsync();
            var second = await SeedApprovedHoldAsync();
            Assert.Equal(SystemReservationRecordStatus.Recorded, (await AutoAsync(first, "SEPAY-SHARED", 400_000m)).Status);

            var result = await AutoAsync(second, "SEPAY-SHARED", 400_000m);

            Assert.Equal(SystemReservationRecordStatus.Duplicate, result.Status);
            Assert.Empty(await RowsAsync(second));
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoThanhToan, (await StatesAsync(second)).Hold);
        }

        // Mã của giữ chỗ khác không bao giờ ghi vào yêu cầu này: mỗi mã TTPTGC{id} chỉ trúng đúng một giữ chỗ.
        [Fact]
        public async Task Auto_RecordsOnlyOnThePaymentRequestOfTheCodeThatWasSent()
        {
            var a = await SeedApprovedHoldAsync();
            var b = await SeedApprovedHoldAsync();

            var result = await AutoAsync(b, "SEPAY-ONLYB", HoldAmount);

            Assert.Equal(b.HoldId, result.YeuCauGiuChoId);
            Assert.Empty(await RowsAsync(a));
            Assert.Single(await RowsAsync(b));
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoThanhToan, (await StatesAsync(a)).Hold);
        }
    }
}
