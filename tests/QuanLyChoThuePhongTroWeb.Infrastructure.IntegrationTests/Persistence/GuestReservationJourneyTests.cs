using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

[Collection("PostgreSqlCollection")]
public sealed class GuestReservationJourneyTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private int branchId, roomId, adminId, guestUserId, otherStaffId, tenantUserId;
    private string code = "";
    private readonly string unique = Guid.NewGuid().ToString("N");

    public async Task InitializeAsync()
    {
        await using var db = fixture.CreateDbContext();
        var branch = new ChiNhanh
        {
            MaChiNhanh = "J" + unique[..8], TenChiNhanh = "Guest journey test",
            DiaChi = "Test address", MoTa = "Test", SoDienThoai = "0900000000"
        };
        var room = new PhongTro
        {
            ChiNhanh = branch, SoPhong = "J" + unique[..6], GiaThue = 2_000_000,
            DienTich = 20, SoNguoiToiDa = 2, TrangThai = TrangThaiPhong.Trong,
            MoTa = "Test room", DuocDangTin = true, MaCongKhai = unique
        };
        var admin = new NguoiDung { TenDangNhap = "admin_j" + unique, MatKhauHash = "test", Role = Role.Admin };
        var user = new NguoiDung { TenDangNhap = "guest_j" + unique, MatKhauHash = "test", Role = Role.KhachVangLai };
        var staff = new NguoiDung { TenDangNhap = "staff_j" + unique, MatKhauHash = "test", Role = Role.NhanVien };
        db.PhongTros.Add(room);
        db.NguoiDungs.AddRange(admin, user, staff);
        db.KhachVangLais.Add(new KhachVangLai
        {
            NguoiDung = user, HoTen = "Guest Test", Email = "guest_" + unique + "@example.test",
            SoDienThoai = "0900000001", CCCD = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString()
        });
        await db.SaveChangesAsync();
        branchId = branch.ChiNhanhId; roomId = room.PhongTroId; adminId = admin.NguoiDungId;
        guestUserId = user.NguoiDungId; otherStaffId = staff.NguoiDungId; code = unique;
    }

    [Fact]
    public async Task ExistingTenantCanScheduleViewAndReserveWithOwnProfile()
    {
        var when = DateTime.UtcNow.AddDays(2);
        await using (var db = fixture.CreateDbContext())
        {
            var tenant = new NguoiThue
            {
                HoVaTen = "Tenant Test", SoDienThoai = "0900000002",
                Email = "tenant_" + unique + "@example.test",
                CCCD = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString()
            };
            var user = new NguoiDung
            {
                TenDangNhap = "tenant_" + unique, MatKhauHash = "test",
                Role = Role.KhachThue, NguoiThue = tenant
            };
            db.NguoiDungs.Add(user);
            db.KhungGioXemPhongs.Add(new KhungGioXemPhong
            {
                PhongTroId = roomId, NguoiTaoId = adminId,
                ThoiGianBatDau = when, ThoiGianKetThuc = when.AddHours(1), SoLuongToiDa = 1
            });
            await db.SaveChangesAsync();
            tenantUserId = user.NguoiDungId;
        }

        await using (var db = fixture.CreateDbContext())
        {
            var slotId = await db.KhungGioXemPhongs.Where(item => item.PhongTroId == roomId)
                .Select(item => item.KhungGioXemPhongId).SingleAsync();
            Assert.True(await new LichXemPhongStore(db).CreateAsync(new(code, slotId, when,
                "Tenant Test", "0900000002", null, null, tenantUserId)));
        }

        int reservationId;
        await using (var db = fixture.CreateDbContext())
        {
            var viewing = await db.YeuCauXemPhongs.Include(item => item.KhachVangLai)
                .SingleAsync(item => item.PhongTroId == roomId);
            Assert.Equal(TrangThaiYeuCauXemPhong.DaXacNhanLich, viewing.TrangThai);
            Assert.Equal(tenantUserId, viewing.KhachVangLai!.NguoiDungId);
            Assert.Single(await new LichXemPhongStore(db).GetMineAsync(tenantUserId));
            Assert.Null(await new GiuChoStore(db).TryCreateAsync(code, viewing.YeuCauXemPhongId, otherStaffId));
            var holdId = await new GiuChoStore(db).TryCreateAsync(code, viewing.YeuCauXemPhongId, tenantUserId);
            Assert.NotNull(holdId);
            reservationId = holdId.Value;
            Assert.Equal(holdId, Assert.Single(await new GiuChoStore(db).GetMineAsync(tenantUserId)).Id);
            Assert.Equal(1, await db.KhachVangLais.CountAsync(item => item.NguoiDungId == tenantUserId));
        }

        int paymentId;
        await using (var db = fixture.CreateDbContext())
        {
            var due = DateTime.UtcNow.AddMinutes(30);
            Assert.True(await new GiuChoStore(db).TryApproveAsync(reservationId, due, due.AddDays(1), adminId));
            paymentId = await db.YeuCauThanhToanGiuChos.Where(item => item.YeuCauGiuChoId == reservationId)
                .Select(item => item.YeuCauThanhToanGiuChoId).SingleAsync();
            Assert.True(await new ThanhToanGiuChoStore(db).TrySubmitAsync(paymentId, tenantUserId,
                "tenant-proof-" + unique, new(1_000_000, DateTimeOffset.UtcNow, null, null), DateTime.UtcNow));
        }
        await using (var db = fixture.CreateDbContext())
        {
            var proofId = await db.MinhChungThanhToanGiuChos.Where(item => item.YeuCauThanhToanGiuChoId == paymentId)
                .Select(item => item.MinhChungThanhToanGiuChoId).SingleAsync();
            Assert.True(await new ThanhToanGiuChoStore(db).ConfirmAsync(proofId, adminId,
                "TENANT-BANK-" + unique, 1_000_000, DateTime.UtcNow));
        }
        await using (var db = fixture.CreateDbContext())
        {
            var originalTenantId = await db.NguoiDungs.Where(item => item.NguoiDungId == tenantUserId)
                .Select(item => item.NguoiThueId).SingleAsync();
            var contractId = await new ChuyenHopDongGiuChoStore(db).ConvertAsync(reservationId,
                new(DateTimeOffset.UtcNow, null, []), adminId);
            Assert.NotNull(contractId);
            Assert.Equal(originalTenantId, await db.HopDongs.Where(item => item.HopDongId == contractId)
                .Select(item => item.NguoiThueId).SingleAsync());
            Assert.Equal(Role.KhachThue, await db.NguoiDungs.Where(item => item.NguoiDungId == tenantUserId)
                .Select(item => item.Role).SingleAsync());
        }
    }

    [Fact]
    public async Task ActualBillingServiceCreditsPartialStartMonthAndCarriesRemainder()
    {
        var (holdId, _) = await CreatePaidHoldAsync();
        var local = DateTime.UtcNow.AddHours(7);
        var start = new DateTimeOffset(local.Year, local.Month,
            DateTime.DaysInMonth(local.Year, local.Month), 0, 0, 0, TimeSpan.FromHours(7));
        int contractId;
        await using (var db = fixture.CreateDbContext())
        {
            contractId = (await new ChuyenHopDongGiuChoStore(db).ConvertAsync(holdId, new(start, null, []), adminId))!.Value;
            foreach (var month in new[] { start, start.AddMonths(1) })
                db.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
                {
                    PhongTroId = roomId, Thang = month.Month, Nam = month.Year,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet, NguoiDuyetId = adminId,
                    NgayDuyet = DateTime.UtcNow, DonGiaDien = 3_000, DonGiaNuoc = 15_000
                });
            await db.SaveChangesAsync();
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString,
            ["BackgroundJobs:Enabled"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        services.AddApplication();
        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var billing = scope.ServiceProvider.GetRequiredService<IHoaDonService>();
            var preview = await billing.PreviewPhatSinhHoaDonAsync(branchId, start.Month, start.Year);
            var item = Assert.Single(preview);
            Assert.True(item.TienCocCanTruDuKien > 0);
            var result = await billing.PhatSinhHoaDonAsync(branchId, start.Month, start.Year, [roomId]);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Single(result.Successes);
        }
        var nextMonth = start.AddMonths(1);
        await using (var scope = provider.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IHoaDonService>()
                .PhatSinhHoaDonAsync(branchId, nextMonth.Month, nextMonth.Year, [roomId]);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Single(result.Successes);
        }
        await using var check = fixture.CreateDbContext();
        var invoices = await check.HoaDons.Where(item => item.HopDongId == contractId)
            .OrderBy(item => item.Nam).ThenBy(item => item.Thang).ToListAsync();
        Assert.Equal(2, invoices.Count);
        Assert.Equal(0, invoices[0].TongTien);
        Assert.InRange(invoices[1].TongTien, 1_000_001, 1_999_999);
        Assert.Equal(1_000_000, await check.CanTruTienGiuChoHoaDons
            .Where(item => item.HoaDon.HopDongId == contractId).SumAsync(item => item.SoTienCanTru));
    }

    [Fact]
    public async Task RefundAndContractConversionCannotBothAllocateSameMoney()
    {
        var (holdId, _) = await CreatePaidHoldAsync();
        var start = new DateTimeOffset(DateTime.SpecifyKind(DateTime.UtcNow.AddHours(7).Date,
            DateTimeKind.Unspecified), TimeSpan.FromHours(7));
        await using var refundDb = fixture.CreateDbContext();
        await using var contractDb = fixture.CreateDbContext();
        var refund = new HoanTienGiuChoStore(refundDb).DecideAsync(holdId, new(1_000_000, "Khách hủy"), adminId);
        var contract = new ChuyenHopDongGiuChoStore(contractDb).ConvertAsync(holdId, new(start, null, []), adminId);
        await Task.WhenAll(refund, contract);
        Assert.NotEqual(await refund, (await contract).HasValue);
        await using var check = fixture.CreateDbContext();
        var refunded = await check.QuyetDinhHoanTienGiuChos.Where(item => item.YeuCauGiuChoId == holdId)
            .SumAsync(item => item.SoTienHoanDuyet);
        var applied = await check.ApDungTienGiuChoVaoTienCocs.Where(item => item.YeuCauGiuChoId == holdId)
            .SumAsync(item => item.SoTienApDung);
        Assert.Equal(1_000_000, refunded + applied);
    }

    [Fact]
    public async Task ProcessedProofsCannotHideOlderPendingReconciliation()
    {
        var (holdId, paymentId) = await CreateApprovedHoldAsync();
        await using var db = fixture.CreateDbContext();
        Assert.True(await new ThanhToanGiuChoStore(db).TrySubmitAsync(paymentId, guestUserId,
            "pending-key", new(1_000_000, DateTimeOffset.UtcNow, null, null), DateTime.UtcNow));
        var pending = await db.MinhChungThanhToanGiuChos.SingleAsync(item => item.YeuCauThanhToanGiuChoId == paymentId);
        for (var i = 0; i < 101; i++)
            db.MinhChungThanhToanGiuChos.Add(new MinhChungThanhToanGiuCho
            {
                YeuCauThanhToanGiuChoId = paymentId, UrlHinhAnh = "processed-key-" + i,
                SoTienKhaiBao = 1_000_000, NgayChuyenTien = DateTime.UtcNow,
                NgayTao = DateTime.UtcNow.AddMinutes(1), TrangThai = TrangThaiMinhChungThanhToan.TuChoi
            });
        await db.SaveChangesAsync();
        Assert.Contains(await new ThanhToanGiuChoStore(db).GetReviewQueueAsync(adminId), item => item.Id == pending.MinhChungThanhToanGiuChoId);
        Assert.False(await new GiuChoStore(db).TryCloseUnpaidAsync(holdId, adminId, "Không được hủy khi chờ đối chiếu"));
    }

    [Fact]
    public async Task StaffRescheduleChoicesOnlyIncludeFreeSlotsAndSaveSelectedSlot()
    {
        var start = DateTime.UtcNow.AddDays(2);
        int firstSlotId, fullSlotId, freeSlotId;
        await using (var db = fixture.CreateDbContext())
        {
            var first = new KhungGioXemPhong { PhongTroId = roomId, NguoiTaoId = adminId,
                ThoiGianBatDau = start, ThoiGianKetThuc = start.AddHours(1), SoLuongToiDa = 1 };
            var full = new KhungGioXemPhong { PhongTroId = roomId, NguoiTaoId = adminId,
                ThoiGianBatDau = start.AddHours(2), ThoiGianKetThuc = start.AddHours(3), SoLuongToiDa = 1 };
            var free = new KhungGioXemPhong { PhongTroId = roomId, NguoiTaoId = adminId,
                ThoiGianBatDau = start.AddHours(4), ThoiGianKetThuc = start.AddHours(5), SoLuongToiDa = 1 };
            db.KhungGioXemPhongs.AddRange(first, full, free);
            await db.SaveChangesAsync();
            firstSlotId = first.KhungGioXemPhongId;
            fullSlotId = full.KhungGioXemPhongId;
            freeSlotId = free.KhungGioXemPhongId;
        }

        int firstViewingId, secondViewingId;
        await using (var db = fixture.CreateDbContext())
        {
            var store = new LichXemPhongStore(db);
            Assert.True(await store.CreateAsync(new(code, firstSlotId, start,
                "First visitor", "0900000001", null, null, null)));
            Assert.True(await store.CreateAsync(new(code, fullSlotId, start.AddHours(2),
                "Second visitor", "0900000002", null, null, null)));
            var viewings = await db.YeuCauXemPhongs.OrderBy(item => item.YeuCauXemPhongId).ToListAsync();
            firstViewingId = viewings[0].YeuCauXemPhongId;
            secondViewingId = viewings[1].YeuCauXemPhongId;
        }

        await using (var db = fixture.CreateDbContext())
        {
            var store = new LichXemPhongStore(db);
            Assert.Empty(await store.GetRescheduleSlotsAsync(firstViewingId, otherStaffId, DateTime.UtcNow));
            var choices = await store.GetRescheduleSlotsAsync(firstViewingId, adminId, DateTime.UtcNow);
            Assert.Equal(freeSlotId, Assert.Single(choices).Id);
            Assert.True(await store.ManageConfirmedAsync(firstViewingId, adminId,
                new(ViewingStaffAction.Reschedule, "Đã thống nhất", DateTime.UtcNow.AddYears(1), freeSlotId)));
        }

        await using (var db = fixture.CreateDbContext())
        {
            var changed = await db.YeuCauXemPhongs.SingleAsync(item => item.YeuCauXemPhongId == firstViewingId);
            Assert.Equal(freeSlotId, changed.KhungGioXemPhongId);
            var selectedStart = await db.KhungGioXemPhongs.Where(item => item.KhungGioXemPhongId == freeSlotId)
                .Select(item => item.ThoiGianBatDau).SingleAsync();
            Assert.Equal(selectedStart, changed.ThoiGianMongMuon);
            var store = new LichXemPhongStore(db);
            Assert.DoesNotContain(await store.GetRescheduleSlotsAsync(secondViewingId, adminId, DateTime.UtcNow),
                item => item.Id == freeSlotId);
            Assert.False(await store.ManageConfirmedAsync(secondViewingId, adminId,
                new(ViewingStaffAction.Reschedule, "Không còn chỗ", null, freeSlotId)));
        }
    }

    [Fact]
    public async Task StaffCanRescheduleCancelAndCompleteConfirmedViewings()
    {
        var when = DateTime.UtcNow.AddDays(2);
        await using var db = fixture.CreateDbContext();
        Assert.True(await new LichXemPhongStore(db).CreateAsync(new(code, null, when,
            "Test visitor", "0900000001", null, null, null)));
        var viewing = await db.YeuCauXemPhongs.SingleAsync(item => item.PhongTroId == roomId);
        var store = new LichXemPhongStore(db);
        Assert.True(await store.DecideAsync(viewing.YeuCauXemPhongId, adminId, true));
        Assert.False(await store.ManageConfirmedAsync(viewing.YeuCauXemPhongId, otherStaffId,
            new(ViewingStaffAction.Cancel, "Khách hủy", null)));
        Assert.True(await store.ManageConfirmedAsync(viewing.YeuCauXemPhongId, adminId,
            new(ViewingStaffAction.Reschedule, "Khách đổi lịch", when.AddHours(1))));
        Assert.Equal(when.AddHours(1), viewing.ThoiGianMongMuon);
        Assert.False(await store.ManageConfirmedAsync(viewing.YeuCauXemPhongId, adminId,
            new(ViewingStaffAction.Complete, "Đã xem", null)));
        Assert.True(await store.ManageConfirmedAsync(viewing.YeuCauXemPhongId, adminId,
            new(ViewingStaffAction.Cancel, "Khách hủy", null)));
        Assert.Equal(TrangThaiYeuCauXemPhong.DaHuy, viewing.TrangThai);
        Assert.Equal(4, await db.LichSuTrangThaiYeuCauXemPhongs.CountAsync(item => item.YeuCauXemPhongId == viewing.YeuCauXemPhongId));
        var past = new YeuCauXemPhong { PhongTroId = roomId, HoTen = "Past visitor", SoDienThoai = "0900000001",
            ThoiGianMongMuon = DateTime.UtcNow.AddHours(-1), TrangThai = TrangThaiYeuCauXemPhong.DaXacNhanLich };
        db.YeuCauXemPhongs.Add(past);
        await db.SaveChangesAsync();
        Assert.True(await store.ManageConfirmedAsync(past.YeuCauXemPhongId, adminId,
            new(ViewingStaffAction.Complete, "Khách đã đến xem", null)));
        Assert.Equal(TrangThaiYeuCauXemPhong.DaXemPhong, past.TrangThai);
    }

    [Fact]
    public async Task ExpiryConflictDiscardsTrackedChangesBeforeRetrying()
    {
        var (holdId, _) = await CreateApprovedHoldAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>(fixture.CreateDbContextOptions())
            .AddInterceptors(new FailFirstSave()).Options;
        await using var db = new ApplicationDbContext(options);
        var store = new HetHanGiuChoStore(db);
        var now = DateTime.UtcNow.AddHours(1);
        Assert.False(await store.TryExpireAsync(holdId, now));
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.True(await store.TryExpireAsync(holdId, now));
    }

    private sealed class FailFirstSave : SaveChangesInterceptor
    {
        private bool failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!failed)
            {
                failed = true;
                throw new PostgresException("Simulated serialization failure", "ERROR", "ERROR", "40001");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task ActiveContractUpdateCannotBypassApprovedGuestHold()
    {
        await CreateApprovedHoldAsync();
        await using var db = fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var contract = new HopDong { PhongTroId = roomId, TrangThaiHopDong = TrangThaiHopDong.DangHoatDong };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HopDongStore(db).UpdateAsync(contract));
    }

    [Fact]
    public async Task PaidHoldConvertsOnceAndCreditCarriesToNextInvoice()
    {
        var (holdId, _) = await CreatePaidHoldAsync();
        await using (var db = fixture.CreateDbContext())
        {
            var room = await db.PhongTros.FindAsync(roomId);
            room!.GiaThue = 3_000_000;
            await db.SaveChangesAsync();
        }
        var start = new DateTimeOffset(DateTime.SpecifyKind(DateTime.UtcNow.AddHours(7).Date,
            DateTimeKind.Unspecified), TimeSpan.FromHours(7));
        int contractId;
        await using (var db = fixture.CreateDbContext())
        {
            var store = new ChuyenHopDongGiuChoStore(db);
            var candidate = await store.GetCandidateAsync(holdId, adminId);
            Assert.NotNull(candidate);
            Assert.Equal(2_000_000, candidate.GiaThueDaChot);
            contractId = (await store.ConvertAsync(holdId, new(start, null, []), adminId))!.Value;
        }
        await using (var db = fixture.CreateDbContext())
        {
            Assert.Equal(contractId, await new ChuyenHopDongGiuChoStore(db)
                .ConvertAsync(holdId, new(start, null, []), adminId));
            Assert.False(await new HoanTienGiuChoStore(db).DecideAsync(holdId, new(100, "Không được"), adminId));
            Assert.Equal(Role.KhachThue, (await db.NguoiDungs.FindAsync(guestUserId))!.Role);
            Assert.Single(await db.ApDungTienGiuChoVaoTienCocs.Where(item => item.YeuCauGiuChoId == holdId).ToListAsync());
            Assert.Equal(2_000_000, (await db.HopDongs.FindAsync(contractId))!.TienThuePhong);
        }
        var first = await CreateInvoiceAsync(contractId, start, 300_000);
        var next = await CreateInvoiceAsync(contractId, start.AddMonths(1), 2_000_000);
        Assert.Equal(0, first.TongTien);
        Assert.Equal(TrangThaiHoaDon.DaThanhToan, first.TrangThaiHoaDon);
        Assert.Equal(1_300_000, next.TongTien);
        await using var check = fixture.CreateDbContext();
        Assert.Equal(1_000_000, await check.CanTruTienGiuChoHoaDons
            .Where(item => item.HoaDon.HopDongId == contractId).SumAsync(item => item.SoTienCanTru));
        Assert.NotEmpty(await new GiuChoStore(check).GetMineAsync(guestUserId));
        var progress = await new TheoDoiGiuChoStore(check).GetMineAsync(holdId, guestUserId);
        Assert.NotNull(progress);
        Assert.Equal(1_000_000, progress.DaCanTruHoaDon);
        Assert.Equal(0, progress.ConCanTru);
        Assert.Null(await new TheoDoiGiuChoStore(check).GetMineAsync(holdId, otherStaffId));
    }

    [Fact]
    public async Task RefundReleasesRoomAndCannotExceedDecisionOrRepeatBankCode()
    {
        var (holdId, _) = await CreatePaidHoldAsync();
        int decisionId;
        await using (var db = fixture.CreateDbContext())
        {
            var store = new HoanTienGiuChoStore(db);
            Assert.False(await store.DecideAsync(holdId, new(1_000_001, "Vượt tiền"), adminId));
            Assert.True(await store.DecideAsync(holdId, new(600_000, "Khách hủy"), adminId));
            decisionId = (await db.QuyetDinhHoanTienGiuChos.SingleAsync(item => item.YeuCauGiuChoId == holdId))
                .QuyetDinhHoanTienGiuChoId;
            Assert.Equal(TrangThaiYeuCauGiuCho.DaHuy, (await db.YeuCauGiuChos.FindAsync(holdId))!.TrangThai);
            Assert.Contains(await store.GetQueueAsync(adminId), item => item.ReservationId == holdId);
        }
        await using (var db = fixture.CreateDbContext())
        {
            var store = new HoanTienGiuChoStore(db);
            Assert.True(await store.RecordAsync(decisionId, new("REF-A-" + unique, 400_000, DateTimeOffset.UtcNow, null), adminId));
        }
        await using (var db = fixture.CreateDbContext())
        {
            var store = new HoanTienGiuChoStore(db);
            Assert.False(await store.RecordAsync(decisionId, new("REF-A-" + unique, 100_000, DateTimeOffset.UtcNow, null), adminId));
            Assert.False(await store.RecordAsync(decisionId, new("REF-B-" + unique, 300_000, DateTimeOffset.UtcNow, null), adminId));
            Assert.True(await store.RecordAsync(decisionId, new("REF-B-" + unique, 200_000, DateTimeOffset.UtcNow, null), adminId));
        }
        await using var check = fixture.CreateDbContext();
        var mine = await new HoanTienGiuChoStore(check).GetMineAsync(guestUserId);
        Assert.Equal(600_000, Assert.Single(mine).SoTienDaHoan);
        Assert.Equal("DaHoanTien", Assert.Single(mine).TrangThaiHoan);
    }

    [Fact]
    public async Task PendingProofKeepsRoomUntilStaffRejects()
    {
        var (holdId, paymentId) = await CreateApprovedHoldAsync();
        int proofId;
        DateTime expiry;
        await using (var db = fixture.CreateDbContext())
        {
            Assert.True(await new ThanhToanGiuChoStore(db).TrySubmitAsync(paymentId, guestUserId,
                "test-private-key", new(1_000_000, DateTimeOffset.UtcNow, null, null), DateTime.UtcNow));
            proofId = (await db.MinhChungThanhToanGiuChos.SingleAsync(item => item.YeuCauThanhToanGiuChoId == paymentId)).MinhChungThanhToanGiuChoId;
            expiry = (await db.YeuCauGiuChos.FindAsync(holdId))!.HanThanhToan!.Value.AddMinutes(6);
        }
        await using (var db = fixture.CreateDbContext())
            Assert.False(await new HetHanGiuChoStore(db).TryExpireAsync(holdId, expiry));
        await using (var db = fixture.CreateDbContext())
        {
            Assert.Null(await new ThanhToanGiuChoStore(db).GetAuthorizedProofKeyAsync(proofId, otherStaffId));
            Assert.False(await new ThanhToanGiuChoStore(db).RejectAsync(proofId, otherStaffId, "Không tìm thấy", 0, null, null));
            Assert.True(await new ThanhToanGiuChoStore(db).RejectAsync(proofId, adminId, "Không tìm thấy", 0, null, null));
        }
        await using var check = fixture.CreateDbContext();
        Assert.Equal(TrangThaiYeuCauGiuCho.TuChoi, (await check.YeuCauGiuChos.FindAsync(holdId))!.TrangThai);
    }

    [Fact]
    public async Task TwoApprovalsHaveOneWinnerAndOnePaymentRequest()
    {
        int holdId;
        await using (var db = fixture.CreateDbContext())
            holdId = (await new GiuChoStore(db).TryCreateAsync(code, null, guestUserId))!.Value;
        var due = DateTime.UtcNow.AddMinutes(30);
        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var results = await Task.WhenAll(new GiuChoStore(first).TryApproveAsync(holdId, due, due.AddDays(1), adminId),
            new GiuChoStore(second).TryApproveAsync(holdId, due, due.AddDays(1), adminId));
        Assert.Single(results.Where(item => item));
        await using var check = fixture.CreateDbContext();
        Assert.Single(await check.YeuCauThanhToanGiuChos.Where(item => item.YeuCauGiuChoId == holdId).ToListAsync());
    }

    [Fact]
    public async Task LastViewingPlaceHasOneWinnerAndStaffCanCloseSlot()
    {
        var start = DateTimeOffset.UtcNow.AddDays(1);
        int slotId;
        await using (var db = fixture.CreateDbContext())
        {
            var store = new KhungGioXemPhongStore(db);
            Assert.False(await store.CreateAsync(new(roomId, start, start.AddHours(1), 1), otherStaffId));
            Assert.True(await store.CreateAsync(new(roomId, start, start.AddHours(1), 1), adminId));
            slotId = (await db.KhungGioXemPhongs.SingleAsync(item => item.PhongTroId == roomId)).KhungGioXemPhongId;
            Assert.Contains(await store.GetManagedAsync(adminId), item => item.Id == slotId);
        }
        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var command = new CreateViewingCommand(code, slotId, start.UtcDateTime,
            "Anonymous Guest", "0900000001", null, null, null);
        var results = await Task.WhenAll(new LichXemPhongStore(first).CreateAsync(command),
            new LichXemPhongStore(second).CreateAsync(command));
        Assert.Single(results.Where(item => item));
        await using var check = fixture.CreateDbContext();
        Assert.True(await new KhungGioXemPhongStore(check).CloseAsync(slotId, adminId));
        Assert.Empty(await new LichXemPhongStore(check).GetOpenSlotsAsync(code, DateTime.UtcNow));
    }

    private async Task<(int HoldId, int PaymentId)> CreateApprovedHoldAsync()
    {
        int holdId;
        await using (var db = fixture.CreateDbContext())
            holdId = (await new GiuChoStore(db).TryCreateAsync(code, null, guestUserId))!.Value;
        await using var approve = fixture.CreateDbContext();
        var due = DateTime.UtcNow.AddMinutes(30);
        Assert.False(await new GiuChoStore(approve).TryApproveAsync(holdId, due, due.AddDays(1), otherStaffId));
        Assert.True(await new GiuChoStore(approve).TryApproveAsync(holdId, due, due.AddDays(1), adminId));
        var paymentId = await approve.YeuCauThanhToanGiuChos.Where(item => item.YeuCauGiuChoId == holdId)
            .Select(item => item.YeuCauThanhToanGiuChoId).SingleAsync();
        return (holdId, paymentId);
    }

    private async Task<(int HoldId, int PaymentId)> CreatePaidHoldAsync()
    {
        var ids = await CreateApprovedHoldAsync();
        int proofId;
        await using (var db = fixture.CreateDbContext())
        {
            Assert.True(await new ThanhToanGiuChoStore(db).TrySubmitAsync(ids.PaymentId, guestUserId,
                "test-private-key", new(1_000_000, DateTimeOffset.UtcNow, null, null), DateTime.UtcNow));
            proofId = await db.MinhChungThanhToanGiuChos.Where(item => item.YeuCauThanhToanGiuChoId == ids.PaymentId)
                .Select(item => item.MinhChungThanhToanGiuChoId).SingleAsync();
        }
        await using var confirm = fixture.CreateDbContext();
        Assert.True(await new ThanhToanGiuChoStore(confirm).ConfirmAsync(proofId, adminId,
            "BANK-" + unique, 1_000_000, DateTime.UtcNow));
        return ids;
    }

    private async Task<HoaDon> CreateInvoiceAsync(int contractId, DateTimeOffset month, decimal total)
    {
        await using var db = fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var invoice = new HoaDon
        {
            MaHoaDon = "J" + unique + month.Month, HopDongId = contractId,
            Thang = month.Month, Nam = month.Year, TongTien = total,
            ChiTietHoaDonDichVus = [new ChiTietHoaDon { TenDichVu = "Tiền phòng", DonGia = total, SoLuong = 1, TongTien = total }]
        };
        await new CanTruTienGiuChoStore(db).PrepareInvoicesAsync([invoice]);
        db.HoaDons.Add(invoice);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return invoice;
    }

    public async Task DisposeAsync()
    {
        if (branchId == 0) return;
        await using var db = fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var holds = db.YeuCauGiuChos.Where(item => item.PhongTroId == roomId).Select(item => item.YeuCauGiuChoId);
        var payments = db.YeuCauThanhToanGiuChos.Where(item => holds.Contains(item.YeuCauGiuChoId)).Select(item => item.YeuCauThanhToanGiuChoId);
        var decisions = db.QuyetDinhHoanTienGiuChos.Where(item => holds.Contains(item.YeuCauGiuChoId)).Select(item => item.QuyetDinhHoanTienGiuChoId);
        var contracts = db.HopDongs.Where(item => item.PhongTroId == roomId).Select(item => item.HopDongId);
        var invoices = db.HoaDons.Where(item => contracts.Contains(item.HopDongId)).Select(item => item.HoaDonId);
        var tenantIds = await db.NguoiDungs.Where(item =>
                (item.NguoiDungId == guestUserId || item.NguoiDungId == tenantUserId) && item.NguoiThueId != null)
            .Select(item => item.NguoiThueId!.Value).ToListAsync();
        await db.CanTruTienGiuChoHoaDons.Where(item => invoices.Contains(item.HoaDonId)).ExecuteDeleteAsync();
        await db.ChiTietHoaDons.Where(item => invoices.Contains(item.HoaDonId)).ExecuteDeleteAsync();
        await db.HoaDons.Where(item => invoices.Contains(item.HoaDonId)).ExecuteDeleteAsync();
        await db.DichVuDienNuocCuaPhongs.Where(item => item.PhongTroId == roomId).ExecuteDeleteAsync();
        await db.GiaoDichGiuChos.Where(item => payments.Contains(item.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
        await db.MinhChungThanhToanGiuChos.Where(item => payments.Contains(item.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
        await db.LichSuTrangThaiYeuCauThanhToanGiuChos.Where(item => payments.Contains(item.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
        await db.YeuCauThanhToanGiuChos.Where(item => payments.Contains(item.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
        await db.GiaoDichHoanTienGiuChos.Where(item => decisions.Contains(item.QuyetDinhHoanTienGiuChoId)).ExecuteDeleteAsync();
        await db.QuyetDinhHoanTienGiuChos.Where(item => decisions.Contains(item.QuyetDinhHoanTienGiuChoId)).ExecuteDeleteAsync();
        await db.ApDungTienGiuChoVaoTienCocs.Where(item => holds.Contains(item.YeuCauGiuChoId)).ExecuteDeleteAsync();
        await db.ChiTietThanhVienHopDongs.Where(item => contracts.Contains(item.HopDongId)).ExecuteDeleteAsync();
        await db.HopDongDieuKhoans.Where(item => contracts.Contains(item.HopDongId)).ExecuteDeleteAsync();
        await db.HopDongs.Where(item => contracts.Contains(item.HopDongId)).ExecuteDeleteAsync();
        await db.LichSuTrangThaiYeuCauGiuChos.Where(item => holds.Contains(item.YeuCauGiuChoId)).ExecuteDeleteAsync();
        await db.YeuCauGiuChos.Where(item => holds.Contains(item.YeuCauGiuChoId)).ExecuteDeleteAsync();
        var viewings = db.YeuCauXemPhongs.Where(item => item.PhongTroId == roomId).Select(item => item.YeuCauXemPhongId);
        await db.LichSuTrangThaiYeuCauXemPhongs.Where(item => viewings.Contains(item.YeuCauXemPhongId)).ExecuteDeleteAsync();
        await db.YeuCauXemPhongs.Where(item => item.PhongTroId == roomId).ExecuteDeleteAsync();
        await db.KhungGioXemPhongs.Where(item => item.PhongTroId == roomId).ExecuteDeleteAsync();
        await db.PhongTros.Where(item => item.PhongTroId == roomId).ExecuteDeleteAsync();
        await db.KhachVangLais.Where(item => item.NguoiDungId == guestUserId || item.NguoiDungId == tenantUserId).ExecuteDeleteAsync();
        await db.NguoiDungs.Where(item => item.NguoiDungId == guestUserId || item.NguoiDungId == tenantUserId || item.NguoiDungId == adminId || item.NguoiDungId == otherStaffId).ExecuteDeleteAsync();
        await db.NguoiThues.Where(item => tenantIds.Contains(item.NguoiThueId)).ExecuteDeleteAsync();
        await db.ChiNhanhs.Where(item => item.ChiNhanhId == branchId).ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }
}
