using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class MeterImageQueryStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public MeterImageQueryStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<(ChiNhanh branch, PhongTro room, NguoiDung tenantUser, NguoiThue tenant, NguoiDung staffUser)> SeedBasicHierarchyAsync(ApplicationDbContext context, string suffix)
        {
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

            var tenant = new NguoiThue
            {
                HoVaTen = $"Khach_{suffix}",
                SoDienThoai = $"09{suffix.Substring(0, 8)}",
                Email = $"khach_{suffix}@example.com",
                CCCD = $"079{suffix.Substring(0, 8)}"
            };
            context.NguoiThues.Add(tenant);
            await context.SaveChangesAsync();

            var tenantUser = new NguoiDung
            {
                TenDangNhap = $"user_khach_{suffix}",
                MatKhauHash = "hash",
                Role = Role.KhachThue,
                IsActive = true,
                NguoiThueId = tenant.NguoiThueId
            };
            context.NguoiDungs.Add(tenantUser);

            var staffUser = new NguoiDung
            {
                TenDangNhap = $"user_staff_{suffix}",
                MatKhauHash = "hash",
                Role = Role.NhanVien,
                IsActive = true
            };
            context.NguoiDungs.Add(staffUser);

            await context.SaveChangesAsync();
            return (branch, room, tenantUser, tenant, staffUser);
        }

        [Fact]
        public async Task HasContractInMonthAsync_And_HasActiveContractForTenantInPeriodAsync_TimeBoundaries()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var (branch, room, tenantUser, tenant, _) = await SeedBasicHierarchyAsync(context, suffix);

            var meterImageStore = new MeterImageStore(context);

            // Contract 1: Starts exactly at 2026-09-30T17:00:00Z (01/10 00:00 VN)
            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 12, 31, 17, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000000m,
                TienCocPhong = 1000000m
            };
            context.HopDongs.Add(contract1);
            await context.SaveChangesAsync();

            // Contract 1 is active in 10/2026, NOT in 9/2026
            Assert.True(await meterImageStore.HasContractInMonthAsync(room.PhongTroId, 10, 2026));
            Assert.False(await meterImageStore.HasContractInMonthAsync(room.PhongTroId, 9, 2026));

            Assert.True(await meterImageStore.HasActiveContractForTenantInPeriodAsync(room.PhongTroId, tenantUser.NguoiDungId, 10, 2026));
            Assert.False(await meterImageStore.HasActiveContractForTenantInPeriodAsync(room.PhongTroId, tenantUser.NguoiDungId, 9, 2026));

            // Contract 2 in a second room: ends exactly at 2026-09-30T16:59:59Z
            var room2 = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"P2_{suffix}",
                GiaThue = 1000000m,
                DienTich = 20,
                MoTa = "Mo ta phong 2"
            };
            context.PhongTros.Add(room2);
            await context.SaveChangesAsync();

            var contract2 = new HopDong
            {
                MaHopDong = $"HD2_{suffix}",
                PhongTroId = room2.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 9, 30, 16, 59, 59, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000000m,
                TienCocPhong = 1000000m
            };
            context.HopDongs.Add(contract2);
            await context.SaveChangesAsync();

            // Contract 2 is active in 9/2026, NOT in 10/2026
            Assert.True(await meterImageStore.HasContractInMonthAsync(room2.PhongTroId, 9, 2026));
            Assert.False(await meterImageStore.HasContractInMonthAsync(room2.PhongTroId, 10, 2026));

            // Cancelled or deleted contracts are not counted
            contract2.TrangThaiHopDong = TrangThaiHopDong.DaHuy;
            await context.SaveChangesAsync();
            Assert.False(await meterImageStore.HasContractInMonthAsync(room2.PhongTroId, 9, 2026));
        }

        [Fact]
        public async Task HasActiveContractForTenantInPeriodAsync_IncludesContractMembers()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var (branch, room, tenantUser, mainTenant, _) = await SeedBasicHierarchyAsync(context, suffix);

            // Member tenant & user
            var memberTenant = new NguoiThue
            {
                HoVaTen = $"ThanhVien_{suffix}",
                SoDienThoai = $"08{suffix.Substring(0, 8)}",
                Email = $"member_{suffix}@example.com",
                CCCD = $"088{suffix.Substring(0, 8)}"
            };
            context.NguoiThues.Add(memberTenant);
            await context.SaveChangesAsync();

            var memberUser = new NguoiDung
            {
                TenDangNhap = $"user_member_{suffix}",
                MatKhauHash = "hash",
                Role = Role.KhachThue,
                IsActive = true,
                NguoiThueId = memberTenant.NguoiThueId
            };
            context.NguoiDungs.Add(memberUser);

            var contract = new HopDong
            {
                MaHopDong = $"HD_mem_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = mainTenant.NguoiThueId,
                ThoiDiemBatDau = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 12, 31, 17, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000000m,
                TienCocPhong = 1000000m
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            context.ChiTietThanhVienHopDongs.Add(new ChiTietThanhVienHopDong
            {
                HopDongId = contract.HopDongId,
                NguoiThueId = memberTenant.NguoiThueId
            });
            await context.SaveChangesAsync();

            var meterImageStore = new MeterImageStore(context);
            Assert.True(await meterImageStore.HasActiveContractForTenantInPeriodAsync(room.PhongTroId, memberUser.NguoiDungId, 10, 2026));
        }

        [Fact]
        public async Task GetTenantRoomsAsync_RepresentativeAndMemberSeeRoom_ExcludesUnrelatedAndCancelled()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var (branch, room, tenantUser, mainTenant, _) = await SeedBasicHierarchyAsync(context, suffix);

            var memberTenant = new NguoiThue
            {
                HoVaTen = $"Member_{suffix}",
                SoDienThoai = $"07{suffix.Substring(0, 8)}",
                Email = $"mem_{suffix}@example.com",
                CCCD = $"077{suffix.Substring(0, 8)}"
            };
            context.NguoiThues.Add(memberTenant);
            await context.SaveChangesAsync();

            var memberUser = new NguoiDung
            {
                TenDangNhap = $"u_mem_{suffix}",
                MatKhauHash = "hash",
                Role = Role.KhachThue,
                IsActive = true,
                NguoiThueId = memberTenant.NguoiThueId
            };
            context.NguoiDungs.Add(memberUser);

            var otherTenant = new NguoiThue
            {
                HoVaTen = $"Other_{suffix}",
                SoDienThoai = $"06{suffix.Substring(0, 8)}",
                Email = $"other_{suffix}@example.com",
                CCCD = $"066{suffix.Substring(0, 8)}"
            };
            context.NguoiThues.Add(otherTenant);
            await context.SaveChangesAsync();

            var otherUser = new NguoiDung
            {
                TenDangNhap = $"u_other_{suffix}",
                MatKhauHash = "hash",
                Role = Role.KhachThue,
                IsActive = true,
                NguoiThueId = otherTenant.NguoiThueId
            };
            context.NguoiDungs.Add(otherUser);

            var contract = new HopDong
            {
                MaHopDong = $"HD_room_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = mainTenant.NguoiThueId,
                ThoiDiemBatDau = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000000m,
                TienCocPhong = 1000000m
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            context.ChiTietThanhVienHopDongs.Add(new ChiTietThanhVienHopDong
            {
                HopDongId = contract.HopDongId,
                NguoiThueId = memberTenant.NguoiThueId
            });
            await context.SaveChangesAsync();

            var queryStore = new MeterImageQueryStore(context);
            var (startUtc, endUtc) = MeterPeriodPolicy.MonthRangeUtc(new MeterPeriod(10, 2026));

            // Main tenant sees room
            var mainRooms = await queryStore.GetTenantRoomsAsync(tenantUser.NguoiDungId, startUtc, endUtc);
            Assert.Single(mainRooms);
            Assert.Equal(room.PhongTroId, mainRooms[0].PhongTroId);

            // Member sees room
            var memberRooms = await queryStore.GetTenantRoomsAsync(memberUser.NguoiDungId, startUtc, endUtc);
            Assert.Single(memberRooms);
            Assert.Equal(room.PhongTroId, memberRooms[0].PhongTroId);

            // Other user sees nothing
            var otherRooms = await queryStore.GetTenantRoomsAsync(otherUser.NguoiDungId, startUtc, endUtc);
            Assert.Empty(otherRooms);

            // Cancelled contract returns empty
            contract.TrangThaiHopDong = TrangThaiHopDong.DaHuy;
            await context.SaveChangesAsync();
            Assert.Empty(await queryStore.GetTenantRoomsAsync(tenantUser.NguoiDungId, startUtc, endUtc));
        }

        [Fact]
        public async Task GetImagesAsync_ExcludesDeleted_AndSortsDesc_AndMarksTenantUploader()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var (branch, room, tenantUser, tenant, staffUser) = await SeedBasicHierarchyAsync(context, suffix);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = 10,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            context.DichVuDienNuocCuaPhongs.Add(period);
            await context.SaveChangesAsync();

            // Image 1: uploaded earlier by tenant
            var img1 = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test/1.jpg",
                NguoiGuiId = tenantUser.NguoiDungId,
                NgayGui = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                GiaTriAIGoiY = 100m,
                DoTinCay = 0.9
            };

            // Image 2: uploaded later by staff
            var img2 = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test/2.jpg",
                NguoiGuiId = staffUser.NguoiDungId,
                NgayGui = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc),
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 105m,
                NguoiXacNhanId = staffUser.NguoiDungId,
                NgayXacNhan = new DateTime(2026, 10, 2, 10, 5, 0, DateTimeKind.Utc),
                DuocChonLamChiSoChinhThuc = true
            };

            // Image 3: deleted image
            var img3 = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test/3.jpg",
                NguoiGuiId = staffUser.NguoiDungId,
                NgayGui = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.Loi,
                IsDeleted = true
            };

            context.AnhChiSoDongHos.AddRange(img1, img2, img3);
            await context.SaveChangesAsync();

            var queryStore = new MeterImageQueryStore(context);
            var images = await queryStore.GetImagesAsync(period.DichVuDienNuocCuaPhongId);

            // Excludes deleted -> only 2 images
            Assert.Equal(2, images.Count);

            // Sorted by NgayGui descending -> img2 first, then img1
            Assert.Equal(img2.AnhChiSoDongHoId, images[0].AnhChiSoDongHoId);
            Assert.Equal(img1.AnhChiSoDongHoId, images[1].AnhChiSoDongHoId);

            // NguoiGuiLaKhachThue check
            Assert.False(images[0].NguoiGuiLaKhachThue); // staff
            Assert.True(images[1].NguoiGuiLaKhachThue);  // tenant
        }

        [Fact]
        public async Task GetPeriodAsync_DeletedPeriodReturnsNull()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var (branch, room, _, _, _) = await SeedBasicHierarchyAsync(context, suffix);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = 10,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m
            };
            context.DichVuDienNuocCuaPhongs.Add(period);
            await context.SaveChangesAsync();

            var queryStore = new MeterImageQueryStore(context);
            var res = await queryStore.GetPeriodAsync(room.PhongTroId, 10, 2026);
            Assert.NotNull(res);
            Assert.Equal(100m, res.ChiSoDienCu);

            // Mark deleted
            period.IsDeleted = true;
            await context.SaveChangesAsync();

            var resAfterDelete = await queryStore.GetPeriodAsync(room.PhongTroId, 10, 2026);
            Assert.Null(resAfterDelete);
        }

        [Fact]
        public async Task GetRoomAsync_ReturnsRoomOrNullIfDeleted()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var (branch, room, _, _, _) = await SeedBasicHierarchyAsync(context, suffix);

            var queryStore = new MeterImageQueryStore(context);
            var found = await queryStore.GetRoomAsync(room.PhongTroId);
            Assert.NotNull(found);
            Assert.Equal(room.SoPhong, found.SoPhong);

            room.IsDeleted = true;
            await context.SaveChangesAsync();

            var notFound = await queryStore.GetRoomAsync(room.PhongTroId);
            Assert.Null(notFound);
        }
    }
}
