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
        private const int AdminId = 1;

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
            public List<DangKyDichVu> RoomServices { get; } = new();

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

            public HopDongFilterReq? LastFilter { get; private set; }
            public HopDongDetailRes? Detail { get; set; }
            public HopDongPrintRes? Print { get; set; }
            public DataTableResponse<HopDongRes>? DataTableResponseToReturn { get; set; }

            public Task<DataTableResponse<HopDongRes>> GetDataTableResponseAsync(HopDongFilterReq request, CancellationToken cancellationToken = default)
            {
                LastFilter = request;
                return Task.FromResult(DataTableResponseToReturn ?? new DataTableResponse<HopDongRes> { draw = request.Draw });
            }

            public Task<HopDongDetailRes?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Detail);
            public Task<HopDongPrintRes?> GetPrintDataAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Print);

            // Not used
            public Task<HopDong?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDong>> GetHopDongsByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDongKhachThueDetailDto?> GetChiTietHopDongKhachThueAsync(int id, int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDong>> GetExpiredActiveContractsAsync(DateTime nowUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDong>> GetActiveExpiringContractsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<DangKyDichVu>> GetActiveServicesByRoomIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DangKyDichVu>>(RoomServices.ToList());
        }

        private HopDongService CreateService(StubHopDongStore store, FakeUnitOfWork uow)
        {
            return new HopDongService(store, uow, new FakePasswordService(), new Fakes.FakeEmployeeAccessService { ScopeToReturn = new Common.Security.EmployeeAccessScope(AdminId, true) }, new Fakes.FakeTenantVisibilityStore());
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

            var outcome = await service.CreateAsync(AdminId, input);
            var (isSuccess, error) = (outcome.Success, outcome.Message);

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

            var outcome = await service.CreateAsync(AdminId, input);
            var (isSuccess, error) = (outcome.Success, outcome.Message);

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

            var outcome = await service.CreateAsync(AdminId, input);
            var (isSuccess, error) = (outcome.Success, outcome.Message);

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

            var outcome = await service.CreateAsync(AdminId, input);
            var (isSuccess, _) = (outcome.Success, outcome.Message);

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

            var outcome = await service.DeleteAsync(AdminId, 1);
            var (isSuccess, error) = (outcome.Success, outcome.Message);

            Assert.False(isSuccess);
            Assert.Contains("Hoạt động", error!);
        }

        [Fact]
        public async Task Delete_NotFound_ReturnsError()
        {
            var store = new StubHopDongStore { ActiveContract = null };
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var outcome = await service.DeleteAsync(AdminId, 999);
            var (isSuccess, error) = (outcome.Success, outcome.Message);

            Assert.False(isSuccess);
            Assert.Contains("Không tìm thấy", error!);
        }

        [Fact]
        public async Task Delete_EndedContract_IsRejected_AndKeptForArchive()
        {
            var room = new PhongTro { PhongTroId = 10, SoPhong = "102", TrangThai = TrangThaiPhong.Trong };
            var contract = new HopDong
            {
                HopDongId = 5,
                TrangThaiHopDong = TrangThaiHopDong.DaKetThuc,
                PhongTroId = 10,
                IsDeleted = false
            };
            var store = new StubHopDongStore { ActiveContract = contract, Room = room };
            var uow = new FakeUnitOfWork();
            var service = CreateService(store, uow);

            var outcome = await service.DeleteAsync(AdminId, 5);

            Assert.False(outcome.Success);
            Assert.Contains("đã kết thúc", outcome.Message!);
            Assert.False(contract.IsDeleted);
            Assert.Empty(store.UpdatedContracts);
        }

        [Fact]
        public async Task Delete_CancelledContract_SoftDeletesAndReleasesRoom()
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
                TrangThaiHopDong = TrangThaiHopDong.DaHuy,
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

            var outcome = await service.DeleteAsync(AdminId, 5);
            var (isSuccess, _) = (outcome.Success, outcome.Message);

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

            var outcome = await service.UpdateAsync(AdminId, 1, input);
            var (isSuccess, error) = (outcome.Success, outcome.Message);

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

            var outcome = await service.UpdateAsync(AdminId, 1, input);
            var (isSuccess, error) = (outcome.Success, outcome.Message);

            Assert.False(isSuccess);
            Assert.Contains("hóa đơn", error!);
        }

        private static HopDong ActiveContractFor(string ma = "HD-CN01-2511-001") => new()
        {
            HopDongId = 1,
            MaHopDong = ma,
            PhongTroId = 1,
            NguoiThueId = 1,
            TienThuePhong = 3000000,
            TienCocPhong = 3000000,
            ThoiDiemBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
        };

        private static HopDongReq UpdateInput(HopDong contract, TrangThaiHopDong status) => new()
        {
            PhongTroId = contract.PhongTroId,
            NguoiThueId = contract.NguoiThueId,
            ThoiDiemBatDau = contract.ThoiDiemBatDau,
            TienThuePhong = contract.TienThuePhong,
            TienCocPhong = contract.TienCocPhong,
            TrangThaiHopDong = (Application.Common.Enums.AppTrangThaiHopDong)(int)status
        };

        [Fact]
        public async Task Update_ToDaKetThuc_IsRejected()
        {
            var contract = ActiveContractFor();
            var service = CreateService(new StubHopDongStore { ActiveContract = contract }, new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(AdminId, 1, UpdateInput(contract, TrangThaiHopDong.DaKetThuc));

            Assert.False(outcome.Success);
            Assert.Contains("Thanh lý", outcome.Message);
            Assert.Equal(TrangThaiHopDong.DangHoatDong, contract.TrangThaiHopDong);
        }

        [Fact]
        public async Task Update_EndedContract_IsRejected()
        {
            var contract = ActiveContractFor();
            contract.TrangThaiHopDong = TrangThaiHopDong.DaKetThuc;
            var service = CreateService(new StubHopDongStore { ActiveContract = contract }, new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(AdminId, 1, UpdateInput(contract, TrangThaiHopDong.DaKetThuc));

            Assert.False(outcome.Success);
            Assert.Contains("không sửa được", outcome.Message);
        }

        [Fact]
        public async Task Update_CancelRenewal_IsRejected()
        {
            var contract = ActiveContractFor("HD-CN01-2511-001-GH1");
            var service = CreateService(new StubHopDongStore { ActiveContract = contract }, new FakeUnitOfWork());

            var outcome = await service.UpdateAsync(AdminId, 1, UpdateInput(contract, TrangThaiHopDong.DaHuy));

            Assert.False(outcome.Success);
            Assert.Contains("hợp đồng gia hạn", outcome.Message);
        }

        [Fact]
        public async Task Update_ToDaHuy_ClosesRoomServices()
        {
            var contract = ActiveContractFor();
            var service = new DangKyDichVu { PhongTroId = 1, NgayBatDau = contract.ThoiDiemBatDau };
            var store = new StubHopDongStore
            {
                ActiveContract = contract,
                Room = new PhongTro { PhongTroId = 1, ChiNhanhId = 1, TrangThai = TrangThaiPhong.DaThue },
                RoomServices = { service }
            };
            var svc = CreateService(store, new FakeUnitOfWork());

            var outcome = await svc.UpdateAsync(AdminId, 1, UpdateInput(contract, TrangThaiHopDong.DaHuy));

            Assert.True(outcome.Success, outcome.Message);
            Assert.NotNull(service.NgayKetThuc);
            Assert.Equal(TrangThaiHopDong.DaHuy, contract.TrangThaiHopDong);
        }

        [Fact]
        public async Task Update_ActiveRenewal_StartStoredAt17Utc_EditEndDate_Succeeds()
        {
            var contract = ActiveContractFor("HD-CN01-2511-001-GH1");
            contract.ThoiDiemBatDau = new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc);
            var storedStart = contract.ThoiDiemBatDau;
            var store = new StubHopDongStore { ActiveContract = contract, Room = new PhongTro { PhongTroId = 1, ChiNhanhId = 1 } };
            var svc = CreateService(store, new FakeUnitOfWork());

            var input = UpdateInput(contract, TrangThaiHopDong.DangHoatDong);
            input.ThoiDiemBatDau = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);
            input.ThoiDiemKetThuc = new DateTime(2027, 10, 31, 0, 0, 0, DateTimeKind.Unspecified);

            var outcome = await svc.UpdateAsync(AdminId, 1, input);

            Assert.True(outcome.Success, outcome.Message);
            Assert.Equal(storedStart, contract.ThoiDiemBatDau);
            Assert.NotNull(contract.ThoiDiemKetThuc);
        }

        [Fact]
        public async Task DanhSach_SetsMocDauNgayHomNay()
        {
            var store = new StubHopDongStore();
            var service = new HopDongService(store, new FakeUnitOfWork(), new FakePasswordService(),
                new Fakes.FakeEmployeeAccessService { ScopeToReturn = new Common.Security.EmployeeAccessScope(AdminId, true) },
                new Fakes.FakeTenantVisibilityStore(),
                new Fakes.FixedTimeProvider(new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero)));

            await service.DanhSachHopDongSideAsync(AdminId, new HopDongFilterReq { MocDauNgayHomNayUtc = new DateTime(2000, 1, 1) });

            Assert.Equal(new DateTime(2026, 11, 4, 17, 0, 0, DateTimeKind.Utc), store.LastFilter!.MocDauNgayHomNayUtc);
        }

        [Fact]
        public async Task DanhSach_CalculatesSoNgayQuaHan_ForActiveContracts()
        {
            var store = new StubHopDongStore();
            var service = new HopDongService(store, new FakeUnitOfWork(), new FakePasswordService(),
                new Fakes.FakeEmployeeAccessService { ScopeToReturn = new Common.Security.EmployeeAccessScope(AdminId, true) },
                new Fakes.FakeTenantVisibilityStore(),
                new Fakes.FixedTimeProvider(new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero)));

            var h1 = new HopDongRes { HopDongId = 1, TrangThaiHopDong = Application.Common.Enums.AppTrangThaiHopDong.DangHoatDong, ThoiDiemKetThuc = new DateTime(2026, 11, 4, 16, 59, 59, DateTimeKind.Utc) };
            var h2 = new HopDongRes { HopDongId = 2, TrangThaiHopDong = Application.Common.Enums.AppTrangThaiHopDong.DangHoatDong, ThoiDiemKetThuc = new DateTime(2026, 11, 5, 16, 59, 59, DateTimeKind.Utc) };
            var h3 = new HopDongRes { HopDongId = 3, TrangThaiHopDong = Application.Common.Enums.AppTrangThaiHopDong.DangHoatDong, ThoiDiemKetThuc = new DateTime(2026, 11, 6, 16, 59, 59, DateTimeKind.Utc) };
            var h4 = new HopDongRes { HopDongId = 4, TrangThaiHopDong = Application.Common.Enums.AppTrangThaiHopDong.DaKetThuc, ThoiDiemKetThuc = new DateTime(2026, 11, 4, 16, 59, 59, DateTimeKind.Utc) };
            var h5 = new HopDongRes { HopDongId = 5, TrangThaiHopDong = Application.Common.Enums.AppTrangThaiHopDong.DangHoatDong, ThoiDiemKetThuc = null };

            store.DataTableResponseToReturn = new DataTableResponse<HopDongRes>
            {
                draw = 1,
                data = new List<HopDongRes> { h1, h2, h3, h4, h5 }
            };

            var res = await service.DanhSachHopDongSideAsync(AdminId, new HopDongFilterReq { Draw = 1 });
            var list = res.data!.ToList();

            Assert.Equal(1, list[0].SoNgayQuaHan);
            Assert.Null(list[1].SoNgayQuaHan);
            Assert.Null(list[2].SoNgayQuaHan);
            Assert.Null(list[3].SoNgayQuaHan);
            Assert.Null(list[4].SoNgayQuaHan);
        }

        // ---------- Phạm vi chi nhánh ----------

        private const int StaffId = 7;

        private static HopDongService CreateStaffService(StubHopDongStore store, int[] branches, Fakes.FakeTenantVisibilityStore? tenants = null) =>
            new(store, new FakeUnitOfWork(), new FakePasswordService(),
                new Fakes.FakeEmployeeAccessService { ScopeToReturn = new Common.Security.EmployeeAccessScope(StaffId, false, branches) },
                tenants ?? new Fakes.FakeTenantVisibilityStore());

        [Fact]
        public async Task Staff_List_IsFilteredToAssignedBranches_AndOtherBranchFilterIsEmpty()
        {
            var store = new StubHopDongStore();
            var service = CreateStaffService(store, new[] { 1 });

            await service.DanhSachHopDongSideAsync(StaffId, new HopDongFilterReq { AllowedBranchIds = null });
            Assert.Equal(new[] { 1 }, store.LastFilter!.AllowedBranchIds);

            store = new StubHopDongStore();
            service = CreateStaffService(store, new[] { 1 });
            var page = await service.DanhSachHopDongSideAsync(StaffId, new HopDongFilterReq { ChiNhanhId = 2, Draw = 4 });
            Assert.Null(store.LastFilter);
            Assert.Equal(4, page.draw);
        }

        [Fact]
        public async Task Admin_List_IsNotFiltered_EvenIfClientSendsBranches()
        {
            var store = new StubHopDongStore();
            var service = CreateService(store, new FakeUnitOfWork());

            await service.DanhSachHopDongSideAsync(AdminId, new HopDongFilterReq { AllowedBranchIds = new[] { 99 } });

            Assert.Null(store.LastFilter!.AllowedBranchIds);
        }

        [Fact]
        public async Task Staff_ReadContractOfOtherBranch_ReturnsNull()
        {
            var store = new StubHopDongStore
            {
                Detail = new HopDongDetailRes { HopDongId = 3, ChiNhanhId = 2 },
                Print = new HopDongPrintRes { HopDongId = 3, ChiNhanhId = 2 }
            };
            var service = CreateStaffService(store, new[] { 1 });

            Assert.Null(await service.GetByIdAsync(StaffId, 3));
            Assert.Null(await service.GetPrintDataAsync(StaffId, 3));
        }

        [Fact]
        public async Task Staff_WriteContractInOtherBranch_IsForbidden()
        {
            var room = new PhongTro { PhongTroId = 10, ChiNhanhId = 2, SoNguoiToiDa = 3 };
            var store = new StubHopDongStore
            {
                Room = room,
                ActiveContract = new HopDong { HopDongId = 5, PhongTroId = 10, TrangThaiHopDong = TrangThaiHopDong.DaKetThuc }
            };
            var service = CreateStaffService(store, new[] { 1 });
            var input = new HopDongReq { PhongTroId = 10, NguoiThueId = 1, ThoiDiemBatDau = DateTime.UtcNow, HopDongId = 5 };

            Assert.Equal(ServiceErrorKind.Forbidden, (await service.CreateAsync(StaffId, input)).ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, (await service.UpdateAsync(StaffId, 5, input)).ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, (await service.DeleteAsync(StaffId, 5)).ErrorKind);
            Assert.Empty(store.AddedContracts);
            Assert.Empty(store.UpdatedContracts);
        }
        [Fact]
        public async Task Staff_CreateContract_WithTenantOfOtherBranch_IsForbidden()
        {
            var store = new StubHopDongStore { Room = new PhongTro { PhongTroId = 10, ChiNhanhId = 1, SoNguoiToiDa = 3 } };
            var tenants = new Fakes.FakeTenantVisibilityStore();
            tenants.Hidden.Add(42);
            var service = CreateStaffService(store, new[] { 1 }, tenants);

            var asSigner = await service.CreateAsync(StaffId, new HopDongReq { PhongTroId = 10, NguoiThueId = 42, ThoiDiemBatDau = DateTime.UtcNow });
            var asMember = await service.CreateAsync(StaffId, new HopDongReq { PhongTroId = 10, NguoiThueId = 1, ThanhVienKhacIds = new List<int> { 42 }, ThoiDiemBatDau = DateTime.UtcNow });

            Assert.Equal(ServiceErrorKind.Forbidden, asSigner.ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, asMember.ErrorKind);
            Assert.Empty(store.AddedContracts);
        }
    }
}
