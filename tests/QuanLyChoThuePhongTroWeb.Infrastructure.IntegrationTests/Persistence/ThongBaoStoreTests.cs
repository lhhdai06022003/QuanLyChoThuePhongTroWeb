using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class ThongBaoStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public ThongBaoStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetRoomResponsibleUserIds_ReturnsActiveAdminsAndRoomBranchStaffOnly()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();
            var s = Guid.NewGuid().ToString("N")[..8];

            NguoiDung User(string tag, Role role, bool active = true) => new()
            {
                TenDangNhap = $"{tag}_{s}", MatKhauHash = "hash", Role = role, IsActive = active
            };
            var admin = User("admin", Role.Admin);
            var inactiveAdmin = User("admin_off", Role.Admin, active: false);
            var staffA = User("staffA", Role.NhanVien);
            var staffB = User("staffB", Role.NhanVien);
            var staffRevoked = User("staffRevoked", Role.NhanVien);
            var staffInactive = User("staffInactive", Role.NhanVien, active: false);
            context.NguoiDungs.AddRange(admin, inactiveAdmin, staffA, staffB, staffRevoked, staffInactive);

            var branchA = new ChiNhanh { TenChiNhanh = $"TbA {s}", MaChiNhanh = TestCodes.MaChiNhanh("TA"), DiaChi = "A", SoDienThoai = "0900000001", MoTa = "A" };
            var branchB = new ChiNhanh { TenChiNhanh = $"TbB {s}", MaChiNhanh = TestCodes.MaChiNhanh("TB"), DiaChi = "B", SoDienThoai = "0900000002", MoTa = "B" };
            context.ChiNhanhs.AddRange(branchA, branchB);
            await context.SaveChangesAsync();

            var roomA = new PhongTro { ChiNhanhId = branchA.ChiNhanhId, SoPhong = $"TA{s[..6]}", GiaThue = 1m, DienTich = 10, MoTa = "A" };
            context.PhongTros.Add(roomA);

            NhanVienChiNhanh Assign(NguoiDung u, ChiNhanh b, DateTime? revoked = null) => new()
            {
                NguoiDungId = u.NguoiDungId, ChiNhanhId = b.ChiNhanhId, NguoiPhanCongId = admin.NguoiDungId,
                IsActive = revoked == null, NgayPhanCong = DateTime.UtcNow.AddMonths(-1), NgayThuHoi = revoked,
                NguoiThuHoiId = revoked == null ? null : admin.NguoiDungId
            };
            context.NhanVienChiNhanhs.AddRange(
                Assign(staffA, branchA),
                Assign(staffB, branchB),
                Assign(staffRevoked, branchA, DateTime.UtcNow.AddDays(-1)),
                Assign(staffInactive, branchA));
            await context.SaveChangesAsync();

            var store = new ThongBaoStore(context);

            var ids = (await store.GetRoomResponsibleUserIdsAsync(roomA.PhongTroId)).ToHashSet();

            Assert.Contains(admin.NguoiDungId, ids);
            Assert.Contains(staffA.NguoiDungId, ids);
            Assert.DoesNotContain(inactiveAdmin.NguoiDungId, ids);
            Assert.DoesNotContain(staffB.NguoiDungId, ids);
            Assert.DoesNotContain(staffRevoked.NguoiDungId, ids);
            Assert.DoesNotContain(staffInactive.NguoiDungId, ids);

            // Phòng không tồn tại: chỉ Admin nhận.
            var missing = (await store.GetRoomResponsibleUserIdsAsync(int.MaxValue)).ToHashSet();
            Assert.Contains(admin.NguoiDungId, missing);
            Assert.DoesNotContain(staffA.NguoiDungId, missing);

            await tx.RollbackAsync();
        }

        // Người nhận được nhớ theo chi nhánh trong một store (một request, docs/features/toi-uu-hieu-nang bước 10):
        // phòng khác chi nhánh vẫn ra đúng người, sửa danh sách trả về không ảnh hưởng lần sau, store mới đọc lại.
        [Fact]
        public async Task GetRoomResponsibleUserIds_CachedPerBranch_StaysCorrectAcrossRoomsAndBranches()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();
            var s = Guid.NewGuid().ToString("N")[..8];

            NguoiDung User(string tag, Role role) => new() { TenDangNhap = $"{tag}_{s}", MatKhauHash = "hash", Role = role, IsActive = true };
            var admin = User("cadmin", Role.Admin);
            var staffA = User("cstaffA", Role.NhanVien);
            var staffB = User("cstaffB", Role.NhanVien);
            var staffLate = User("cstaffLate", Role.NhanVien);
            context.NguoiDungs.AddRange(admin, staffA, staffB, staffLate);
            var branchA = new ChiNhanh { TenChiNhanh = $"CcA {s}", MaChiNhanh = TestCodes.MaChiNhanh("CA"), DiaChi = "A", SoDienThoai = "0900000001", MoTa = "A" };
            var branchB = new ChiNhanh { TenChiNhanh = $"CcB {s}", MaChiNhanh = TestCodes.MaChiNhanh("CB"), DiaChi = "B", SoDienThoai = "0900000002", MoTa = "B" };
            context.ChiNhanhs.AddRange(branchA, branchB);
            await context.SaveChangesAsync();

            var roomA1 = new PhongTro { ChiNhanhId = branchA.ChiNhanhId, SoPhong = $"C1{s[..6]}", GiaThue = 1m, DienTich = 10, MoTa = "A" };
            var roomA2 = new PhongTro { ChiNhanhId = branchA.ChiNhanhId, SoPhong = $"C2{s[..6]}", GiaThue = 1m, DienTich = 10, MoTa = "A" };
            var roomB = new PhongTro { ChiNhanhId = branchB.ChiNhanhId, SoPhong = $"C3{s[..6]}", GiaThue = 1m, DienTich = 10, MoTa = "B" };
            context.PhongTros.AddRange(roomA1, roomA2, roomB);
            NhanVienChiNhanh Assign(NguoiDung u, ChiNhanh b) => new()
            {
                NguoiDungId = u.NguoiDungId, ChiNhanhId = b.ChiNhanhId, NguoiPhanCongId = admin.NguoiDungId,
                IsActive = true, NgayPhanCong = DateTime.UtcNow.AddMonths(-1)
            };
            context.NhanVienChiNhanhs.AddRange(Assign(staffA, branchA), Assign(staffB, branchB));
            await context.SaveChangesAsync();

            var store = new ThongBaoStore(context);

            var a1 = await store.GetRoomResponsibleUserIdsAsync(roomA1.PhongTroId);
            a1.Clear();
            var a2 = (await store.GetRoomResponsibleUserIdsAsync(roomA2.PhongTroId)).ToHashSet();
            var b = (await store.GetRoomResponsibleUserIdsAsync(roomB.PhongTroId)).ToHashSet();
            var missing = (await store.GetRoomResponsibleUserIdsAsync(int.MaxValue)).ToHashSet();

            Assert.Contains(admin.NguoiDungId, a2);
            Assert.Contains(staffA.NguoiDungId, a2);
            Assert.DoesNotContain(staffB.NguoiDungId, a2);
            Assert.Contains(staffB.NguoiDungId, b);
            Assert.DoesNotContain(staffA.NguoiDungId, b);
            Assert.Contains(admin.NguoiDungId, missing);
            Assert.DoesNotContain(staffA.NguoiDungId, missing);
            Assert.DoesNotContain(staffB.NguoiDungId, missing);

            // Phân công mới sau khi đã nhớ: request sau (store mới) thấy ngay.
            context.NhanVienChiNhanhs.Add(Assign(staffLate, branchA));
            await context.SaveChangesAsync();
            var fresh = (await new ThongBaoStore(context).GetRoomResponsibleUserIdsAsync(roomA1.PhongTroId)).ToHashSet();
            Assert.Contains(staffLate.NguoiDungId, fresh);

            await tx.RollbackAsync();
        }
    }
}
