using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class HopDongServiceTests
    {
        private class FakeApplicationTransaction : IApplicationTransaction
        {
            public bool Committed { get; private set; }
            public bool RolledBack { get; private set; }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                Committed = true;
                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                RolledBack = true;
                return Task.CompletedTask;
            }

            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private class FakeUnitOfWork : IUnitOfWork
        {
            public FakeApplicationTransaction? LastTransaction { get; private set; }
            public int SaveChangesCallCount { get; private set; }

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                LastTransaction = new FakeApplicationTransaction();
                return Task.FromResult<IApplicationTransaction>(LastTransaction);
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                SaveChangesCallCount++;
                return Task.FromResult(1);
            }
        }

        private class FakePasswordService : IPasswordService
        {
            public string HashPassword(string password) => $"hashed_{password}";
            public bool VerifyPassword(string hashedPassword, string providedPassword, out bool needsRehash) { needsRehash = false; return true; }
            public bool VerifyPassword(string hashedPassword, string providedPassword) => true;
        }

        private class StubHopDongStore : IHopDongStore
        {
            public bool RoomRented { get; set; }
            public bool TenantActive { get; set; }
            public bool ContractHasInvoices { get; set; }
            public PhongTro? Room { get; set; }
            public HopDong? ActiveContract { get; set; }
            public List<string> OverlappingMembers { get; set; } = new();
            public NguoiThue? Tenant { get; set; }
            public bool AccountExists { get; set; }
            public List<HopDong> AddedContracts { get; } = new();
            public List<PhongTro> UpdatedRooms { get; } = new();
            public List<HopDong> UpdatedContracts { get; } = new();
            public HopDong? DeletedContract { get; set; }
            public List<NguoiDung> AddedUsers { get; } = new();

            public Task<bool> IsRoomRentedAsync(int phongTroId, CancellationToken cancellationToken = default) =>
                Task.FromResult(RoomRented);

            public Task<bool> IsTenantActiveInAnotherContractAsync(int nguoiThueId, int? excludeHopDongId = null, CancellationToken cancellationToken = default) =>
                Task.FromResult(TenantActive);

            public Task<PhongTro?> GetPhongTroByIdAsync(int phongTroId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Room);

            public Task<string?> GetMaChiNhanhAsync(int chiNhanhId, CancellationToken cancellationToken = default) =>
                Task.FromResult<string?>("CN01");

            public Task<int> CountContractsWithPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
                Task.FromResult(0);

            public Task<bool> ExistsContractWithCodeAsync(string code, CancellationToken cancellationToken = default) =>
                Task.FromResult(false);

            public Task<IReadOnlyList<string>> GetOverlappingLivingMemberNamesAsync(IReadOnlyList<int> memberIds, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<string>>(OverlappingMembers);

            public Task<NguoiThue?> GetNguoiThueByIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Tenant);

            public Task<bool> ExistsActiveAccountForTenantAsync(int nguoiThueId, CancellationToken cancellationToken = default) =>
                Task.FromResult(AccountExists);

            public Task<IReadOnlyList<DieuKhoanMau>> GetActiveTermsByIdsAsync(IReadOnlyList<int> termIds, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<DieuKhoanMau>>(new List<DieuKhoanMau>());

            public Task<bool> HasInvoicesAsync(int hopDongId, CancellationToken cancellationToken = default) =>
                Task.FromResult(ContractHasInvoices);

            public Task<HopDong?> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default) =>
                Task.FromResult(ActiveContract);

            public Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetActiveMembersByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ChiTietThanhVienHopDong>>(new List<ChiTietThanhVienHopDong>());

            public Task<IReadOnlyList<HopDongDieuKhoan>> GetHopDongDieuKhoansByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<HopDongDieuKhoan>>(new List<HopDongDieuKhoan>());

            public Task<bool> IsRoomRentedExcludingContractAsync(int phongTroId, int excludeHopDongId, CancellationToken cancellationToken = default) =>
                Task.FromResult(false);

            public Task AddAsync(HopDong hopDong, CancellationToken cancellationToken = default)
            {
                hopDong.HopDongId = 100;
                AddedContracts.Add(hopDong);
                return Task.CompletedTask;
            }

            public Task AddMembersAsync(IEnumerable<ChiTietThanhVienHopDong> members, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task AddTermsAsync(IEnumerable<HopDongDieuKhoan> terms, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task AddNguoiDungAsync(NguoiDung nguoiDung, CancellationToken cancellationToken = default)
            {
                AddedUsers.Add(nguoiDung);
                return Task.CompletedTask;
            }

            public void Update(HopDong hopDong) => UpdatedContracts.Add(hopDong);
            public void UpdatePhongTro(PhongTro phongTro) => UpdatedRooms.Add(phongTro);
            public void RemoveTerms(IEnumerable<HopDongDieuKhoan> terms) { }

            // Not used
            public Task<DataTableResponse<HopDongRes>> GetDataTableResponseAsync(HopDongFilterReq request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDongDetailRes?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDong?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDongPrintRes?> GetPrintDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDong>> GetHopDongsByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDongKhachThueDetailDto?> GetChiTietHopDongKhachThueAsync(int id, int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDong>> GetExpiredActiveContractsAsync(DateTime nowUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDong>> GetActiveExpiringContractsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<DangKyDichVu>> GetActiveServicesByRoomIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private HopDongService CreateService(StubHopDongStore store, FakeUnitOfWork uow)
        {
            return new HopDongService(store, uow, new FakePasswordService());
        }

        [Fact]
        public async Task Create_EndDateBeforeStartDate_ReturnsError()
        {
            var store = new StubHopDongStore();
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var input = new HopDongReq
            {
                PhongTroId = 1,
                NguoiThueId = 1,
                ThoiDiemBatDau = DateTime.UtcNow,
                ThoiDiemKetThuc = DateTime.UtcNow.AddDays(-1),
                TienThuePhong = 3000000,
                TienCocPhong = 3000000
            };

            var (isSuccess, error) = await service.CreateAsync(input);

            Assert.False(isSuccess);
            Assert.Contains("kết thúc", error!);
        }

        [Fact]
        public async Task Create_RoomAlreadyRented_ReturnsError()
        {
            var store = new StubHopDongStore { RoomRented = true };
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var input = new HopDongReq
            {
                PhongTroId = 1,
                NguoiThueId = 1,
                ThoiDiemBatDau = DateTime.UtcNow,
                TienThuePhong = 3000000,
                TienCocPhong = 3000000
            };

            var (isSuccess, error) = await service.CreateAsync(input);

            Assert.False(isSuccess);
            Assert.Contains("hợp đồng hiệu lực", error!);
        }

        [Fact]
        public async Task Create_TenantAlreadyActive_ReturnsError()
        {
            var store = new StubHopDongStore { TenantActive = true };
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var input = new HopDongReq
            {
                PhongTroId = 1,
                NguoiThueId = 1,
                ThoiDiemBatDau = DateTime.UtcNow,
                TienThuePhong = 3000000,
                TienCocPhong = 3000000
            };

            var (isSuccess, error) = await service.CreateAsync(input);

            Assert.False(isSuccess);
            Assert.Contains("đứng tên", error!);
        }

        [Fact]
        public async Task Create_Success_CommitsTransactionAndUpdatesRoomStatus()
        {
            var room = new PhongTro
            {
                PhongTroId = 1,
                SoPhong = "101",
                ChiNhanhId = 1,
                TrangThai = TrangThaiPhong.Trong,
                SoNguoiToiDa = 4
            };
            var tenant = new NguoiThue
            {
                NguoiThueId = 1,
                HoVaTen = "Test Tenant",
                Email = "tenant@test.com",
                SoDienThoai = "0901234567"
            };
            var store = new StubHopDongStore
            {
                Room = room,
                Tenant = tenant,
                AccountExists = false
            };
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var input = new HopDongReq
            {
                PhongTroId = 1,
                NguoiThueId = 1,
                ThoiDiemBatDau = DateTime.UtcNow,
                TienThuePhong = 3000000,
                TienCocPhong = 3000000,
                NguoiDungCoOPhongKhong = true
            };

            var (isSuccess, _) = await service.CreateAsync(input);

            Assert.True(isSuccess);
            Assert.True(uow.LastTransaction!.Committed);
            Assert.Equal(TrangThaiPhong.DaThue, room.TrangThai);
            Assert.Single(store.AddedContracts);
            Assert.Single(store.AddedUsers);
            Assert.Equal(2, uow.SaveChangesCallCount);
        }

        [Fact]
        public async Task Delete_ActiveContract_ReturnsError()
        {
            var store = new StubHopDongStore
            {
                ActiveContract = new HopDong
                {
                    HopDongId = 1,
                    TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                    PhongTroId = 1
                }
            };
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var (isSuccess, error) = await service.DeleteAsync(1);

            Assert.False(isSuccess);
            Assert.Contains("Hoạt động", error!);
        }

        [Fact]
        public async Task Delete_NotFound_ReturnsError()
        {
            var store = new StubHopDongStore { ActiveContract = null };
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var (isSuccess, error) = await service.DeleteAsync(999);

            Assert.False(isSuccess);
            Assert.Contains("Không tìm thấy", error!);
        }

        [Fact]
        public async Task Delete_EndedContract_SoftDeletesAndReleasesRoom()
        {
            var room = new PhongTro
            {
                PhongTroId = 10,
                SoPhong = "102",
                TrangThai = TrangThaiPhong.DaThue
            };
            var contract = new HopDong
            {
                HopDongId = 5,
                TrangThaiHopDong = TrangThaiHopDong.DaKetThuc,
                PhongTroId = 10,
                IsDeleted = false
            };
            var store = new StubHopDongStore
            {
                ActiveContract = contract,
                Room = room
            };
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var (isSuccess, _) = await service.DeleteAsync(5);

            Assert.True(isSuccess);
            Assert.True(contract.IsDeleted);
            Assert.Equal(TrangThaiPhong.Trong, room.TrangThai);
        }

        [Fact]
        public async Task Update_CoreInfoChanged_OnActiveContract_ReturnsError()
        {
            var contract = new HopDong
            {
                HopDongId = 1,
                PhongTroId = 1,
                NguoiThueId = 1,
                TienThuePhong = 3000000,
                TienCocPhong = 3000000,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-1),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            var store = new StubHopDongStore { ActiveContract = contract };
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var input = new HopDongReq
            {
                PhongTroId = 1,
                NguoiThueId = 1,
                ThoiDiemBatDau = contract.ThoiDiemBatDau,
                TienThuePhong = 5000000,
                TienCocPhong = 3000000,
                TrangThaiHopDong = (Application.Common.Enums.AppTrangThaiHopDong)(int)TrangThaiHopDong.DangHoatDong
            };

            var (isSuccess, error) = await service.UpdateAsync(1, input);

            Assert.False(isSuccess);
            Assert.Contains("cốt lõi", error!);
        }

        [Fact]
        public async Task Update_CancelContractWithInvoices_ReturnsError()
        {
            var contract = new HopDong
            {
                HopDongId = 1,
                PhongTroId = 1,
                NguoiThueId = 1,
                TienThuePhong = 3000000,
                TienCocPhong = 3000000,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-1),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            var store = new StubHopDongStore
            {
                ActiveContract = contract,
                ContractHasInvoices = true
            };
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var input = new HopDongReq
            {
                PhongTroId = 1,
                NguoiThueId = 1,
                ThoiDiemBatDau = contract.ThoiDiemBatDau,
                TienThuePhong = 3000000,
                TienCocPhong = 3000000,
                TrangThaiHopDong = (Application.Common.Enums.AppTrangThaiHopDong)(int)TrangThaiHopDong.DaHuy
            };

            var (isSuccess, error) = await service.UpdateAsync(1, input);

            Assert.False(isSuccess);
            Assert.Contains("hóa đơn", error!);
        }
    }
}
