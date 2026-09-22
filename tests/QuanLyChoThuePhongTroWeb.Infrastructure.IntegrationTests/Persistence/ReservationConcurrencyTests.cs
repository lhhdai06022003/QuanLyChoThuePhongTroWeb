using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class ReservationConcurrencyTests : IAsyncLifetime
    {
        private readonly PostgreSqlFixture _fixture;
        private readonly List<int> _createdPhongTroIds = new();
        private readonly List<int> _createdChiNhanhIds = new();
        private readonly List<int> _createdNguoiDungIds = new();

        public ReservationConcurrencyTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            await using var context = _fixture.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();

            if (_createdPhongTroIds.Count > 0)
            {
                var reservationIds = await context.YeuCauGiuChos
                    .Where(y => _createdPhongTroIds.Contains(y.PhongTroId))
                    .Select(y => y.YeuCauGiuChoId)
                    .ToListAsync();

                var paymentRequestIds = await context.YeuCauThanhToanGiuChos
                    .Where(y => reservationIds.Contains(y.YeuCauGiuChoId))
                    .Select(y => y.YeuCauThanhToanGiuChoId)
                    .ToListAsync();

                var refundDecisionIds = await context.QuyetDinhHoanTienGiuChos
                    .Where(q => reservationIds.Contains(q.YeuCauGiuChoId))
                    .Select(q => q.QuyetDinhHoanTienGiuChoId)
                    .ToListAsync();

                await context.GiaoDichGiuChos
                    .Where(g => paymentRequestIds.Contains(g.YeuCauThanhToanGiuChoId))
                    .ExecuteDeleteAsync();
                await context.MinhChungThanhToanGiuChos
                    .Where(m => paymentRequestIds.Contains(m.YeuCauThanhToanGiuChoId))
                    .ExecuteDeleteAsync();
                await context.LichSuTrangThaiYeuCauThanhToanGiuChos
                    .Where(l => paymentRequestIds.Contains(l.YeuCauThanhToanGiuChoId))
                    .ExecuteDeleteAsync();
                await context.YeuCauThanhToanGiuChos
                    .Where(y => paymentRequestIds.Contains(y.YeuCauThanhToanGiuChoId))
                    .ExecuteDeleteAsync();

                await context.GiaoDichHoanTienGiuChos
                    .Where(g => refundDecisionIds.Contains(g.QuyetDinhHoanTienGiuChoId))
                    .ExecuteDeleteAsync();
                await context.QuyetDinhHoanTienGiuChos
                    .Where(q => refundDecisionIds.Contains(q.QuyetDinhHoanTienGiuChoId))
                    .ExecuteDeleteAsync();
                await context.ApDungTienGiuChoVaoTienCocs
                    .Where(a => reservationIds.Contains(a.YeuCauGiuChoId))
                    .ExecuteDeleteAsync();
                await context.LichSuTrangThaiYeuCauGiuChos
                    .Where(l => reservationIds.Contains(l.YeuCauGiuChoId))
                    .ExecuteDeleteAsync();
                await context.YeuCauGiuChos
                    .Where(y => reservationIds.Contains(y.YeuCauGiuChoId))
                    .ExecuteDeleteAsync();

                var viewingRequestIds = await context.YeuCauXemPhongs
                    .Where(y => _createdPhongTroIds.Contains(y.PhongTroId))
                    .Select(y => y.YeuCauXemPhongId)
                    .ToListAsync();

                await context.LichSuTrangThaiYeuCauXemPhongs
                    .Where(l => viewingRequestIds.Contains(l.YeuCauXemPhongId))
                    .ExecuteDeleteAsync();
                await context.YeuCauXemPhongs
                    .Where(y => viewingRequestIds.Contains(y.YeuCauXemPhongId))
                    .ExecuteDeleteAsync();
                await context.KhungGioXemPhongs
                    .Where(k => _createdPhongTroIds.Contains(k.PhongTroId))
                    .ExecuteDeleteAsync();
                await context.AnhPhongTros
                    .Where(a => _createdPhongTroIds.Contains(a.PhongTroId))
                    .ExecuteDeleteAsync();
            }

            if (_createdNguoiDungIds.Count > 0 || _createdChiNhanhIds.Count > 0)
            {
                await context.NhanVienChiNhanhs
                    .Where(n => _createdNguoiDungIds.Contains(n.NguoiDungId) ||
                                _createdChiNhanhIds.Contains(n.ChiNhanhId))
                    .ExecuteDeleteAsync();
            }

            if (_createdNguoiDungIds.Count > 0)
            {
                await context.KhachVangLais
                    .Where(k => _createdNguoiDungIds.Contains(k.NguoiDungId))
                    .ExecuteDeleteAsync();
            }

            if (_createdPhongTroIds.Count > 0)
            {
                await context.PhongTros
                    .Where(p => _createdPhongTroIds.Contains(p.PhongTroId))
                    .ExecuteDeleteAsync();
            }

            if (_createdNguoiDungIds.Count > 0)
            {
                await context.NguoiDungs
                    .Where(u => _createdNguoiDungIds.Contains(u.NguoiDungId))
                    .ExecuteDeleteAsync();
            }

            if (_createdChiNhanhIds.Count > 0)
            {
                await context.ChiNhanhs
                    .Where(c => _createdChiNhanhIds.Contains(c.ChiNhanhId))
                    .ExecuteDeleteAsync();
            }

            await transaction.CommitAsync();
        }

        #region Helper Factory Methods

        private async Task<ChiNhanh> CreateChiNhanhAsync(ApplicationDbContext context)
        {
            var code = "CN" + Guid.NewGuid().ToString("N")[..8].ToUpper();
            var cn = new ChiNhanh
            {
                TenChiNhanh = "Chi Nhanh Test " + code,
                DiaChi = "Dia Chi Test",
                SoDienThoai = "0988000111",
                MoTa = "Mo ta chi nhanh test",
                MaChiNhanh = code
            };
            context.ChiNhanhs.Add(cn);
            await context.SaveChangesAsync();
            _createdChiNhanhIds.Add(cn.ChiNhanhId);
            return cn;
        }

        private async Task<NguoiDung> CreateNguoiDungAsync(ApplicationDbContext context, Role role)
        {
            var uid = Guid.NewGuid().ToString("N")[..8];
            var user = new NguoiDung
            {
                TenDangNhap = "user_" + uid,
                MatKhauHash = "hash_" + uid,
                Role = role,
                IsActive = true
            };
            context.NguoiDungs.Add(user);
            await context.SaveChangesAsync();
            _createdNguoiDungIds.Add(user.NguoiDungId);
            return user;
        }

        private async Task<PhongTro> CreatePhongTroAsync(ApplicationDbContext context, int chiNhanhId)
        {
            var uid = Guid.NewGuid().ToString("N")[..6];
            var pt = new PhongTro
            {
                ChiNhanhId = chiNhanhId,
                SoPhong = "P" + uid,
                TangLau = 1,
                GiaThue = 3000000m,
                DienTich = 25.0,
                SoNguoiToiDa = 2,
                TrangThai = TrangThaiPhong.Trong,
                MoTa = "Phong tro test",
                DuocDangTin = true,
                MaCongKhai = "PUB" + uid
            };
            context.PhongTros.Add(pt);
            await context.SaveChangesAsync();
            _createdPhongTroIds.Add(pt.PhongTroId);
            return pt;
        }

        private async Task<KhachVangLai> CreateKhachVangLaiAsync(ApplicationDbContext context, int nguoiDungId)
        {
            var kvl = new KhachVangLai
            {
                NguoiDungId = nguoiDungId,
                HoTen = "Khach Test",
                SoDienThoai = "0911222333"
            };
            context.KhachVangLais.Add(kvl);
            await context.SaveChangesAsync();
            return kvl;
        }

        #endregion

        #region 3.1. Unique phan cong nhan vien chi nhanh

        [Fact]
        public async Task BranchStaffAssignment_RejectsDuplicatePair_RegardlessOfIsActive()
        {
            await using var context = _fixture.CreateDbContext();
            var cn = await CreateChiNhanhAsync(context);
            var user = await CreateNguoiDungAsync(context, Role.NhanVien);

            // 1. First assignment with IsActive = true
            var assignment1 = new NhanVienChiNhanh
            {
                NguoiDungId = user.NguoiDungId,
                ChiNhanhId = cn.ChiNhanhId,
                IsActive = true,
                NguoiPhanCongId = user.NguoiDungId,
                NgayPhanCong = DateTime.UtcNow
            };
            context.NhanVienChiNhanhs.Add(assignment1);
            await context.SaveChangesAsync();

            // 2. Second assignment with same (NguoiDungId, ChiNhanhId) even with IsActive = false
            await using var context2 = _fixture.CreateDbContext();
            var assignment2 = new NhanVienChiNhanh
            {
                NguoiDungId = user.NguoiDungId,
                ChiNhanhId = cn.ChiNhanhId,
                IsActive = false,
                NguoiPhanCongId = user.NguoiDungId,
                NgayPhanCong = DateTime.UtcNow
            };
            context2.NhanVienChiNhanhs.Add(assignment2);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context2.SaveChangesAsync());
            Assert.Contains("23505", ex.InnerException?.Message ?? ""); // Postgres unique_violation code
        }

        #endregion

        #region 3.2. Mot yeu cau giu cho hoat dong tren moi phong

        [Fact]
        public async Task ActiveReservationPerRoom_RejectsSecondActiveReservation_ForSameRoom()
        {
            await using var context = _fixture.CreateDbContext();
            var cn = await CreateChiNhanhAsync(context);
            var room = await CreatePhongTroAsync(context, cn.ChiNhanhId);
            var user1 = await CreateNguoiDungAsync(context, Role.KhachVangLai);
            var kvl1 = await CreateKhachVangLaiAsync(context, user1.NguoiDungId);

            var user2 = await CreateNguoiDungAsync(context, Role.KhachVangLai);
            var kvl2 = await CreateKhachVangLaiAsync(context, user2.NguoiDungId);

            // 1. First reservation in active state ChoThanhToan (1)
            var res1 = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl1.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan,
                SoTienGiuCho = 500000m,
                HanThanhToan = DateTime.UtcNow.AddDays(1)
            };
            context.YeuCauGiuChos.Add(res1);
            await context.SaveChangesAsync();

            // 2. Second reservation on same room in active state DangGiuCho (3)
            await using var context2 = _fixture.CreateDbContext();
            var res2 = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl2.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.DangGiuCho,
                SoTienGiuCho = 500000m,
                HanThanhToan = DateTime.UtcNow.AddDays(1)
            };
            context2.YeuCauGiuChos.Add(res2);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context2.SaveChangesAsync());
            Assert.Contains("23505", ex.InnerException?.Message ?? "");
        }

        [Fact]
        public async Task ActiveReservationPerRoom_AllowsNewActiveReservation_WhenPreviousIsTerminalOrMoiTao()
        {
            await using var context = _fixture.CreateDbContext();
            var cn = await CreateChiNhanhAsync(context);
            var room = await CreatePhongTroAsync(context, cn.ChiNhanhId);
            var user = await CreateNguoiDungAsync(context, Role.KhachVangLai);
            var kvl = await CreateKhachVangLaiAsync(context, user.NguoiDungId);

            // 1. MoiTao (0) does not lock room
            var resMoiTao = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.MoiTao,
                SoTienGiuCho = null
            };
            context.YeuCauGiuChos.Add(resMoiTao);

            // 2. DaHuy (7) does not lock room
            var resDaHuy = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.DaHuy,
                SoTienGiuCho = 500000m
            };
            context.YeuCauGiuChos.Add(resDaHuy);

            // 3. ChoThanhToan (1) active reservation
            var resActive = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan,
                SoTienGiuCho = 500000m,
                HanThanhToan = DateTime.UtcNow.AddDays(1)
            };
            context.YeuCauGiuChos.Add(resActive);

            // All three can be saved together without unique violation!
            await context.SaveChangesAsync();
            Assert.True(resMoiTao.YeuCauGiuChoId > 0);
            Assert.True(resDaHuy.YeuCauGiuChoId > 0);
            Assert.True(resActive.YeuCauGiuChoId > 0);
        }

        [Fact]
        public async Task ConcurrentApproval_CompetingOnSameRoom_OnlyOneSucceeds()
        {
            await using var setupContext = _fixture.CreateDbContext();
            var cn = await CreateChiNhanhAsync(setupContext);
            var room = await CreatePhongTroAsync(setupContext, cn.ChiNhanhId);
            var userA = await CreateNguoiDungAsync(setupContext, Role.KhachVangLai);
            var kvlA = await CreateKhachVangLaiAsync(setupContext, userA.NguoiDungId);
            var userB = await CreateNguoiDungAsync(setupContext, Role.KhachVangLai);
            var kvlB = await CreateKhachVangLaiAsync(setupContext, userB.NguoiDungId);

            await using var contextA = _fixture.CreateDbContext();
            await using var contextB = _fixture.CreateDbContext();
            await using var transactionA = await contextA.Database.BeginTransactionAsync();
            await using var transactionB = await contextB.Database.BeginTransactionAsync();

            var resA = new YeuCauGiuCho
            {
                KhachVangLaiId = kvlA.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan,
                SoTienGiuCho = 500000m,
                HanThanhToan = DateTime.UtcNow.AddDays(1)
            };

            var resB = new YeuCauGiuCho
            {
                KhachVangLaiId = kvlB.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan,
                SoTienGiuCho = 500000m,
                HanThanhToan = DateTime.UtcNow.AddDays(1)
            };

            contextA.YeuCauGiuChos.Add(resA);
            contextB.YeuCauGiuChos.Add(resB);

            // A inserts the active reservation but keeps its transaction open.
            await contextA.SaveChangesAsync();
            Assert.True(resA.YeuCauGiuChoId > 0);

            // B competes for the same filtered unique key while A is still uncommitted.
            var competingSave = contextB.SaveChangesAsync();
            Assert.False(competingSave.IsCompleted);

            await transactionA.CommitAsync();

            var ex = await Assert.ThrowsAsync<DbUpdateException>(async () => await competingSave);
            Assert.Contains("23505", ex.InnerException?.Message ?? "");
            await transactionB.RollbackAsync();
        }

        #endregion

        #region 3.3. Ma giao dich giu cho duy nhat

        [Fact]
        public async Task DepositTransaction_RejectsDuplicateTransactionCode()
        {
            await using var context = _fixture.CreateDbContext();
            var cn = await CreateChiNhanhAsync(context);
            var room = await CreatePhongTroAsync(context, cn.ChiNhanhId);
            var user = await CreateNguoiDungAsync(context, Role.KhachVangLai);
            var staff = await CreateNguoiDungAsync(context, Role.NhanVien);
            var kvl = await CreateKhachVangLaiAsync(context, user.NguoiDungId);

            var res = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.DangGiuCho,
                SoTienGiuCho = 500000m
            };
            context.YeuCauGiuChos.Add(res);
            await context.SaveChangesAsync();

            var paymentReq = new YeuCauThanhToanGiuCho
            {
                YeuCauGiuChoId = res.YeuCauGiuChoId,
                NguoiTaoId = staff.NguoiDungId,
                MaYeuCau = "PAY" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                NoiDungChuyenKhoan = "NDCK test",
                HanThanhToan = DateTime.UtcNow.AddDays(1),
                SoTien = 500000m,
                TrangThai = TrangThaiYeuCauThanhToan.DaHoanTat
            };
            context.YeuCauThanhToanGiuChos.Add(paymentReq);
            await context.SaveChangesAsync();

            var sharedTxCode = "TX" + Guid.NewGuid().ToString("N")[..10].ToUpper();

            var tx1 = new GiaoDichGiuCho
            {
                YeuCauThanhToanGiuChoId = paymentReq.YeuCauThanhToanGiuChoId,
                MaGiaoDich = sharedTxCode,
                SoTienThucNhan = 500000m,
                PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
                NgayThucNhan = DateTime.UtcNow,
                NguoiXacNhanId = staff.NguoiDungId
            };
            context.GiaoDichGiuChos.Add(tx1);
            await context.SaveChangesAsync();

            await using var context2 = _fixture.CreateDbContext();
            var tx2 = new GiaoDichGiuCho
            {
                YeuCauThanhToanGiuChoId = paymentReq.YeuCauThanhToanGiuChoId,
                MaGiaoDich = sharedTxCode, // Duplicate
                SoTienThucNhan = 500000m,
                PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
                NgayThucNhan = DateTime.UtcNow,
                NguoiXacNhanId = staff.NguoiDungId
            };
            context2.GiaoDichGiuChos.Add(tx2);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context2.SaveChangesAsync());
            Assert.Contains("23505", ex.InnerException?.Message ?? "");
        }

        #endregion

        #region 3.4. Ma giao dich hoan tien duy nhat

        [Fact]
        public async Task RefundTransaction_RejectsDuplicateRefundTransactionCode()
        {
            await using var context = _fixture.CreateDbContext();
            var cn = await CreateChiNhanhAsync(context);
            var room = await CreatePhongTroAsync(context, cn.ChiNhanhId);
            var user = await CreateNguoiDungAsync(context, Role.KhachVangLai);
            var staff = await CreateNguoiDungAsync(context, Role.NhanVien);
            var kvl = await CreateKhachVangLaiAsync(context, user.NguoiDungId);

            var res = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.DaHuy,
                SoTienGiuCho = 500000m
            };
            context.YeuCauGiuChos.Add(res);
            await context.SaveChangesAsync();

            var refundDecision = new QuyetDinhHoanTienGiuCho
            {
                YeuCauGiuChoId = res.YeuCauGiuChoId,
                SoTienHoanDuyet = 500000m,
                LyDo = "Khach huy phong hop le",
                NguoiQuyetDinhId = staff.NguoiDungId,
                TrangThai = TrangThaiHoanTien.DangHoanTien
            };
            context.QuyetDinhHoanTienGiuChos.Add(refundDecision);
            await context.SaveChangesAsync();

            var sharedRefundCode = "RF" + Guid.NewGuid().ToString("N")[..10].ToUpper();

            var rtx1 = new GiaoDichHoanTienGiuCho
            {
                QuyetDinhHoanTienGiuChoId = refundDecision.QuyetDinhHoanTienGiuChoId,
                SoTienHoan = 250000m,
                MaGiaoDichHoan = sharedRefundCode,
                NguoiXacNhanId = staff.NguoiDungId,
                NgayHoan = DateTime.UtcNow
            };
            context.GiaoDichHoanTienGiuChos.Add(rtx1);
            await context.SaveChangesAsync();

            await using var context2 = _fixture.CreateDbContext();
            var rtx2 = new GiaoDichHoanTienGiuCho
            {
                QuyetDinhHoanTienGiuChoId = refundDecision.QuyetDinhHoanTienGiuChoId,
                SoTienHoan = 250000m,
                MaGiaoDichHoan = sharedRefundCode, // Duplicate
                NguoiXacNhanId = staff.NguoiDungId,
                NgayHoan = DateTime.UtcNow
            };
            context2.GiaoDichHoanTienGiuChos.Add(rtx2);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context2.SaveChangesAsync());
            Assert.Contains("23505", ex.InnerException?.Message ?? "");
        }

        #endregion

        #region 3.5. Check constraints khung gio xem phong

        [Fact]
        public async Task ViewingSlot_CheckConstraints_EnforcedByDatabase()
        {
            await using var context = _fixture.CreateDbContext();
            var cn = await CreateChiNhanhAsync(context);
            var room = await CreatePhongTroAsync(context, cn.ChiNhanhId);
            var staff = await CreateNguoiDungAsync(context, Role.NhanVien);

            // 1. Valid slot succeeds
            var validSlot = new KhungGioXemPhong
            {
                PhongTroId = room.PhongTroId,
                ThoiGianBatDau = DateTime.UtcNow.AddHours(1),
                ThoiGianKetThuc = DateTime.UtcNow.AddHours(2),
                SoLuongToiDa = 5,
                NguoiTaoId = staff.NguoiDungId
            };
            context.KhungGioXemPhongs.Add(validSlot);
            await context.SaveChangesAsync();
            Assert.True(validSlot.KhungGioXemPhongId > 0);

            // 2. Invalid time: ThoiGianBatDau >= ThoiGianKetThuc
            await using var contextTime = _fixture.CreateDbContext();
            var invalidTimeSlot = new KhungGioXemPhong
            {
                PhongTroId = room.PhongTroId,
                ThoiGianBatDau = DateTime.UtcNow.AddHours(3),
                ThoiGianKetThuc = DateTime.UtcNow.AddHours(2), // End before start
                SoLuongToiDa = 3,
                NguoiTaoId = staff.NguoiDungId
            };
            contextTime.KhungGioXemPhongs.Add(invalidTimeSlot);
            var exTime = await Assert.ThrowsAsync<DbUpdateException>(() => contextTime.SaveChangesAsync());
            Assert.Contains("CK_KhungGioXemPhong_ThoiGian", exTime.InnerException?.Message ?? "");

            // 3. Invalid capacity: SoLuongToiDa <= 0
            await using var contextCap = _fixture.CreateDbContext();
            var invalidCapSlot = new KhungGioXemPhong
            {
                PhongTroId = room.PhongTroId,
                ThoiGianBatDau = DateTime.UtcNow.AddHours(4),
                ThoiGianKetThuc = DateTime.UtcNow.AddHours(5),
                SoLuongToiDa = 0, // <= 0
                NguoiTaoId = staff.NguoiDungId
            };
            contextCap.KhungGioXemPhongs.Add(invalidCapSlot);
            var exCap = await Assert.ThrowsAsync<DbUpdateException>(() => contextCap.SaveChangesAsync());
            Assert.Contains("CK_KhungGioXemPhong_SoLuongToiDa", exCap.InnerException?.Message ?? "");
        }

        #endregion

        #region 3.6. Check constraints so tien giu cho

        [Fact]
        public async Task ReservationDeposit_CheckConstraints_EnforcedByDatabase()
        {
            await using var context = _fixture.CreateDbContext();
            var cn = await CreateChiNhanhAsync(context);
            var room = await CreatePhongTroAsync(context, cn.ChiNhanhId);
            var user = await CreateNguoiDungAsync(context, Role.KhachVangLai);
            var kvl = await CreateKhachVangLaiAsync(context, user.NguoiDungId);

            // 1. MoiTao allows SoTienGiuCho = null
            var resNull = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.MoiTao,
                SoTienGiuCho = null
            };
            context.YeuCauGiuChos.Add(resNull);
            await context.SaveChangesAsync();
            Assert.True(resNull.YeuCauGiuChoId > 0);

            // 2. Reject SoTienGiuCho = 0
            await using var contextZero = _fixture.CreateDbContext();
            var resZero = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.MoiTao,
                SoTienGiuCho = 0m
            };
            contextZero.YeuCauGiuChos.Add(resZero);
            var exZero = await Assert.ThrowsAsync<DbUpdateException>(() => contextZero.SaveChangesAsync());
            Assert.Contains("CK_YeuCauGiuCho_SoTienGiuCho", exZero.InnerException?.Message ?? "");

            // 3. Reject SoTienGiuCho < 0
            await using var contextNeg = _fixture.CreateDbContext();
            var resNeg = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.MoiTao,
                SoTienGiuCho = -100000m
            };
            contextNeg.YeuCauGiuChos.Add(resNeg);
            var exNeg = await Assert.ThrowsAsync<DbUpdateException>(() => contextNeg.SaveChangesAsync());
            Assert.Contains("CK_YeuCauGiuCho_SoTienGiuCho", exNeg.InnerException?.Message ?? "");

            // 4. Reject ChoThanhToan (1) with SoTienGiuCho = null
            await using var contextStatusNull = _fixture.CreateDbContext();
            var resStatusNull = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan,
                SoTienGiuCho = null
            };
            contextStatusNull.YeuCauGiuChos.Add(resStatusNull);
            var exStatusNull = await Assert.ThrowsAsync<DbUpdateException>(() => contextStatusNull.SaveChangesAsync());
            Assert.Contains("CK_YeuCauGiuCho_SoTienTheoTrangThai", exStatusNull.InnerException?.Message ?? "");

            // 5. Allow ChoThanhToan (1) with SoTienGiuCho > 0
            await using var contextValidActive = _fixture.CreateDbContext();
            var resValidActive = new YeuCauGiuCho
            {
                KhachVangLaiId = kvl.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan,
                SoTienGiuCho = 500000m,
                HanThanhToan = DateTime.UtcNow.AddDays(2)
            };
            contextValidActive.YeuCauGiuChos.Add(resValidActive);
            await contextValidActive.SaveChangesAsync();
            Assert.True(resValidActive.YeuCauGiuChoId > 0);
        }

        #endregion
    }
}
