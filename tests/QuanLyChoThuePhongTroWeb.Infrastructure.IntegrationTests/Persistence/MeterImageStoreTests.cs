using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class MeterImageStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public MeterImageStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task PersistAndReload_ImageAndPeriod_StoresUrlPublicIdAndRelationships()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN_{suffix}",
                MaChiNhanh = $"C_{suffix}",
                DiaChi = "123 Street",
                SoDienThoai = "0900000000",
                MoTa = "Mo ta chi nhanh"
            };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"P_{suffix}",
                GiaThue = 1000000m,
                DienTich = 20,
                MoTa = "Mo ta phong"
            };
            context.PhongTros.Add(room);
            await context.SaveChangesAsync();

            var store = new MeterImageStore(context);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 100,
                DonGiaDien = 3500,
                ChiSoNuocCu = 50,
                ChiSoNuocMoi = 50,
                DonGiaNuoc = 20000,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            await store.AddPeriodRecordAsync(period);
            await context.SaveChangesAsync();

            var image = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://res.cloudinary.com/test/image/upload/meter.jpg",
                PublicId = "meters/sample_public_id",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.MoiTaiLen
            };
            await store.AddImageAsync(image);
            await context.SaveChangesAsync();

            var loadedImage = await store.GetImageByIdAsync(image.AnhChiSoDongHoId);
            Assert.NotNull(loadedImage);
            Assert.Equal("meters/sample_public_id", loadedImage.PublicId);
            Assert.Equal(image.Url, loadedImage.Url);
            Assert.NotNull(loadedImage.DichVuDienNuocCuaPhong);
            Assert.Equal(room.PhongTroId, loadedImage.DichVuDienNuocCuaPhong.PhongTroId);
        }

        [Fact]
        public async Task GetBranchIdByRoomAsync_ReturnsCorrectBranchId()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
            context.PhongTros.Add(room);
            await context.SaveChangesAsync();

            var store = new MeterImageStore(context);
            var branchId = await store.GetBranchIdByRoomAsync(room.PhongTroId);

            Assert.Equal(branch.ChiNhanhId, branchId);

            var notFound = await store.GetBranchIdByRoomAsync(-999);
            Assert.Null(notFound);
        }

        [Fact]
        public async Task HasActiveContractForTenantInPeriodAsync_ValidatesCorrectTenantAndDates()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
            context.PhongTros.Add(room);

            var tenant = new NguoiThue { HoVaTen = $"T_{suffix}", Email = $"t_{suffix}@test.com", SoDienThoai = "0901", CCCD = $"001_{suffix}" };
            context.NguoiThues.Add(tenant);
            await context.SaveChangesAsync();

            var user = new NguoiDung { TenDangNhap = $"u_{suffix}", MatKhauHash = "h", Role = Role.KhachThue, NguoiThueId = tenant.NguoiThueId };
            context.NguoiDungs.Add(user);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m,
                ThoiDiemBatDau = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            var store = new MeterImageStore(context);

            var hasInOct = await store.HasActiveContractForTenantInPeriodAsync(room.PhongTroId, user.NguoiDungId, 10, 2026);
            Assert.True(hasInOct);

            var hasInAug = await store.HasActiveContractForTenantInPeriodAsync(room.PhongTroId, user.NguoiDungId, 8, 2026);
            Assert.False(hasInAug);

            var hasOtherUser = await store.HasActiveContractForTenantInPeriodAsync(room.PhongTroId, 99999, 10, 2026);
            Assert.False(hasOtherUser);
        }

        [Fact]
        public async Task HasActiveContractForTenantInPeriodAsync_IdCollision_DoesNotAuthorizeWrongUser()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
            context.PhongTros.Add(room);
            await context.SaveChangesAsync();

            // Tạo người thuê A và User A (User A có NguoiThueId = tenantA.NguoiThueId)
            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", Email = $"ta_{suffix}@test.com", SoDienThoai = "0901", CCCD = $"001_{suffix}" };
            context.NguoiThues.Add(tenantA);
            await context.SaveChangesAsync();

            var userA = new NguoiDung { TenDangNhap = $"ua_{suffix}", MatKhauHash = "h", Role = Role.KhachThue, NguoiThueId = tenantA.NguoiThueId, IsActive = true };
            context.NguoiDungs.Add(userA);
            await context.SaveChangesAsync();

            // Giả lập ID collision: Tạo Tenant B có NguoiThueId trùng đúng NguoiDungId của User A
            int collidedId = userA.NguoiDungId;
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nguoi_thue (\"NguoiThueId\", \"HoVaTen\", \"SoDienThoai\", \"Email\", \"CCCD\", \"IsDeleted\", \"NgayTao\") VALUES ({collidedId}, {$"TB_{suffix}"}, '0902', {$"tb_{suffix}@test.com"}, {$"002_{suffix}"}, false, NOW());");

            // Phòng có hợp đồng thuộc về tenantB (NguoiThueId = collidedId)
            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = collidedId,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m,
                ThoiDiemBatDau = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            var store = new MeterImageStore(context);

            // userA KHÔNG PHẢI là người thuê của hợp đồng này vì NguoiThueId của họ là tenantA, không phải tenantB
            var isAuth = await store.HasActiveContractForTenantInPeriodAsync(room.PhongTroId, userA.NguoiDungId, 10, 2026);
            Assert.False(isAuth, "ID Collision: User có NguoiDungId trùng NguoiThueId của hợp đồng không được phép upload nếu NguoiThueId của họ khác!");
        }

        [Fact]
        public async Task HasActiveContractForTenantInPeriodAsync_InactiveOrWrongRoleOrDeleted_ReturnsFalse()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
            context.PhongTros.Add(room);
            await context.SaveChangesAsync();

            var tenant = new NguoiThue { HoVaTen = $"T_{suffix}", Email = $"t_{suffix}@test.com", SoDienThoai = "0901", CCCD = $"001_{suffix}" };
            context.NguoiThues.Add(tenant);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m,
                ThoiDiemBatDau = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            var store = new MeterImageStore(context);

            // 1. Inactive user
            var inactiveUser = new NguoiDung { TenDangNhap = $"inact_{suffix}", MatKhauHash = "h", Role = Role.KhachThue, NguoiThueId = tenant.NguoiThueId, IsActive = false };
            context.NguoiDungs.Add(inactiveUser);
            await context.SaveChangesAsync();
            Assert.False(await store.HasActiveContractForTenantInPeriodAsync(room.PhongTroId, inactiveUser.NguoiDungId, 10, 2026));

            // 2. Wrong role (NhanVien có NguoiThueId nhưng không có branch permission)
            var wrongRoleUser = new NguoiDung { TenDangNhap = $"staff_{suffix}", MatKhauHash = "h", Role = Role.NhanVien, NguoiThueId = tenant.NguoiThueId, IsActive = true };
            context.NguoiDungs.Add(wrongRoleUser);
            await context.SaveChangesAsync();
            Assert.False(await store.HasActiveContractForTenantInPeriodAsync(room.PhongTroId, wrongRoleUser.NguoiDungId, 10, 2026));

            // 3. Member in ChiTietThanhVienHopDong
            var memberTenant = new NguoiThue { HoVaTen = $"M_{suffix}", Email = $"m_{suffix}@test.com", SoDienThoai = "0902", CCCD = $"002_{suffix}" };
            context.NguoiThues.Add(memberTenant);
            await context.SaveChangesAsync();

            var memberUser = new NguoiDung { TenDangNhap = $"mem_{suffix}", MatKhauHash = "h", Role = Role.KhachThue, NguoiThueId = memberTenant.NguoiThueId, IsActive = true };
            context.NguoiDungs.Add(memberUser);

            var memberDetail = new ChiTietThanhVienHopDong { HopDongId = contract.HopDongId, NguoiThueId = memberTenant.NguoiThueId };
            context.ChiTietThanhVienHopDongs.Add(memberDetail);
            await context.SaveChangesAsync();

            Assert.True(await store.HasActiveContractForTenantInPeriodAsync(room.PhongTroId, memberUser.NguoiDungId, 10, 2026));
        }

        [Fact]
        public async Task HasSubsequentPeriodAsync_And_HasNonDraftInvoiceAsync_DetectLockConditions()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
            context.PhongTros.Add(room);
            await context.SaveChangesAsync();

            var tenant = new NguoiThue { HoVaTen = $"T_{suffix}", Email = $"t_{suffix}@test.com", SoDienThoai = "0901", CCCD = $"001_{suffix}" };
            context.NguoiThues.Add(tenant);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                MaHopDong = $"HD_{suffix}",
                ThoiDiemBatDau = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                TienCocPhong = 1000m,
                TienThuePhong = 1000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            var period9 = new DichVuDienNuocCuaPhong { PhongTroId = room.PhongTroId, Thang = 9, Nam = 2026 };
            var period10 = new DichVuDienNuocCuaPhong { PhongTroId = room.PhongTroId, Thang = 10, Nam = 2026 };
            context.DichVuDienNuocCuaPhongs.AddRange(period9, period10);
            await context.SaveChangesAsync();

            var store = new MeterImageStore(context);

            // Kỳ sau chỉ là bản nháp: không khóa kỳ trước
            Assert.False(await store.HasSubsequentPeriodAsync(room.PhongTroId, 9, 2026));
            Assert.False(await store.HasSubsequentPeriodAsync(room.PhongTroId, 10, 2026));

            // Kỳ sau chỉ có hóa đơn nháp hoặc đã hủy: vẫn không khóa
            var activeInvoice = new HoaDon { MaHoaDon = $"N_{suffix}", HopDongId = contract.HopDongId, DichVuDienNuocCuaPhongId = period10.DichVuDienNuocCuaPhongId, Thang = 10, Nam = 2026, TongTien = 1, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap };
            context.HoaDons.AddRange(
                activeInvoice,
                new HoaDon { MaHoaDon = $"H_{suffix}", HopDongId = contract.HopDongId, DichVuDienNuocCuaPhongId = period10.DichVuDienNuocCuaPhongId, Thang = 10, Nam = 2026, TongTien = 1, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy, IsDeleted = true });
            await context.SaveChangesAsync();
            Assert.False(await store.HasSubsequentPeriodAsync(room.PhongTroId, 9, 2026));

            // Kỳ sau đã DaDuyet: khóa mọi kỳ trước nó
            period10.TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet;
            await context.SaveChangesAsync();
            Assert.True(await store.HasSubsequentPeriodAsync(room.PhongTroId, 9, 2026));
            Assert.False(await store.HasSubsequentPeriodAsync(room.PhongTroId, 10, 2026));

            // Kỳ sau còn nháp nhưng có hóa đơn đã chốt: khóa
            period10.TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap;
            activeInvoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot;
            await context.SaveChangesAsync();
            Assert.True(await store.HasSubsequentPeriodAsync(room.PhongTroId, 9, 2026));
        }

        [Fact]
        public async Task DienNuocStore_GetLockedRoomIdsAsync_OnlyLocksWhenLaterPeriodIsApproved()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
            context.PhongTros.Add(room);
            await context.SaveChangesAsync();

            var period9 = new DichVuDienNuocCuaPhong { PhongTroId = room.PhongTroId, Thang = 9, Nam = 2026, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet };
            var period10 = new DichVuDienNuocCuaPhong { PhongTroId = room.PhongTroId, Thang = 10, Nam = 2026, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap };
            context.DichVuDienNuocCuaPhongs.AddRange(period9, period10);
            await context.SaveChangesAsync();

            var store = new DienNuocStore(context);
            var rooms = new[] { room.PhongTroId };

            Assert.DoesNotContain(room.PhongTroId, await store.GetLockedRoomIdsAsync(rooms, 9, 2026));

            period10.TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet;
            await context.SaveChangesAsync();
            Assert.Contains(room.PhongTroId, await store.GetLockedRoomIdsAsync(rooms, 9, 2026));
        }

        [Fact]
        public async Task GetSubsequentPeriodsForUpdateAsync_ReturnsLaterPeriodsInOrder_ExcludingDeletedAndOtherRooms()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
            var otherRoom = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"Q_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
            context.PhongTros.AddRange(room, otherRoom);
            await context.SaveChangesAsync();

            context.DichVuDienNuocCuaPhongs.AddRange(
                new DichVuDienNuocCuaPhong { PhongTroId = room.PhongTroId, Thang = 11, Nam = 2026 },
                new DichVuDienNuocCuaPhong { PhongTroId = room.PhongTroId, Thang = 8, Nam = 2026 },
                new DichVuDienNuocCuaPhong { PhongTroId = room.PhongTroId, Thang = 10, Nam = 2026 },
                new DichVuDienNuocCuaPhong { PhongTroId = room.PhongTroId, Thang = 12, Nam = 2026, IsDeleted = true },
                new DichVuDienNuocCuaPhong { PhongTroId = room.PhongTroId, Thang = 1, Nam = 2027 },
                new DichVuDienNuocCuaPhong { PhongTroId = otherRoom.PhongTroId, Thang = 10, Nam = 2026 });
            await context.SaveChangesAsync();

            var store = new MeterImageStore(context);
            var result = await store.GetSubsequentPeriodsForUpdateAsync(room.PhongTroId, 8, 2026);

            Assert.Equal(new[] { (10, 2026), (11, 2026), (1, 2027) }, result.Select(p => (p.Thang, p.Nam)).ToArray());
        }

        [Fact]
        public async Task OfficialImage_UniqueConstraint_PreventsTwoOfficialImagesForSamePeriodAndType()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
            context.PhongTros.Add(room);

            var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin };
            context.NguoiDungs.Add(admin);
            await context.SaveChangesAsync();

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = 9,
                Nam = 2026,
                DonGiaDien = 3000,
                DonGiaNuoc = 15000
            };
            context.DichVuDienNuocCuaPhongs.Add(period);
            await context.SaveChangesAsync();

            var img1 = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://url1",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc
            };
            img1.XacNhan(100m, admin.NguoiDungId);

            var img2 = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://url2",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc
            };
            img2.XacNhan(120m, admin.NguoiDungId);

            context.AnhChiSoDongHos.Add(img1);
            await context.SaveChangesAsync();

            context.AnhChiSoDongHos.Add(img2);
            await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
            {
                await context.SaveChangesAsync();
            });
        }

        internal class FakeStorageService : IMeterImageStorageService
        {
            public Task<MeterImageUploadResult> UploadAsync(QuanLyChoThuePhongTroWeb.Application.Common.Files.UploadFile file, string folder, CancellationToken cancellationToken = default) => Task.FromResult(new MeterImageUploadResult("https://storage.test/img.jpg", "meters/sample_public_id"));
            public Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default) => Task.FromResult(new MeterImageReadResult(new byte[] { 1, 2, 3 }, "image/jpeg"));
            public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        internal class FakeOcrService : IMeterOcrService
        {
            public Task<MeterOcrResult> ProcessImageAsync(byte[] imageBytes, string contentType, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _utcNow;
            public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
            public override DateTimeOffset GetUtcNow() => _utcNow;
        }

        internal static MeterReadingWorkflowService CreateWorkflowService(
            ApplicationDbContext context,
            IMeterImageStorageService? storage = null,
            IMeterOcrService? ocr = null,
            IEmployeeAccessService? access = null,
            IHoaDonStore? hoaDonStore = null,
            IHoaDonCalculatorService? calculator = null,
            MeterImageOptions? options = null,
            TimeProvider? timeProvider = null)
        {
            return new MeterReadingWorkflowService(
                new MeterImageStore(context),
                storage ?? new FakeStorageService(),
                ocr ?? new FakeOcrService(),
                access ?? new EmployeeAccessService(new EmployeeBranchStore(context)),
                new EfUnitOfWork(context),
                hoaDonStore ?? new HoaDonStore(context),
                calculator ?? new HoaDonCalculatorService(),
                NullLogger<MeterReadingWorkflowService>.Instance,
                options ?? new MeterImageOptions(),
                timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 3, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public async Task ConcurrentConfirmImage_OnPostgreSql_EnsuresSingleOfficialImageAndConsistentPeriodRecord()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId;
            int periodId;
            int img1Id;
            int img2Id;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
                seedCtx.PhongTros.Add(room);

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();

                adminId = admin.NguoiDungId;

                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId,
                    Thang = 9,
                    Nam = 2026,
                    ChiSoDienCu = 100,
                    ChiSoDienMoi = 100,
                    ChiSoNuocCu = 50,
                    ChiSoNuocMoi = 50,
                    DonGiaDien = 3000,
                    DonGiaNuoc = 15000
                };
                seedCtx.DichVuDienNuocCuaPhongs.Add(period);
                await seedCtx.SaveChangesAsync();
                periodId = period.DichVuDienNuocCuaPhongId;

                var img1 = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "http://url1",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                    GiaTriAIGoiY = 150
                };
                var img2 = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "http://url2",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                    GiaTriAIGoiY = 160
                };
                seedCtx.AnhChiSoDongHos.AddRange(img1, img2);
                await seedCtx.SaveChangesAsync();
                img1Id = img1.AnhChiSoDongHoId;
                img2Id = img2.AnhChiSoDongHoId;
            }

            var task1 = Task.Run(async () =>
            {
                await using var ctx1 = _fixture.CreateDbContext();
                var svc1 = CreateWorkflowService(ctx1);

                return await svc1.ConfirmImageAsync(new ConfirmMeterImageRequest
                {
                    AnhChiSoDongHoId = img1Id,
                    GiaTriXacNhan = 150
                }, adminId);
            });

            var task2 = Task.Run(async () =>
            {
                await using var ctx2 = _fixture.CreateDbContext();
                var svc2 = CreateWorkflowService(ctx2);

                return await svc2.ConfirmImageAsync(new ConfirmMeterImageRequest
                {
                    AnhChiSoDongHoId = img2Id,
                    GiaTriXacNhan = 160
                }, adminId);
            });

            var results = await Task.WhenAll(task1, task2);

            // Kiểm tra: ít nhất một request thành công
            Assert.True(results[0].Success || results[1].Success);

            // Xác minh DB trên context mới
            await using var verifyCtx = _fixture.CreateDbContext();
            var officialImages = await verifyCtx.AnhChiSoDongHos
                .Where(x => x.DichVuDienNuocCuaPhongId == periodId &&
                            x.LoaiDongHo == LoaiDongHo.Dien &&
                            x.DuocChonLamChiSoChinhThuc &&
                            !x.IsDeleted)
                .ToListAsync();

            // BẮT BUỘC: Chỉ có duy nhất đúng 1 ảnh chính thức cho loại đồng hồ này
            Assert.Single(officialImages);

            var reloadPeriod = await verifyCtx.DichVuDienNuocCuaPhongs.FindAsync(periodId);
            Assert.NotNull(reloadPeriod);
            Assert.Equal(officialImages[0].GiaTriXacNhan, reloadPeriod.ChiSoDienMoi);
        }

        [Fact]
        public async Task Concurrent_ConfirmElectricAndWater_BothSucceedAndKeepBothReadings()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId, periodId, dienImgId, nuocImgId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
                seedCtx.PhongTros.Add(room);
                await seedCtx.SaveChangesAsync();

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin, IsActive = true };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();
                adminId = admin.NguoiDungId;

                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId,
                    Thang = 9,
                    Nam = 2026,
                    ChiSoDienCu = 100,
                    ChiSoDienMoi = 100,
                    ChiSoNuocCu = 50,
                    ChiSoNuocMoi = 50,
                    DonGiaDien = 3000,
                    DonGiaNuoc = 15000,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
                };
                seedCtx.DichVuDienNuocCuaPhongs.Add(period);
                await seedCtx.SaveChangesAsync();
                periodId = period.DichVuDienNuocCuaPhongId;

                var dienImg = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "http://dien-url",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                    GiaTriAIGoiY = 180
                };
                var nuocImg = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Nuoc,
                    Url = "http://nuoc-url",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                    GiaTriAIGoiY = 90
                };
                seedCtx.AnhChiSoDongHos.AddRange(dienImg, nuocImg);
                await seedCtx.SaveChangesAsync();
                dienImgId = dienImg.AnhChiSoDongHoId;
                nuocImgId = nuocImg.AnhChiSoDongHoId;
            }

            var task1 = Task.Run(async () =>
            {
                await using var ctx1 = _fixture.CreateDbContext();
                var svc1 = CreateWorkflowService(ctx1);

                return await svc1.ConfirmImageAsync(new ConfirmMeterImageRequest
                {
                    AnhChiSoDongHoId = dienImgId,
                    GiaTriXacNhan = 180
                }, adminId);
            });

            var task2 = Task.Run(async () =>
            {
                await using var ctx2 = _fixture.CreateDbContext();
                var svc2 = CreateWorkflowService(ctx2);

                return await svc2.ConfirmImageAsync(new ConfirmMeterImageRequest
                {
                    AnhChiSoDongHoId = nuocImgId,
                    GiaTriXacNhan = 90
                }, adminId);
            });

            var results = await Task.WhenAll(task1, task2);

            Assert.True(results[0].Success, $"Task 1 (Điện) failed: {results[0].Message}");
            Assert.True(results[1].Success, $"Task 2 (Nước) failed: {results[1].Message}");

            await using var verifyCtx = _fixture.CreateDbContext();
            var finalPeriod = await verifyCtx.DichVuDienNuocCuaPhongs.FindAsync(periodId);
            Assert.NotNull(finalPeriod);
            Assert.Equal(180, finalPeriod.ChiSoDienMoi);
            Assert.Equal(90, finalPeriod.ChiSoNuocMoi);
        }

        [Fact]
        public async Task HasLockedInvoiceAsync_WhenOnlyCancelledInvoice_ReturnsFalse()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
            context.PhongTros.Add(room);
            await context.SaveChangesAsync();

            var tenant = new NguoiThue { HoVaTen = $"T_{suffix}", Email = $"t_{suffix}@test.com", SoDienThoai = "0901", CCCD = $"001_{suffix}" };
            context.NguoiThues.Add(tenant);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                MaHopDong = $"HD_{suffix}",
                ThoiDiemBatDau = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc),
                TienCocPhong = 1000m,
                TienThuePhong = 1000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 10,
                ChiSoDienMoi = 20,
                DonGiaDien = 3000,
                ChiSoNuocCu = 5,
                ChiSoNuocMoi = 10,
                DonGiaNuoc = 15000,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            context.DichVuDienNuocCuaPhongs.Add(period);
            await context.SaveChangesAsync();

            var invoice = new HoaDon
            {
                MaHoaDon = $"INV_{suffix}",
                HopDongId = contract.HopDongId,
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 50000,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true
            };
            context.HoaDons.Add(invoice);
            await context.SaveChangesAsync();

            var store = new MeterImageStore(context);
            var isLocked = await store.HasNonDraftInvoiceAsync(period.DichVuDienNuocCuaPhongId);

            Assert.False(isLocked, "Hóa đơn đã hủy không khóa kỳ chỉ số để nhân viên sửa số và tạo bản thay thế.");

            context.HoaDons.Add(new HoaDon
            {
                MaHoaDon = $"INV_{suffix}-R1",
                HopDongId = contract.HopDongId,
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 50000,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui
            });
            await context.SaveChangesAsync();

            Assert.True(await store.HasNonDraftInvoiceAsync(period.DichVuDienNuocCuaPhongId),
                "Bản thay thế đã gửi phải khóa kỳ chỉ số trở lại.");
        }

        [Fact]
        public async Task RetryOcr_WhenImageConfirmedConcurrently_Phase3DoesNotOverwriteConfirmedState()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId, periodId, imgId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta" };
                seedCtx.PhongTros.Add(room);
                await seedCtx.SaveChangesAsync();

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin, IsActive = true };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();
                adminId = admin.NguoiDungId;

                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId,
                    Thang = 9,
                    Nam = 2026,
                    ChiSoDienCu = 100,
                    ChiSoDienMoi = 100,
                    ChiSoNuocCu = 50,
                    ChiSoNuocMoi = 50,
                    DonGiaDien = 3000,
                    DonGiaNuoc = 15000,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
                };
                seedCtx.DichVuDienNuocCuaPhongs.Add(period);
                await seedCtx.SaveChangesAsync();
                periodId = period.DichVuDienNuocCuaPhongId;

                var img = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "http://test-url",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.KhongDocDuoc
                };
                seedCtx.AnhChiSoDongHos.Add(img);
                await seedCtx.SaveChangesAsync();
                imgId = img.AnhChiSoDongHoId;
            }

            var enteredPhase2Tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var proceedPhase3Tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var controllingOcr = new CallbackOcrService(async () =>
            {
                enteredPhase2Tcs.TrySetResult(true);
                await proceedPhase3Tcs.Task;
                return MeterOcrResult.Readable(150m, 0.95);
            });

            var retryTask = Task.Run(async () =>
            {
                await using var ctx = _fixture.CreateDbContext();
                await ctx.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var svc = CreateWorkflowService(ctx, ocr: controllingOcr);

                return await svc.RetryOcrAsync(imgId, adminId);
            });

            // Chờ Retry hoàn thành Pha 1 và dừng ở Pha 2
            await enteredPhase2Tcs.Task;

            // Trong lúc Retry đang ở Pha 2, một thao tác khác xác nhận ảnh thành DaXacNhan và là ảnh chính thức
            await using (var confirmCtx = _fixture.CreateDbContext())
            {
                await confirmCtx.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var imgToConfirm = await confirmCtx.AnhChiSoDongHos.FindAsync(imgId);
                var periodToConfirm = await confirmCtx.DichVuDienNuocCuaPhongs.FindAsync(periodId);
                imgToConfirm!.TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan;
                imgToConfirm.GiaTriXacNhan = 180m;
                imgToConfirm.NguoiXacNhanId = adminId;
                imgToConfirm.NgayXacNhan = DateTime.UtcNow;
                imgToConfirm.GhiChuXacNhan = "Xác nhận số thủ công tại phòng";
                imgToConfirm.DuocChonLamChiSoChinhThuc = true;
                periodToConfirm!.ChiSoDienMoi = 180m;
                await confirmCtx.SaveChangesAsync();
            }

            // Thả Retry bước vào Pha 3
            proceedPhase3Tcs.TrySetResult(true);
            var retryResult = await retryTask;

            // Pha 3 phát hiện trạng thái ảnh đã đổi -> trả Fail an toàn
            Assert.False(retryResult.Success);
            Assert.Contains("thao tác khác", retryResult.Message.ToLowerInvariant());

            // Đọc lại từ DB xác nhận cờ chính thức và số xác nhận không bị ghi đè
            await using var verifyCtx = _fixture.CreateDbContext();
            var finalImg = await verifyCtx.AnhChiSoDongHos.FindAsync(imgId);
            var finalPeriod = await verifyCtx.DichVuDienNuocCuaPhongs.FindAsync(periodId);

            Assert.NotNull(finalImg);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, finalImg.TrangThaiXuLy);
            Assert.Equal(180m, finalImg.GiaTriXacNhan);
            Assert.True(finalImg.DuocChonLamChiSoChinhThuc);
            Assert.NotEqual(150m, finalImg.GiaTriAIGoiY); // Kết quả 150 của OCR bị bỏ qua
            Assert.Equal(180m, finalPeriod!.ChiSoDienMoi);
        }

        [Fact]
        public async Task RetryOcr_WhenTwoRetriesRunSequentiallyOrStale_OnlyMatchingStampApplies()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId, periodId, imgId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta" };
                seedCtx.PhongTros.Add(room);
                await seedCtx.SaveChangesAsync();

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin, IsActive = true };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();
                adminId = admin.NguoiDungId;

                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId,
                    Thang = 9,
                    Nam = 2026,
                    ChiSoDienCu = 100,
                    ChiSoDienMoi = 100,
                    ChiSoNuocCu = 50,
                    ChiSoNuocMoi = 50,
                    DonGiaDien = 3000,
                    DonGiaNuoc = 15000,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
                };
                seedCtx.DichVuDienNuocCuaPhongs.Add(period);
                await seedCtx.SaveChangesAsync();
                periodId = period.DichVuDienNuocCuaPhongId;

                var img = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "http://test-url",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.KhongDocDuoc
                };
                seedCtx.AnhChiSoDongHos.Add(img);
                await seedCtx.SaveChangesAsync();
                imgId = img.AnhChiSoDongHoId;
            }

            var enteredPhase2_1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var proceedPhase3_1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var enteredPhase2_2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var proceedPhase3_2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var ocrCallCount = 0;
            var dualOcr = new CallbackOcrService(async () =>
            {
                var count = Interlocked.Increment(ref ocrCallCount);
                if (count == 1)
                {
                    enteredPhase2_1.TrySetResult(true);
                    await proceedPhase3_1.Task;
                    return MeterOcrResult.Readable(150m, 0.90);
                }
                else
                {
                    enteredPhase2_2.TrySetResult(true);
                    await proceedPhase3_2.Task;
                    return MeterOcrResult.Readable(160m, 0.95);
                }
            });

            // Bắt đầu Retry 1
            var retryTask1 = Task.Run(async () =>
            {
                await using var ctx1 = _fixture.CreateDbContext();
                await ctx1.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var svc1 = CreateWorkflowService(ctx1, ocr: dualOcr);

                return await svc1.RetryOcrAsync(imgId, adminId);
            });

            // Đợi Retry 1 vào Pha 2
            await enteredPhase2_1.Task;

            // Mô phỏng: ảnh bị kẹt quá hạn StaleProcessingMinutes (15 phút trước)
            await using (var staleCtx = _fixture.CreateDbContext())
            {
                await staleCtx.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var imgStale = await staleCtx.AnhChiSoDongHos.FindAsync(imgId);
                imgStale!.NgayXuLy = DateTime.UtcNow.AddMinutes(-15);
                await staleCtx.SaveChangesAsync();
            }

            // Retry 2 bắt đầu vì ảnh đã quá hạn stale timeout
            var retryTask2 = Task.Run(async () =>
            {
                await using var ctx2 = _fixture.CreateDbContext();
                await ctx2.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var svc2 = CreateWorkflowService(ctx2, ocr: dualOcr);

                return await svc2.RetryOcrAsync(imgId, adminId);
            });

            // Đợi Retry 2 vào Pha 2 (Retry 2 đã chiếm stamp mới)
            await enteredPhase2_2.Task;

            // Thả Retry 1 vào Pha 3 trước
            proceedPhase3_1.TrySetResult(true);
            var result1 = await retryTask1;

            // Retry 1 phải Fail vì stamp không khớp
            Assert.False(result1.Success);
            Assert.Contains("thao tác khác", result1.Message.ToLowerInvariant());

            // Thả Retry 2 vào Pha 3
            proceedPhase3_2.TrySetResult(true);
            var result2 = await retryTask2;

            // Retry 2 phải Thành công vì stamp của nó khớp
            Assert.True(result2.Success);

            // Kiểm tra DB: giá trị gợi ý là 160m (của Retry 2)
            await using var verifyCtx = _fixture.CreateDbContext();
            var finalImg = await verifyCtx.AnhChiSoDongHos.FindAsync(imgId);
            Assert.NotNull(finalImg);
            Assert.Equal(160m, finalImg.GiaTriAIGoiY);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DocDuoc, finalImg.TrangThaiXuLy);
        }

        private class CallbackOcrService : IMeterOcrService
        {
            private readonly Func<Task<MeterOcrResult>> _callback;

            public CallbackOcrService(Func<Task<MeterOcrResult>> callback)
            {
                _callback = callback;
            }

            public Task<MeterOcrResult> ProcessImageAsync(byte[] imageBytes, string contentType, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
            {
                return _callback();
            }
        }

        [Fact]
        public async Task LockRoomsAsync_WhenRoomDeletedOrNotExists_ThrowsInvalidOperationException()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int validRoomId, deletedRoomId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var roomValid = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
                var roomDeleted = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P2_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong", IsDeleted = true };
                seedCtx.PhongTros.AddRange(roomValid, roomDeleted);
                await seedCtx.SaveChangesAsync();

                validRoomId = roomValid.PhongTroId;
                deletedRoomId = roomDeleted.PhongTroId;
            }

            await using var ctx = _fixture.CreateDbContext();
            await ctx.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
            var store = new MeterImageStore(ctx);

            // Kiểm tra trường hợp phòng đã bị soft-delete
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.LockRoomsAsync(new[] { validRoomId, deletedRoomId }));

            // Kiểm tra trường hợp phòng không tồn tại
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.LockRoomsAsync(new[] { validRoomId, 999999 }));
        }

        private async Task WaitForBlockedLocksAsync(int expectedWaitingCount, TimeSpan timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                await using var checkCtx = _fixture.CreateDbContext();
                using var cmd = checkCtx.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT count(*) FROM pg_locks l JOIN pg_stat_activity a ON a.pid = l.pid WHERE NOT l.granted AND a.datname = current_database();";
                await checkCtx.Database.OpenConnectionAsync();
                var result = await cmd.ExecuteScalarAsync();
                var count = Convert.ToInt32(result);
                if (count >= expectedWaitingCount)
                {
                    return;
                }
                // Nhịp thăm dò ngắn cố định 25ms để chờ tiến trình rơi vào trạng thái chờ khóa (NOT granted) trên pg_locks
                await Task.Delay(25);
            }

            throw new TimeoutException($"Hết thời gian chờ {timeout.TotalSeconds}s nhưng số tiến trình bị chặn trên pg_locks không đạt, kỳ vọng >= {expectedWaitingCount}.");
        }

        [Fact]
        public async Task UploadImage_ConcurrentUploadsForSameRoomAndPeriod_CreatesSinglePeriodRecord()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId, roomId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
                seedCtx.PhongTros.Add(room);
                await seedCtx.SaveChangesAsync();
                roomId = room.PhongTroId;

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin, IsActive = true };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();
                adminId = admin.NguoiDungId;
            }

            var dummyOcr = new CallbackOcrService(() => Task.FromResult(MeterOcrResult.Readable(100m, 0.9)));

            // 1. Mở kết nối holder giữ khóa phòng trọ
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
            await holderCtx.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM phong_tro WHERE \"PhongTroId\" = {roomId} FOR UPDATE;");

            // 2. Khởi động hai tác vụ upload chạy ngầm
            var uploadTask1 = Task.Run(async () =>
            {
                await using var ctx1 = _fixture.CreateDbContext();
                await ctx1.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var svc1 = CreateWorkflowService(ctx1, ocr: dummyOcr);

                var file = new QuanLyChoThuePhongTroWeb.Application.Common.Files.UploadFile(new MemoryStream(new byte[] { 1, 2 }), "dien.jpg", "image/jpeg", 2);
                return await svc1.UploadImageAsync(new UploadMeterImageRequest
                {
                    PhongTroId = roomId,
                    Thang = 10,
                    Nam = 2026,
                    LoaiDongHo = LoaiDongHo.Dien,
                    File = file
                }, adminId);
            });

            var uploadTask2 = Task.Run(async () =>
            {
                await using var ctx2 = _fixture.CreateDbContext();
                await ctx2.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var svc2 = CreateWorkflowService(ctx2, ocr: dummyOcr);

                var file = new QuanLyChoThuePhongTroWeb.Application.Common.Files.UploadFile(new MemoryStream(new byte[] { 1, 2 }), "nuoc.jpg", "image/jpeg", 2);
                return await svc2.UploadImageAsync(new UploadMeterImageRequest
                {
                    PhongTroId = roomId,
                    Thang = 10,
                    Nam = 2026,
                    LoaiDongHo = LoaiDongHo.Nuoc,
                    File = file
                }, adminId);
            });

            // 3. Chờ theo điều kiện trên pg_locks cho đến khi cả 2 tiến trình đều đang chờ khóa
            await WaitForBlockedLocksAsync(expectedWaitingCount: 2, timeout: TimeSpan.FromSeconds(10));

            // 4. Giải phóng khóa ở holder để hai tiến trình đồng thời tranh chấp
            await holderTx.CommitAsync();

            var results = await Task.WhenAll(uploadTask1, uploadTask2);

            Assert.True(results[0].Success, $"Upload điện thất bại: {results[0].Message}");
            Assert.True(results[1].Success, $"Upload nước thất bại: {results[1].Message}");

            // 5. Kiểm tra DB: chỉ có duy nhất 1 bản ghi kỳ được tạo và cả 2 ảnh trỏ cùng 1 kỳ
            await using var verifyCtx = _fixture.CreateDbContext();
            var periods = await verifyCtx.DichVuDienNuocCuaPhongs
                .Where(p => p.PhongTroId == roomId && p.Thang == 10 && p.Nam == 2026 && !p.IsDeleted)
                .ToListAsync();

            Assert.Single(periods);
            var periodId = periods[0].DichVuDienNuocCuaPhongId;

            var images = await verifyCtx.AnhChiSoDongHos
                .Where(a => a.DichVuDienNuocCuaPhongId == periodId && !a.IsDeleted)
                .ToListAsync();

            Assert.Equal(2, images.Count);
        }

        [Fact]
        public async Task UploadImage_ConcurrentWithApprovePeriods_DoesNotThrowUniqueViolation_AndProducesSinglePeriod()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId, roomId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
                seedCtx.PhongTros.Add(room);
                await seedCtx.SaveChangesAsync();
                roomId = room.PhongTroId;

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin, IsActive = true };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();
                adminId = admin.NguoiDungId;
            }

            var dummyOcr = new CallbackOcrService(() => Task.FromResult(MeterOcrResult.Readable(100m, 0.9)));

            // 1. Mở kết nối holder giữ khóa phòng trọ
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
            await holderCtx.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM phong_tro WHERE \"PhongTroId\" = {roomId} FOR UPDATE;");

            // 2. Khởi động tác vụ upload và approve đồng thời
            var uploadTask = Task.Run(async () =>
            {
                await using var ctx1 = _fixture.CreateDbContext();
                await ctx1.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var svc1 = CreateWorkflowService(ctx1, ocr: dummyOcr);

                var file = new QuanLyChoThuePhongTroWeb.Application.Common.Files.UploadFile(new MemoryStream(new byte[] { 1, 2 }), "dien.jpg", "image/jpeg", 2);
                return await svc1.UploadImageAsync(new UploadMeterImageRequest
                {
                    PhongTroId = roomId,
                    Thang = 10,
                    Nam = 2026,
                    LoaiDongHo = LoaiDongHo.Dien,
                    File = file
                }, adminId);
            });

            var approveTask = Task.Run(async () =>
            {
                await using var ctx2 = _fixture.CreateDbContext();
                await ctx2.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var svc2 = CreateWorkflowService(ctx2, ocr: dummyOcr);

                return await svc2.ApprovePeriodsAsync(new ApproveMeterPeriodsRequest
                {
                    Thang = 10,
                    Nam = 2026,
                    DanhSachPhong = new List<ApproveMeterPeriodItem>
                    {
                        new()
                        {
                            PhongTroId = roomId,
                            DienMode = MeterReadingSubmissionMode.Manual,
                            ChiSoDienMoiThuCong = 100m,
                            LyDoDienThuCong = "Chốt trực tiếp",
                            NuocMode = MeterReadingSubmissionMode.Manual,
                            ChiSoNuocMoiThuCong = 50m,
                            LyDoNuocThuCong = "Chốt trực tiếp"
                        }
                    }
                }, adminId);
            });

            // 3. Chờ theo điều kiện trên pg_locks cho đến khi cả 2 tiến trình đều đang chờ khóa
            await WaitForBlockedLocksAsync(expectedWaitingCount: 2, timeout: TimeSpan.FromSeconds(10));

            // 4. Giải phóng khóa ở holder
            await holderTx.CommitAsync();

            var uploadRes = await uploadTask;
            var approveRes = await approveTask;

            // 5. Kiểm tra DB: không có lỗi unique và chỉ có duy nhất 1 kỳ
            await using var verifyCtx = _fixture.CreateDbContext();
            var periods = await verifyCtx.DichVuDienNuocCuaPhongs
                .Where(p => p.PhongTroId == roomId && p.Thang == 10 && p.Nam == 2026 && !p.IsDeleted)
                .ToListAsync();

            Assert.Single(periods);
        }

        [Fact]
        public async Task ApprovePeriods_ConcurrentBatchesWithOppositeRoomOrder_DoNotDeadlock()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId;
            int roomAId;
            int roomBId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var roomA = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"PA_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta" };
                var roomB = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"PB_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta" };
                seedCtx.PhongTros.AddRange(roomA, roomB);

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();

                adminId = admin.NguoiDungId;
                roomAId = Math.Min(roomA.PhongTroId, roomB.PhongTroId);
                roomBId = Math.Max(roomA.PhongTroId, roomB.PhongTroId);
            }

            // 1. Mở kết nối holder giữ khóa cả 2 phòng trọ theo thứ tự ID tăng dần
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
            await holderCtx.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM phong_tro WHERE \"PhongTroId\" IN ({roomAId}, {roomBId}) ORDER BY \"PhongTroId\" FOR UPDATE;");

            // 2. Khởi động 2 batch duyệt với thứ tự phòng ngược nhau
            var batch1Task = Task.Run(async () =>
            {
                await using var ctx1 = _fixture.CreateDbContext();
                await ctx1.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var svc1 = CreateWorkflowService(ctx1);

                return await svc1.ApprovePeriodsAsync(new ApproveMeterPeriodsRequest
                {
                    Thang = 11,
                    Nam = 2026,
                    DanhSachPhong = new List<ApproveMeterPeriodItem>
                    {
                        new() { PhongTroId = roomAId, DienMode = MeterReadingSubmissionMode.Manual, ChiSoDienMoiThuCong = 10, LyDoDienThuCong = "Batch 1 A", NuocMode = MeterReadingSubmissionMode.Manual, ChiSoNuocMoiThuCong = 5, LyDoNuocThuCong = "Batch 1 A" },
                        new() { PhongTroId = roomBId, DienMode = MeterReadingSubmissionMode.Manual, ChiSoDienMoiThuCong = 20, LyDoDienThuCong = "Batch 1 B", NuocMode = MeterReadingSubmissionMode.Manual, ChiSoNuocMoiThuCong = 10, LyDoNuocThuCong = "Batch 1 B" }
                    }
                }, adminId);
            });

            var batch2Task = Task.Run(async () =>
            {
                await using var ctx2 = _fixture.CreateDbContext();
                await ctx2.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                var svc2 = CreateWorkflowService(ctx2);

                // Thứ tự phòng ngược lại: roomBId trước, roomAId sau
                return await svc2.ApprovePeriodsAsync(new ApproveMeterPeriodsRequest
                {
                    Thang = 11,
                    Nam = 2026,
                    DanhSachPhong = new List<ApproveMeterPeriodItem>
                    {
                        new() { PhongTroId = roomBId, DienMode = MeterReadingSubmissionMode.Manual, ChiSoDienMoiThuCong = 25, LyDoDienThuCong = "Batch 2 B", NuocMode = MeterReadingSubmissionMode.Manual, ChiSoNuocMoiThuCong = 12, LyDoNuocThuCong = "Batch 2 B" },
                        new() { PhongTroId = roomAId, DienMode = MeterReadingSubmissionMode.Manual, ChiSoDienMoiThuCong = 15, LyDoDienThuCong = "Batch 2 A", NuocMode = MeterReadingSubmissionMode.Manual, ChiSoNuocMoiThuCong = 7, LyDoNuocThuCong = "Batch 2 A" }
                    }
                }, adminId);
            });

            // 3. Chờ theo điều kiện trên pg_locks cho đến khi cả 2 tiến trình đều đang chờ khóa
            await WaitForBlockedLocksAsync(expectedWaitingCount: 2, timeout: TimeSpan.FromSeconds(10));

            // 4. Giải phóng khóa ở holder
            await holderTx.CommitAsync();

            var results = await Task.WhenAll(batch1Task, batch2Task);

            // Cả hai batch đều thành công không bị deadlock exception
            Assert.True(results[0].Success, $"Batch 1 thất bại: {results[0].Message}");
            Assert.True(results[1].Success, $"Batch 2 thất bại: {results[1].Message}");

            // 5. Kiểm tra DB dữ liệu cuối cùng hợp lệ
            await using var verifyCtx = _fixture.CreateDbContext();
            var periodA = await verifyCtx.DichVuDienNuocCuaPhongs.FirstOrDefaultAsync(p => p.PhongTroId == roomAId && p.Thang == 11 && p.Nam == 2026 && !p.IsDeleted);
            var periodB = await verifyCtx.DichVuDienNuocCuaPhongs.FirstOrDefaultAsync(p => p.PhongTroId == roomBId && p.Thang == 11 && p.Nam == 2026 && !p.IsDeleted);
            Assert.NotNull(periodA);
            Assert.NotNull(periodB);
        }

        [Fact]
        public async Task ConfirmImage_RollbackAfterClearingOldOfficialImage_RestoresOldOfficialImageOnPostgreSql()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId;
            int imgOldId;
            int periodId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta" };
                seedCtx.PhongTros.Add(room);

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();

                adminId = admin.NguoiDungId;

                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId,
                    Thang = 10,
                    Nam = 2026,
                    ChiSoDienCu = 100,
                    ChiSoDienMoi = 150,
                    ChiSoNuocCu = 50,
                    ChiSoNuocMoi = 60,
                    DonGiaDien = 3000,
                    DonGiaNuoc = 15000,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
                };
                seedCtx.DichVuDienNuocCuaPhongs.Add(period);
                await seedCtx.SaveChangesAsync();
                periodId = period.DichVuDienNuocCuaPhongId;

                var imgOld = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "https://res.cloudinary.com/test/old.jpg",
                    PublicId = "old_public_id",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                    GiaTriXacNhan = 150m,
                    DuocChonLamChiSoChinhThuc = true,
                    NguoiXacNhanId = adminId,
                    NgayXacNhan = DateTime.UtcNow
                };
                seedCtx.AnhChiSoDongHos.Add(imgOld);
                await seedCtx.SaveChangesAsync();
                imgOldId = imgOld.AnhChiSoDongHoId;
            }

            // Thực hiện transaction: bỏ cờ imgOld, nhưng sau đó rollback
            await using (var rollbackCtx = _fixture.CreateDbContext())
            {
                await using var tx = await rollbackCtx.Database.BeginTransactionAsync();
                var imgToModify = await rollbackCtx.AnhChiSoDongHos.FindAsync(imgOldId);
                Assert.NotNull(imgToModify);
                imgToModify.DuocChonLamChiSoChinhThuc = false;
                await rollbackCtx.SaveChangesAsync();

                // Rollback transaction mà không commit
                await tx.RollbackAsync();
            }

            // Kiểm tra lại trên PostgreSQL: imgOld vẫn giữ cờ chính thức true
            await using var verifyCtx = _fixture.CreateDbContext();
            var verifiedImg = await verifyCtx.AnhChiSoDongHos.FindAsync(imgOldId);
            Assert.NotNull(verifiedImg);
            Assert.True(verifiedImg.DuocChonLamChiSoChinhThuc);
        }

        [Fact]
        public async Task UploadAndConfirmImage_InSameDbContextScope_SucceedsWithoutTrackingConflict()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId, roomId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
                seedCtx.PhongTros.Add(room);
                await seedCtx.SaveChangesAsync();
                roomId = room.PhongTroId;

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin, IsActive = true };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();
                adminId = admin.NguoiDungId;
            }

            var dummyOcr = new CallbackOcrService(() => Task.FromResult(MeterOcrResult.Readable(150m, 0.95)));

            // Dùng một DbContext scope duy nhất cho cả Upload và Confirm
            await using var singleScopeCtx = _fixture.CreateDbContext();
            var svc = CreateWorkflowService(singleScopeCtx, ocr: dummyOcr);

            var file = new QuanLyChoThuePhongTroWeb.Application.Common.Files.UploadFile(new MemoryStream(new byte[] { 1, 2 }), "dien.jpg", "image/jpeg", 2);
            var uploadRes = await svc.UploadImageAsync(new UploadMeterImageRequest
            {
                PhongTroId = roomId,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            }, adminId);

            Assert.True(uploadRes.Success, $"Upload thất bại: {uploadRes.Message}");
            var imgId = uploadRes.Data!.AnhChiSoDongHoId;

            // Confirm ngay trên cùng DbContext scope
            var confirmRes = await svc.ConfirmImageAsync(new ConfirmMeterImageRequest
            {
                AnhChiSoDongHoId = imgId,
                GiaTriXacNhan = 150m
            }, adminId);

            Assert.True(confirmRes.Success, $"Confirm thất bại: {confirmRes.Message}");

            // Kiểm tra DB: có đúng 1 ảnh chính thức với giá trị xác nhận 150m
            await using var verifyCtx = _fixture.CreateDbContext();
            var finalImg = await verifyCtx.AnhChiSoDongHos.FindAsync(imgId);
            Assert.NotNull(finalImg);
            Assert.True(finalImg.DuocChonLamChiSoChinhThuc);
            Assert.Equal(150m, finalImg.GiaTriXacNhan);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, finalImg.TrangThaiXuLy);
        }

        [Fact]
        public async Task CorrectConfirmedImage_ConcurrentWithConfirmAnotherImage_ResultsInSingleOfficialImage()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId, roomId, periodId, img1Id, img2Id;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
                seedCtx.PhongTros.Add(room);
                await seedCtx.SaveChangesAsync();
                roomId = room.PhongTroId;

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin, IsActive = true };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();
                adminId = admin.NguoiDungId;

                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = roomId,
                    Thang = 10,
                    Nam = 2026,
                    ChiSoDienCu = 100m,
                    ChiSoDienMoi = 150m,
                    ChiSoNuocCu = 50m,
                    ChiSoNuocMoi = 70m,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
                };
                seedCtx.DichVuDienNuocCuaPhongs.Add(period);
                await seedCtx.SaveChangesAsync();
                periodId = period.DichVuDienNuocCuaPhongId;

                var img1 = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "https://res.cloudinary.com/test/img1.jpg",
                    PublicId = "img1_pub",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                    DuocChonLamChiSoChinhThuc = true,
                    GiaTriXacNhan = 150m,
                    NguoiXacNhanId = adminId,
                    NgayXacNhan = DateTime.UtcNow
                };
                var img2 = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "https://res.cloudinary.com/test/img2.jpg",
                    PublicId = "img2_pub",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                    DuocChonLamChiSoChinhThuc = false,
                    GiaTriAIGoiY = 170m
                };
                seedCtx.AnhChiSoDongHos.AddRange(img1, img2);
                await seedCtx.SaveChangesAsync();
                img1Id = img1.AnhChiSoDongHoId;
                img2Id = img2.AnhChiSoDongHoId;
            }

            // 1. Dùng TaskCompletionSource ép thứ tự: Sửa ảnh 1 hoàn tất rồi Confirm ảnh 2
            var dummyOcr = new CallbackOcrService(() => Task.FromResult(MeterOcrResult.Readable(170m, 0.95)));
            var tcsTask1Done = new TaskCompletionSource<bool>();

            var task1 = Task.Run(async () =>
            {
                await using var ctx1 = _fixture.CreateDbContext();
                var svc1 = CreateWorkflowService(ctx1, ocr: dummyOcr);
                var res = await svc1.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(img1Id, 180m, "Sửa lại số điện"), adminId);
                tcsTask1Done.SetResult(true);
                return res;
            });

            var task2 = Task.Run(async () =>
            {
                await tcsTask1Done.Task;
                await using var ctx2 = _fixture.CreateDbContext();
                var svc2 = CreateWorkflowService(ctx2, ocr: dummyOcr);
                return await svc2.ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = img2Id, GiaTriXacNhan = 170m }, adminId);
            });

            var results = await Task.WhenAll(task1, task2);

            Assert.True(results[0].Success, $"Task 1 thất bại: {results[0].Message}");
            Assert.True(results[1].Success, $"Task 2 thất bại: {results[1].Message}");

            // 2. Kiểm tra DB: đúng 1 ảnh là chính thức và số trên kỳ khớp ảnh đó
            await using var verifyCtx = _fixture.CreateDbContext();
            var verifiedImages = await verifyCtx.AnhChiSoDongHos
                .Where(a => a.DichVuDienNuocCuaPhongId == periodId && !a.IsDeleted)
                .ToListAsync();

            var officialImages = verifiedImages.Where(a => a.DuocChonLamChiSoChinhThuc).ToList();
            Assert.Single(officialImages);
            var theOfficial = officialImages[0];
            Assert.Equal(img2Id, theOfficial.AnhChiSoDongHoId);

            var verifiedPeriod = await verifyCtx.DichVuDienNuocCuaPhongs.FindAsync(periodId);
            Assert.NotNull(verifiedPeriod);
            Assert.Equal(theOfficial.GiaTriXacNhan, verifiedPeriod.ChiSoDienMoi);
        }

        [Fact]
        public async Task CorrectConfirmedImage_WhenInvoiceBecomesNonDraftDuringTransaction_FailsAndDbUnchanged()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            int adminId, roomId, periodId, imgId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
                seedCtx.ChiNhanhs.Add(branch);
                await seedCtx.SaveChangesAsync();

                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "Mo ta phong" };
                seedCtx.PhongTros.Add(room);
                await seedCtx.SaveChangesAsync();
                roomId = room.PhongTroId;

                var tenant = new NguoiThue { HoVaTen = "Tenant", CCCD = $"C_{suffix}", SoDienThoai = "0900", Email = $"t_{suffix}@test.com" };
                seedCtx.NguoiThues.Add(tenant);
                await seedCtx.SaveChangesAsync();

                var contract = new HopDong
                {
                    MaHopDong = $"HD_{suffix}",
                    PhongTroId = roomId,
                    NguoiThueId = tenant.NguoiThueId,
                    TienThuePhong = 1000m,
                    ThoiDiemBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
                };
                seedCtx.HopDongs.Add(contract);
                await seedCtx.SaveChangesAsync();

                var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin, IsActive = true };
                seedCtx.NguoiDungs.Add(admin);
                await seedCtx.SaveChangesAsync();
                adminId = admin.NguoiDungId;

                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = roomId,
                    Thang = 10,
                    Nam = 2026,
                    ChiSoDienCu = 100m,
                    ChiSoDienMoi = 150m,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
                };
                seedCtx.DichVuDienNuocCuaPhongs.Add(period);
                await seedCtx.SaveChangesAsync();
                periodId = period.DichVuDienNuocCuaPhongId;

                var img = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "https://res.cloudinary.com/test/img.jpg",
                    PublicId = "img_pub",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                    DuocChonLamChiSoChinhThuc = true,
                    GiaTriXacNhan = 150m,
                    NguoiXacNhanId = adminId,
                    NgayXacNhan = DateTime.UtcNow
                };
                seedCtx.AnhChiSoDongHos.Add(img);

                // Thêm hóa đơn đã phát hành (không còn là Nhap)
                var invoice = new HoaDon
                {
                    HopDongId = contract.HopDongId,
                    DichVuDienNuocCuaPhongId = periodId,
                    Thang = 10,
                    Nam = 2026,
                    TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                    MaHoaDon = $"HD_{suffix}"
                };
                seedCtx.HoaDons.Add(invoice);
                await seedCtx.SaveChangesAsync();
                imgId = img.AnhChiSoDongHoId;
            }

            await using var actionCtx = _fixture.CreateDbContext();
            var svc = CreateWorkflowService(actionCtx);

            var result = await svc.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(imgId, 180m, "Thử sửa khi hóa đơn đã chốt"), adminId);

            Assert.False(result.Success);
            Assert.Contains("hóa đơn", result.Message.ToLowerInvariant());

            // Kiểm tra DB không đổi
            await using var verifyCtx = _fixture.CreateDbContext();
            var verifyImg = await verifyCtx.AnhChiSoDongHos.FindAsync(imgId);
            Assert.NotNull(verifyImg);
            Assert.Equal(150m, verifyImg.GiaTriXacNhan);

            var verifyPeriod = await verifyCtx.DichVuDienNuocCuaPhongs.FindAsync(periodId);
            Assert.NotNull(verifyPeriod);
            Assert.Equal(150m, verifyPeriod.ChiSoDienMoi);
        }
    }
}
