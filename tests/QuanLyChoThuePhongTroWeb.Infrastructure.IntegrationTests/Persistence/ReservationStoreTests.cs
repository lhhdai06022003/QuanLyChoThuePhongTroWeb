using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.ReservationSettlement;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

[Collection("PostgreSqlCollection")]
public sealed class ReservationStoreTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task ExpiredPaidHold_CanBeExtendedOrCancelledByStaff()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        int? branchId = null, roomId = null, userId = null, guestUserId = null,
            guestId = null, holdId = null, lateHoldId = null,
            latePaymentId = null;
        try
        {
            await using (var db = fixture.CreateDbContext())
            {
                var branch = new ChiNhanh
                {
                    MaChiNhanh = "E" + suffix[..7], TenChiNhanh = "Test " + suffix,
                    DiaChi = "A", MoTa = "A", SoDienThoai = "0900000001"
                };
                var user = new NguoiDung
                {
                    TenDangNhap = "staff_exp_" + suffix,
                    MatKhauHash = "test", Role = Role.NhanVien
                };
                var guestUser = new NguoiDung
                {
                    TenDangNhap = "guest_exp_" + suffix,
                    MatKhauHash = "test", Role = Role.KhachVangLai
                };
                var guest = new KhachVangLai
                {
                    NguoiDung = guestUser, HoTen = "Khách test",
                    SoDienThoai = "0912345678",
                    Email = "expired" + suffix + "@example.com"
                };
                db.ChiNhanhs.Add(branch);
                db.NguoiDungs.AddRange(user, guestUser);
                db.KhachVangLais.Add(guest);
                await db.SaveChangesAsync();
                var room = new PhongTro
                {
                    ChiNhanhId = branch.ChiNhanhId, SoPhong = "E" + suffix,
                    GiaThue = 1000000m, DienTich = 20, MoTa = "Test",
                    TrangThai = TrangThaiPhong.Trong, DuocDangTin = true
                };
                db.PhongTros.Add(room);
                await db.SaveChangesAsync();
                var hold = new YeuCauGiuCho
                {
                    KhachVangLaiId = guest.KhachVangLaiId,
                    PhongTroId = room.PhongTroId,
                    SoTienGiuCho = 200000m,
                    HanThanhToan = DateTime.UtcNow.AddDays(-2),
                    HanKyHopDong = DateTime.UtcNow.AddDays(-1),
                    TrangThai = TrangThaiYeuCauGiuCho.DangGiuCho
                };
                db.YeuCauGiuChos.Add(hold);
                await db.SaveChangesAsync();
                branchId = branch.ChiNhanhId;
                roomId = room.PhongTroId;
                userId = user.NguoiDungId;
                guestUserId = guestUser.NguoiDungId;
                guestId = guest.KhachVangLaiId;
                holdId = hold.YeuCauGiuChoId;
            }

            var now = DateTime.UtcNow;
            await using (var db = fixture.CreateDbContext())
            {
                var store = new ReservationStore(db);
                Assert.Equal(ReservationError.None,
                    await store.ExtendContractDeadlineAsync(userId.Value,
                        holdId.Value, now.AddDays(2), now));
                Assert.Equal(ReservationError.InvalidState,
                    await store.CancelExpiredContractAsync(userId.Value,
                        holdId.Value, "Chưa quá hạn", now));
                Assert.Null(await new PublicRoomStore(db, TimeProvider.System)
                    .GetAvailableAsync(roomId.Value));
            }

            await using (var db = fixture.CreateDbContext())
            {
                var hold = await db.YeuCauGiuChos.SingleAsync(h =>
                    h.YeuCauGiuChoId == holdId.Value);
                hold.HanKyHopDong = now.AddMinutes(-1);
                await db.SaveChangesAsync();
                var store = new ReservationStore(db);
                Assert.Equal(ReservationError.None,
                    await store.CancelExpiredContractAsync(userId.Value,
                        holdId.Value, "Khách không ký hợp đồng", now));
                Assert.NotNull(await new PublicRoomStore(db, TimeProvider.System)
                    .GetAvailableAsync(roomId.Value));

                var lateHold = new YeuCauGiuCho
                {
                    KhachVangLaiId = guestId.Value,
                    PhongTroId = roomId.Value,
                    SoTienGiuCho = 200000m,
                    HanThanhToan = now.AddHours(-1),
                    HanKyHopDong = now.AddDays(1),
                    TrangThai = TrangThaiYeuCauGiuCho.ChoXacNhanTien
                };
                db.YeuCauGiuChos.Add(lateHold);
                await db.SaveChangesAsync();
                lateHoldId = lateHold.YeuCauGiuChoId;
                var latePayment = new YeuCauThanhToanGiuCho
                {
                    YeuCauGiuChoId = lateHoldId.Value,
                    MaYeuCau = "L" + suffix,
                    NoiDungChuyenKhoan = "L" + suffix,
                    SoTien = 200000m,
                    HanThanhToan = now.AddHours(-1),
                    NgayTao = now.AddHours(-3),
                    NguoiTaoId = userId.Value,
                    TrangThai = TrangThaiYeuCauThanhToan.DaBaoChuyen
                };
                db.YeuCauThanhToanGiuChos.Add(latePayment);
                await db.SaveChangesAsync();
                latePaymentId = latePayment.YeuCauThanhToanGiuChoId;
                var evidence = new MinhChungThanhToanGiuCho
                {
                    YeuCauThanhToanGiuChoId = latePaymentId.Value,
                    UrlHinhAnh = "private://hold-evidence/test.jpg",
                    SoTienKhaiBao = 200000m,
                    NgayChuyenTien = now.AddHours(-2),
                    NgayTao = now.AddHours(-2),
                    TrangThai = TrangThaiMinhChungThanhToan.ChoXacNhan
                };
                db.MinhChungThanhToanGiuChos.Add(evidence);
                await db.SaveChangesAsync();
                var paymentStore = new ReservationPaymentStore(db);
                Assert.Equal(PaymentError.None,
                    await paymentStore.ConfirmReceivedAsync(userId.Value,
                        latePaymentId.Value, evidence.MinhChungThanhToanGiuChoId,
                        "LATE-" + suffix, 200000m, now, now));
                Assert.Equal(TrangThaiYeuCauGiuCho.HetHan,
                    lateHold.TrangThai);
                Assert.Equal(TrangThaiYeuCauThanhToan.HetHan,
                    latePayment.TrangThai);
                Assert.Equal(200000m,
                    (await new ReservationSettlementStore(db)
                        .GetAsync(lateHoldId.Value))!.MandatoryRefundAmount);
            }
        }
        finally
        {
            await using var db = fixture.CreateDbContext();
            if (lateHoldId.HasValue && latePaymentId.HasValue)
            {
                await db.GiaoDichGiuChos.Where(g =>
                    g.YeuCauThanhToanGiuChoId == latePaymentId.Value)
                    .ExecuteDeleteAsync();
                await db.MinhChungThanhToanGiuChos.Where(e =>
                    e.YeuCauThanhToanGiuChoId == latePaymentId.Value)
                    .ExecuteDeleteAsync();
                await db.LichSuTrangThaiYeuCauThanhToanGiuChos.Where(h =>
                    h.YeuCauThanhToanGiuChoId == latePaymentId.Value)
                    .ExecuteDeleteAsync();
                await db.YeuCauThanhToanGiuChos.Where(p =>
                    p.YeuCauThanhToanGiuChoId == latePaymentId.Value)
                    .ExecuteDeleteAsync();
                await db.LichSuTrangThaiYeuCauGiuChos.Where(h =>
                    h.YeuCauGiuChoId == lateHoldId.Value).ExecuteDeleteAsync();
                await db.YeuCauGiuChos.Where(h =>
                    h.YeuCauGiuChoId == lateHoldId.Value).ExecuteDeleteAsync();
            }
            if (holdId.HasValue)
            {
                await db.LichSuTrangThaiYeuCauGiuChos.Where(h =>
                    h.YeuCauGiuChoId == holdId.Value).ExecuteDeleteAsync();
                await db.YeuCauGiuChos.Where(h =>
                    h.YeuCauGiuChoId == holdId.Value).ExecuteDeleteAsync();
            }
            if (roomId.HasValue)
                await db.PhongTros.Where(r => r.PhongTroId == roomId.Value)
                    .ExecuteDeleteAsync();
            if (userId.HasValue)
                await db.NguoiDungs.Where(u => u.NguoiDungId == userId.Value)
                    .ExecuteDeleteAsync();
            if (guestId.HasValue)
                await db.KhachVangLais.Where(g => g.KhachVangLaiId == guestId.Value)
                    .ExecuteDeleteAsync();
            if (guestUserId.HasValue)
                await db.NguoiDungs.Where(u => u.NguoiDungId == guestUserId.Value)
                    .ExecuteDeleteAsync();
            if (branchId.HasValue)
                await db.ChiNhanhs.Where(b => b.ChiNhanhId == branchId.Value)
                    .ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task ApplyToContractDeposit_RequiresMatchingGuest_AndRefundsExcess()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        int? branchId = null, roomId = null, guestUserId = null;
        int? staffUserId = null, tenantId = null, contractId = null, holdId = null;
        try
        {
            await using (var db = fixture.CreateDbContext())
            {
                var branch = new ChiNhanh
                {
                    MaChiNhanh = "S" + suffix[..7], TenChiNhanh = "Test " + suffix,
                    DiaChi = "A", MoTa = "A", SoDienThoai = "0900000001"
                };
                var guestUser = new NguoiDung
                {
                    TenDangNhap = "guest2_" + suffix, MatKhauHash = "test",
                    Role = Role.KhachVangLai
                };
                var staffUser = new NguoiDung
                {
                    TenDangNhap = "staff2_" + suffix, MatKhauHash = "test",
                    Role = Role.NhanVien
                };
                var profile = new KhachVangLai
                {
                    NguoiDung = guestUser, HoTen = "Khách test",
                    SoDienThoai = "0912345678", Email = "apply" + suffix + "@example.com"
                };
                var tenant = new NguoiThue
                {
                    HoVaTen = "Khách test", SoDienThoai = profile.SoDienThoai,
                    Email = profile.Email, CCCD = "012345678901"
                };
                db.ChiNhanhs.Add(branch);
                db.NguoiDungs.AddRange(guestUser, staffUser);
                db.KhachVangLais.Add(profile);
                db.NguoiThues.Add(tenant);
                await db.SaveChangesAsync();
                var room = new PhongTro
                {
                    ChiNhanhId = branch.ChiNhanhId, SoPhong = "S" + suffix,
                    GiaThue = 1000000m, DienTich = 20, MoTa = "Test",
                    TrangThai = TrangThaiPhong.Trong, DuocDangTin = true
                };
                db.PhongTros.Add(room);
                await db.SaveChangesAsync();
                var contract = new HopDong
                {
                    MaHopDong = "TEST-" + suffix, PhongTroId = room.PhongTroId,
                    NguoiThueId = tenant.NguoiThueId,
                    ThoiDiemBatDau = DateTime.UtcNow,
                    TienCocPhong = 300000m, TienThuePhong = 1000000m,
                    TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
                };
                var hold = new YeuCauGiuCho
                {
                    KhachVangLaiId = profile.KhachVangLaiId,
                    PhongTroId = room.PhongTroId,
                    SoTienGiuCho = 300000m,
                    HanThanhToan = DateTime.UtcNow.AddDays(1),
                    HanKyHopDong = DateTime.UtcNow.AddDays(2),
                    TrangThai = TrangThaiYeuCauGiuCho.DangGiuCho
                };
                db.HopDongs.Add(contract);
                db.YeuCauGiuChos.Add(hold);
                await db.SaveChangesAsync();
                var payment = new YeuCauThanhToanGiuCho
                {
                    YeuCauGiuChoId = hold.YeuCauGiuChoId,
                    MaYeuCau = "T" + suffix,
                    NoiDungChuyenKhoan = "T" + suffix,
                    SoTien = 300000m, HanThanhToan = DateTime.UtcNow.AddDays(1),
                    NguoiTaoId = staffUser.NguoiDungId,
                    TrangThai = TrangThaiYeuCauThanhToan.DaHoanTat
                };
                db.YeuCauThanhToanGiuChos.Add(payment);
                await db.SaveChangesAsync();
                db.GiaoDichGiuChos.Add(new GiaoDichGiuCho
                {
                    YeuCauThanhToanGiuChoId = payment.YeuCauThanhToanGiuChoId,
                    MaGiaoDich = "PAY-" + suffix, SoTienThucNhan = 400000m,
                    PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
                    NguoiXacNhanId = staffUser.NguoiDungId,
                    NgayThucNhan = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
                branchId = branch.ChiNhanhId;
                roomId = room.PhongTroId;
                guestUserId = guestUser.NguoiDungId;
                staffUserId = staffUser.NguoiDungId;
                tenantId = tenant.NguoiThueId;
                contractId = contract.HopDongId;
                holdId = hold.YeuCauGiuChoId;
            }
            await using (var db = fixture.CreateDbContext())
            {
                var store = new ReservationSettlementStore(db);
                var now = DateTime.UtcNow;
                Assert.Equal(SettlementError.AmountExceedsAvailable,
                    await store.ApplyAsync(staffUserId.Value, holdId.Value,
                        contractId.Value, 400000m, now));
                Assert.Equal(SettlementError.None,
                    await store.ApplyAsync(staffUserId.Value, holdId.Value,
                        contractId.Value, 300000m, now));
            }
            await using (var db = fixture.CreateDbContext())
            {
                var store = new ReservationSettlementStore(db);
                var summary = await store.GetAsync(holdId.Value);
                Assert.Equal(100000m, summary!.MandatoryRefundAmount);
                Assert.Equal(SettlementError.BelowMandatoryRefund,
                    await store.DecideRefundAsync(staffUserId.Value, holdId.Value,
                        50000m, "Thiếu phần dư", DateTime.UtcNow));
                Assert.Equal(SettlementError.None,
                    await store.DecideRefundAsync(staffUserId.Value, holdId.Value,
                        100000m, "Hoàn phần dư", DateTime.UtcNow));
            }
        }
        finally
        {
            await using var db = fixture.CreateDbContext();
            if (holdId.HasValue)
            {
                await db.ApDungTienGiuChoVaoTienCocs.Where(a => a.YeuCauGiuChoId == holdId)
                    .ExecuteDeleteAsync();
                await db.QuyetDinhHoanTienGiuChos.Where(q => q.YeuCauGiuChoId == holdId)
                    .ExecuteDeleteAsync();
                await db.GiaoDichGiuChos.Where(g =>
                    g.YeuCauThanhToanGiuCho.YeuCauGiuChoId == holdId)
                    .ExecuteDeleteAsync();
                await db.YeuCauThanhToanGiuChos.Where(p => p.YeuCauGiuChoId == holdId)
                    .ExecuteDeleteAsync();
                await db.LichSuTrangThaiYeuCauGiuChos.Where(h => h.YeuCauGiuChoId == holdId)
                    .ExecuteDeleteAsync();
                await db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == holdId)
                    .ExecuteDeleteAsync();
            }
            if (contractId.HasValue)
                await db.HopDongs.Where(h => h.HopDongId == contractId).ExecuteDeleteAsync();
            if (roomId.HasValue)
                await db.PhongTros.Where(r => r.PhongTroId == roomId).ExecuteDeleteAsync();
            if (guestUserId.HasValue)
            {
                await db.KhachVangLais.Where(g => g.NguoiDungId == guestUserId)
                    .ExecuteDeleteAsync();
                await db.NguoiDungs.Where(u => u.NguoiDungId == guestUserId)
                    .ExecuteDeleteAsync();
            }
            if (staffUserId.HasValue)
                await db.NguoiDungs.Where(u => u.NguoiDungId == staffUserId)
                    .ExecuteDeleteAsync();
            if (tenantId.HasValue)
                await db.NguoiThues.Where(t => t.NguoiThueId == tenantId)
                    .ExecuteDeleteAsync();
            if (branchId.HasValue)
                await db.ChiNhanhs.Where(b => b.ChiNhanhId == branchId)
                    .ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task CreateApproveCancel_PersistsState_AndRejectsDuplicate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        int? branchId = null;
        int? roomId = null;
        int? guestUserId = null;
        int? staffUserId = null;
        int? holdId = null;
        try
        {
            await using (var db = fixture.CreateDbContext())
            {
                var branch = new ChiNhanh
                {
                    MaChiNhanh = "R" + suffix[..7], TenChiNhanh = "Test " + suffix,
                    DiaChi = "A", MoTa = "A", SoDienThoai = "0900000001"
                };
                var guest = new NguoiDung
                {
                    TenDangNhap = "guest_" + suffix, MatKhauHash = "test",
                    Role = Role.KhachVangLai
                };
                var staff = new NguoiDung
                {
                    TenDangNhap = "staff_" + suffix, MatKhauHash = "test",
                    Role = Role.NhanVien
                };
                var profile = new KhachVangLai
                {
                    NguoiDung = guest, HoTen = "Khách test",
                    SoDienThoai = "0912345678", Email = "guest@example.com"
                };
                db.ChiNhanhs.Add(branch);
                db.NguoiDungs.AddRange(guest, staff);
                db.KhachVangLais.Add(profile);
                await db.SaveChangesAsync();
                var room = new PhongTro
                {
                    ChiNhanhId = branch.ChiNhanhId, SoPhong = "R" + suffix,
                    GiaThue = 1000000m, DienTich = 20, MoTa = "Test",
                    TrangThai = TrangThaiPhong.Trong, DuocDangTin = true
                };
                db.PhongTros.Add(room);
                await db.SaveChangesAsync();
                branchId = branch.ChiNhanhId;
                roomId = room.PhongTroId;
                guestUserId = guest.NguoiDungId;
                staffUserId = staff.NguoiDungId;
            }

            var now = DateTime.UtcNow;
            await using (var db = fixture.CreateDbContext())
            {
                var store = new ReservationStore(db);
                var created = await store.CreateAsync(guestUserId.Value, roomId.Value,
                    null, now);
                Assert.Equal(ReservationError.None, created.Error);
                holdId = created.Id;
                Assert.NotNull(holdId);
            }
            await using (var db = fixture.CreateDbContext())
            {
                var store = new ReservationStore(db);
                var duplicate = await store.CreateAsync(guestUserId.Value, roomId.Value,
                    null, now);
                Assert.Equal(ReservationError.Duplicate, duplicate.Error);
                Assert.Equal(ReservationError.None, await store.ApproveAsync(staffUserId.Value,
                    holdId.Value, 400000m, now.AddDays(1), now.AddDays(2), now));
                Assert.Null(await new PublicRoomStore(db, TimeProvider.System)
                    .GetAvailableAsync(roomId.Value));
            }
            await using (var db = fixture.CreateDbContext())
            {
                var store = new ReservationStore(db);
                var mine = await store.ListMineAsync(guestUserId.Value);
                var hold = Assert.Single(mine);
                Assert.Equal("ChoThanhToan", hold.Status);
                Assert.Equal(400000m, hold.HoldAmount);
                Assert.NotNull(hold.PaymentRequestId);
                var paymentStore = new ReservationPaymentStore(db);
                var evidenceId = await paymentStore.AddEvidenceAsync(guestUserId.Value,
                    new EvidenceSubmission(hold.PaymentRequestId.Value,
                        Guid.NewGuid().ToString("N") + ".jpg", 200000m, now,
                        "BANK-" + suffix, null), now);
                Assert.NotNull(evidenceId);
                Assert.Equal(PaymentError.None, await paymentStore.ConfirmReceivedAsync(
                    staffUserId.Value, hold.PaymentRequestId.Value, evidenceId,
                    "BANK-" + suffix, 200000m, now, now));
                Assert.Equal(PaymentError.DuplicateTransaction,
                    await paymentStore.ConfirmReceivedAsync(staffUserId.Value,
                        hold.PaymentRequestId.Value, evidenceId, "BANK-" + suffix,
                        200000m, now, now));
                Assert.Equal(PaymentError.None, await paymentStore.ConfirmReceivedAsync(
                    staffUserId.Value, hold.PaymentRequestId.Value, null,
                    "BANK2-" + suffix, 200000m, now, now));
                Assert.Equal("DangGiuCho", Assert.Single(await store.ListMineAsync(
                    guestUserId.Value)).Status);
                Assert.Equal(PaymentError.None, await paymentStore.ConfirmReceivedAsync(
                    staffUserId.Value, hold.PaymentRequestId.Value, null,
                    "BANK3-" + suffix, 50000m, now.AddDays(2), now.AddDays(2)));
                var settlement = new ReservationSettlementStore(db);
                Assert.Equal(50000m,
                    (await settlement.GetAsync(holdId.Value))!.MandatoryRefundAmount);
                Assert.Equal(ReservationError.None,
                    await store.CancelAsync(guestUserId.Value, holdId.Value,
                        now.AddDays(2)));
                Assert.NotNull(await new PublicRoomStore(db, TimeProvider.System)
                    .GetAvailableAsync(roomId.Value));
                Assert.Equal(SettlementError.None, await settlement.DecideRefundAsync(
                    staffUserId.Value, holdId.Value, 450000m,
                    "Khách hủy sau khi chuyển tiền", now));
                Assert.Equal(SettlementError.None, await settlement.RecordRefundAsync(
                    staffUserId.Value, holdId.Value, "REF-" + suffix,
                    200000m, now));
                Assert.Equal(SettlementError.DuplicateTransaction,
                    await settlement.RecordRefundAsync(staffUserId.Value,
                        holdId.Value, "REF-" + suffix, 200000m, now));
                Assert.Equal(SettlementError.None, await settlement.RecordRefundAsync(
                    staffUserId.Value, holdId.Value, "REF2-" + suffix,
                    250000m, now));
                Assert.Equal(450000m, (await settlement.GetAsync(holdId.Value))!.RefundedAmount);
            }
            await using (var db = fixture.CreateDbContext())
            {
                Assert.Equal(TrangThaiYeuCauGiuCho.DaHuy,
                    (await db.YeuCauGiuChos.SingleAsync(h => h.YeuCauGiuChoId == holdId)).TrangThai);
            }
        }
        finally
        {
            await using var db = fixture.CreateDbContext();
            if (holdId.HasValue)
            {
                var decisionId = await db.QuyetDinhHoanTienGiuChos
                    .Where(d => d.YeuCauGiuChoId == holdId)
                    .Select(d => (int?)d.QuyetDinhHoanTienGiuChoId)
                    .SingleOrDefaultAsync();
                if (decisionId.HasValue)
                {
                    await db.GiaoDichHoanTienGiuChos.Where(g =>
                        g.QuyetDinhHoanTienGiuChoId == decisionId)
                        .ExecuteDeleteAsync();
                    await db.QuyetDinhHoanTienGiuChos.Where(d =>
                        d.QuyetDinhHoanTienGiuChoId == decisionId)
                        .ExecuteDeleteAsync();
                }
                var paymentIds = await db.YeuCauThanhToanGiuChos
                    .Where(p => p.YeuCauGiuChoId == holdId)
                    .Select(p => p.YeuCauThanhToanGiuChoId).ToArrayAsync();
                await db.GiaoDichGiuChos.Where(g =>
                    paymentIds.Contains(g.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
                await db.MinhChungThanhToanGiuChos.Where(e =>
                    paymentIds.Contains(e.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
                await db.LichSuTrangThaiYeuCauThanhToanGiuChos
                    .Where(h => paymentIds.Contains(h.YeuCauThanhToanGiuChoId))
                    .ExecuteDeleteAsync();
                await db.YeuCauThanhToanGiuChos.Where(p => p.YeuCauGiuChoId == holdId)
                    .ExecuteDeleteAsync();
                await db.LichSuTrangThaiYeuCauGiuChos.Where(h => h.YeuCauGiuChoId == holdId)
                    .ExecuteDeleteAsync();
                await db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == holdId)
                    .ExecuteDeleteAsync();
            }
            if (guestUserId.HasValue)
            {
                await db.KhachVangLais.Where(g => g.NguoiDungId == guestUserId)
                    .ExecuteDeleteAsync();
                await db.NguoiDungs.Where(u => u.NguoiDungId == guestUserId)
                    .ExecuteDeleteAsync();
            }
            if (staffUserId.HasValue)
                await db.NguoiDungs.Where(u => u.NguoiDungId == staffUserId)
                    .ExecuteDeleteAsync();
            if (roomId.HasValue)
                await db.PhongTros.Where(r => r.PhongTroId == roomId)
                    .ExecuteDeleteAsync();
            if (branchId.HasValue)
                await db.ChiNhanhs.Where(b => b.ChiNhanhId == branchId)
                    .ExecuteDeleteAsync();
        }
    }
}
