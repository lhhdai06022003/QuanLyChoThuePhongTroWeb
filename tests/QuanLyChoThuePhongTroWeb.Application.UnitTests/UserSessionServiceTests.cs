using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class UserSessionServiceTests
    {
        private const int ValidId = 1;
        private const int LockedId = 2;
        private const int DeletedId = 3;
        private const int RoleMismatchId = 4;
        private const int MissingId = 99;

        private readonly FakeUserSessionStore _store = new();
        private readonly UserSessionService _service;

        public UserSessionServiceTests()
        {
            _store.Snapshots[ValidId] = new UserSessionSnapshotDto { NguoiDungId = ValidId, Role = Role.Admin, IsActive = true, IsDeleted = false };
            _store.Snapshots[LockedId] = new UserSessionSnapshotDto { NguoiDungId = LockedId, Role = Role.NhanVien, IsActive = false, IsDeleted = false };
            _store.Snapshots[DeletedId] = new UserSessionSnapshotDto { NguoiDungId = DeletedId, Role = Role.NhanVien, IsActive = true, IsDeleted = true };
            _store.Snapshots[RoleMismatchId] = new UserSessionSnapshotDto { NguoiDungId = RoleMismatchId, Role = Role.KhachThue, IsActive = true, IsDeleted = false };

            _service = new UserSessionService(_store);
        }

        [Fact]
        public async Task ValidAccount_MatchingRole_ReturnsTrue()
        {
            var result = await _service.IsSessionValidAsync(ValidId, AppRole.Admin);
            Assert.True(result);
        }

        [Fact]
        public async Task LockedAccount_ReturnsFalse()
        {
            var result = await _service.IsSessionValidAsync(LockedId, AppRole.NhanVien);
            Assert.False(result);
        }

        [Fact]
        public async Task DeletedAccount_ReturnsFalse()
        {
            var result = await _service.IsSessionValidAsync(DeletedId, AppRole.NhanVien);
            Assert.False(result);
        }

        [Fact]
        public async Task RoleMismatch_ReturnsFalse()
        {
            // Snapshot thật là KhachThue nhưng claim role đang mang là NhanVien -> lệch role, đổi role phải mất phiên.
            var result = await _service.IsSessionValidAsync(RoleMismatchId, AppRole.NhanVien);
            Assert.False(result);
        }

        [Fact]
        public async Task NonExistentAccount_ReturnsFalse()
        {
            var result = await _service.IsSessionValidAsync(MissingId, AppRole.Admin);
            Assert.False(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task InvalidId_ReturnsFalse(int nguoiDungId)
        {
            var result = await _service.IsSessionValidAsync(nguoiDungId, AppRole.Admin);
            Assert.False(result);
        }
    }
}
