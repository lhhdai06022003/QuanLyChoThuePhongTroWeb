using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Lịch sử thanh toán phải bị giới hạn theo chi nhánh được phân công của nhân viên.
    public class LichSuThanhToanBranchScopeTests
    {
        private sealed class RecordingStore : ILichSuThanhToanStore
        {
            public bool Called { get; private set; }
            public IReadOnlyCollection<int>? ListBranches { get; private set; }
            public IReadOnlyCollection<int>? StatsBranches { get; private set; }

            public Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
                DataTableRequest request, int chiNhanhId, int phuongThuc, DateTime? tuNgay, DateTime? denNgay, bool chiCoTienThua = false,
                IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Called = true;
                ListBranches = allowedBranchIds;
                return Task.FromResult(new DataTableResponse<LichSuThanhToanGiaoDichRes> { draw = request.Draw });
            }

            public Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(
                int chiNhanhId, DateTime? tuNgay, DateTime? denNgay, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Called = true;
                StatsBranches = allowedBranchIds;
                return Task.FromResult(new ThongKeThanhToanRes { TongSoGiaoDich = 7 });
            }

            public Task<IReadOnlyList<LichSuThanhToan>> GetLichSuByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<LichSuThanhToan>>(Array.Empty<LichSuThanhToan>());
        }

        private static (LichSuThanhToanService Service, RecordingStore Store) Create(EmployeeAccessScope? scope)
        {
            var store = new RecordingStore();
            var access = new FakeEmployeeAccessService { ScopeToReturn = scope };
            return (new LichSuThanhToanService(store, access), store);
        }

        [Fact]
        public async Task Staff_ListIsLimitedToAssignedBranches()
        {
            var (service, store) = Create(new EmployeeAccessScope(5, false, new[] { 1, 2 }));

            await service.GetDanhSachThanhToanAsync(5, new DataTableRequest { Draw = 3 }, 0, -1, null, null);

            Assert.NotNull(store.ListBranches);
            Assert.Equal(new[] { 1, 2 }, store.ListBranches!.OrderBy(x => x));
        }

        [Fact]
        public async Task Admin_ListIsNotLimited()
        {
            var (service, store) = Create(new EmployeeAccessScope(1, true));

            await service.GetDanhSachThanhToanAsync(1, new DataTableRequest(), 0, -1, null, null);

            Assert.True(store.Called);
            Assert.Null(store.ListBranches);
        }

        [Fact]
        public async Task Staff_AskingForUnassignedBranch_GetsEmptyResultWithoutQuery()
        {
            var (service, store) = Create(new EmployeeAccessScope(5, false, new[] { 1 }));

            var page = await service.GetDanhSachThanhToanAsync(5, new DataTableRequest { Draw = 9 }, 2, -1, null, null);
            var stats = await service.GetThongKeThanhToanAsync(5, 2, null, null);

            Assert.False(store.Called);
            Assert.Equal(9, page.draw);
            Assert.Empty(page.data);
            Assert.Equal(0, stats.TongSoGiaoDich);
        }

        [Fact]
        public async Task NoScope_GetsEmptyResultWithoutQuery()
        {
            var (service, store) = Create(null);

            var page = await service.GetDanhSachThanhToanAsync(5, new DataTableRequest(), 0, -1, null, null);
            var stats = await service.GetThongKeThanhToanAsync(5, 0, null, null);

            Assert.False(store.Called);
            Assert.Empty(page.data);
            Assert.Equal(0, stats.TongSoGiaoDich);
        }

        [Fact]
        public async Task Staff_StatsAreLimitedToAssignedBranches()
        {
            var (service, store) = Create(new EmployeeAccessScope(5, false, new[] { 4 }));

            var stats = await service.GetThongKeThanhToanAsync(5, 4, null, null);

            Assert.Equal(7, stats.TongSoGiaoDich);
            Assert.Equal(new[] { 4 }, store.StatsBranches!.ToArray());
        }
    }
}
