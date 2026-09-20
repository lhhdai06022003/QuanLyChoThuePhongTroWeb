using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class ContractAutoCloseUseCaseTests
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
            public FakeApplicationTransaction LastTransaction { get; private set; } = null!;
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

        private class FakeAutoCloseHopDongStore : IHopDongStore
        {
            public List<HopDong> ExpiredContracts { get; set; } = new();
            public List<DangKyDichVu> ActiveServices { get; set; } = new();
            public List<HopDong> UpdatedContracts { get; } = new();
            public List<PhongTro> UpdatedRooms { get; } = new();

            public Task<IReadOnlyList<HopDong>> GetExpiredActiveContractsAsync(DateTime nowUtc, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<HopDong>>(ExpiredContracts);

            public Task<IReadOnlyList<DangKyDichVu>> GetActiveServicesByRoomIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<DangKyDichVu>>(ActiveServices);

            public void Update(HopDong hopDong) => UpdatedContracts.Add(hopDong);
            public void UpdatePhongTro(PhongTro phongTro) => UpdatedRooms.Add(phongTro);

            // Not used by ContractAutoCloseUseCase
            public Task<IReadOnlyList<HopDong>> GetActiveExpiringContractsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<Common.Models.DataTableResponse<Features.HopDongs.DTOs.HopDongRes>> GetDataTableResponseAsync(Features.HopDongs.DTOs.HopDongFilterReq request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<Features.HopDongs.DTOs.HopDongDetailRes?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDong?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDong?> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<string?> GetMaChiNhanhAsync(int chiNhanhId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<int> CountContractsWithPrefixAsync(string prefix, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> ExistsContractWithCodeAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> IsRoomRentedAsync(int phongTroId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> IsTenantActiveInAnotherContractAsync(int nguoiThueId, int? excludeHopDongId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> IsRoomRentedExcludingContractAsync(int phongTroId, int excludeHopDongId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<PhongTro?> GetPhongTroByIdAsync(int phongTroId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<string>> GetOverlappingLivingMemberNamesAsync(IReadOnlyList<int> memberIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<NguoiThue?> GetNguoiThueByIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> ExistsActiveAccountForTenantAsync(int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<DieuKhoanMau>> GetActiveTermsByIdsAsync(IReadOnlyList<int> termIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> HasInvoicesAsync(int hopDongId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetActiveMembersByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDongDieuKhoan>> GetHopDongDieuKhoansByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<Features.HopDongs.DTOs.HopDongPrintRes?> GetPrintDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDong>> GetHopDongsByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<Features.HopDongs.DTOs.HopDongKhachThueDetailDto?> GetChiTietHopDongKhachThueAsync(int id, int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddAsync(HopDong hopDong, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddMembersAsync(IEnumerable<ChiTietThanhVienHopDong> members, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddTermsAsync(IEnumerable<HopDongDieuKhoan> terms, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddNguoiDungAsync(NguoiDung nguoiDung, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public void RemoveTerms(IEnumerable<HopDongDieuKhoan> terms) => throw new NotImplementedException();
        }

        [Fact]
        public async Task Execute_NoExpiredContracts_ReturnsZero()
        {
            var store = new FakeAutoCloseHopDongStore();
            var uow = new FakeUnitOfWork();
            var useCase = new ContractAutoCloseUseCase(store, uow, NullLogger<ContractAutoCloseUseCase>.Instance);

            var result = await useCase.ExecuteAsync();

            Assert.Equal(0, result);
            Assert.Equal(0, uow.SaveChangesCallCount);
        }

        [Fact]
        public async Task Execute_WithExpiredContracts_ClosesAndCommitsTransaction()
        {
            var phongTro = new PhongTro
            {
                PhongTroId = 10,
                SoPhong = "101",
                TrangThai = TrangThaiPhong.DaThue,
                ChiNhanhId = 1
            };
            var contract = new HopDong
            {
                HopDongId = 1,
                PhongTroId = 10,
                PhongTro = phongTro,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                ThoiDiemKetThuc = DateTime.UtcNow.AddDays(-1),
                ChiTietThanhVienHopDongs = new List<ChiTietThanhVienHopDong>
                {
                    new() { ChiTietThanhVienHopDongId = 1, NgayChuyenDi = null, IsDeleted = false }
                }
            };
            var store = new FakeAutoCloseHopDongStore
            {
                ExpiredContracts = new List<HopDong> { contract }
            };
            var uow = new FakeUnitOfWork();
            var useCase = new ContractAutoCloseUseCase(store, uow, NullLogger<ContractAutoCloseUseCase>.Instance);

            var result = await useCase.ExecuteAsync();

            Assert.Equal(1, result);
            Assert.Equal(TrangThaiHopDong.DaKetThuc, contract.TrangThaiHopDong);
            Assert.Equal(TrangThaiPhong.Trong, phongTro.TrangThai);
            Assert.NotNull(contract.ChiTietThanhVienHopDongs.First().NgayChuyenDi);
            Assert.True(uow.LastTransaction.Committed);
            Assert.False(uow.LastTransaction.RolledBack);
            Assert.Equal(1, uow.SaveChangesCallCount);
        }

        [Fact]
        public async Task Execute_WithActiveServices_ClosesServicesForRoom()
        {
            var contract = new HopDong
            {
                HopDongId = 2,
                PhongTroId = 20,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                ThoiDiemKetThuc = DateTime.UtcNow.AddDays(-1)
            };
            var service = new DangKyDichVu { DangKyDichVuId = 1, PhongTroId = 20, NgayKetThuc = null };
            var store = new FakeAutoCloseHopDongStore
            {
                ExpiredContracts = new List<HopDong> { contract },
                ActiveServices = new List<DangKyDichVu> { service }
            };
            var uow = new FakeUnitOfWork();
            var useCase = new ContractAutoCloseUseCase(store, uow, NullLogger<ContractAutoCloseUseCase>.Instance);

            await useCase.ExecuteAsync();

            Assert.NotNull(service.NgayKetThuc);
            Assert.True(uow.LastTransaction.Committed);
        }

        [Fact]
        public async Task Execute_TransactionRollsBackOnException()
        {
            var store = new FakeAutoCloseHopDongStore
            {
                ExpiredContracts = new List<HopDong>
                {
                    new()
                    {
                        HopDongId = 3,
                        PhongTroId = 30,
                        TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                        ThoiDiemKetThuc = DateTime.UtcNow.AddDays(-1)
                    }
                }
            };
            var uow = new FailingUnitOfWork();
            var useCase = new ContractAutoCloseUseCase(store, uow, NullLogger<ContractAutoCloseUseCase>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync());

            Assert.True(uow.LastTransaction!.RolledBack);
            Assert.False(uow.LastTransaction.Committed);
        }

        private class FailingUnitOfWork : IUnitOfWork
        {
            public FakeApplicationTransaction? LastTransaction { get; private set; }

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                LastTransaction = new FakeApplicationTransaction();
                return Task.FromResult<IApplicationTransaction>(LastTransaction);
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("Simulated DB error");
        }
    }
}
