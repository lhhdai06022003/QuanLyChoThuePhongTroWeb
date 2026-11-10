using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
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

            public LichSuThanhToanLoc? LastListLoc { get; private set; }
            public LichSuThanhToanLoc? LastStatsLoc { get; private set; }

            public Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
                DataTableRequest request, LichSuThanhToanLoc loc,
                IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Called = true;
                LastListLoc = loc;
                ListBranches = allowedBranchIds;
                return Task.FromResult(new DataTableResponse<LichSuThanhToanGiaoDichRes> { draw = request.Draw });
            }

            public Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(
                LichSuThanhToanLoc loc, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Called = true;
                LastStatsLoc = loc;
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

        private static LichSuThanhToanLocReq Loc(int chiNhanhId = 0, string? phuongThuc = null, bool tienThua = false,
            string? nguoiThu = null, int? kyThang = null, int? kyNam = null) =>
            new() { ChiNhanhId = chiNhanhId, PhuongThuc = phuongThuc, ChiCoTienThua = tienThua, NguoiThu = nguoiThu, KyThang = kyThang, KyNam = kyNam };

        [Fact]
        public async Task Admin_SlugSePay_StoreNhanKenhSePay_SlugLaThiMoiKenh()
        {
            var (service, store) = Create(new EmployeeAccessScope(1, true));

            await service.GetDanhSachThanhToanAsync(1, new DataTableRequest(), Loc(phuongThuc: "sepay"));
            Assert.Equal(KenhThu.SePay, store.LastListLoc!.Kenh);

            await service.GetDanhSachThanhToanAsync(1, new DataTableRequest(), Loc(phuongThuc: " VNPay "));
            Assert.Equal(KenhThu.VnPay, store.LastListLoc!.Kenh);

            foreach (var sai in new[] { "xyz", "0", "-1", "", "  " })
            {
                await service.GetDanhSachThanhToanAsync(1, new DataTableRequest(), Loc(phuongThuc: sai));
                Assert.Null(store.LastListLoc!.Kenh);
            }
        }

        [Fact]
        public async Task ThongKe_KhongApKenhVaTienThua_DanhSachThiCo()
        {
            var (service, store) = Create(new EmployeeAccessScope(1, true));

            await service.GetThongKeThanhToanAsync(1, Loc(phuongThuc: "tien-mat", tienThua: true));
            await service.GetDanhSachThanhToanAsync(1, new DataTableRequest(), Loc(phuongThuc: "tien-mat", tienThua: true));

            Assert.Null(store.LastStatsLoc!.Kenh);
            Assert.False(store.LastStatsLoc.ChiCoTienThua);
            Assert.Equal(KenhThu.TienMat, store.LastListLoc!.Kenh);
            Assert.True(store.LastListLoc.ChiCoTienThua);
        }

        [Fact]
        public async Task Staff_ListIsLimitedToAssignedBranches()
        {
            var (service, store) = Create(new EmployeeAccessScope(5, false, new[] { 1, 2 }));

            await service.GetDanhSachThanhToanAsync(5, new DataTableRequest { Draw = 3 }, Loc());

            Assert.NotNull(store.ListBranches);
            Assert.Equal(new[] { 1, 2 }, store.ListBranches!.OrderBy(x => x));
        }

        [Fact]
        public async Task Admin_ListIsNotLimited()
        {
            var (service, store) = Create(new EmployeeAccessScope(1, true));

            await service.GetDanhSachThanhToanAsync(1, new DataTableRequest(), Loc());

            Assert.True(store.Called);
            Assert.Null(store.ListBranches);
        }

        [Fact]
        public async Task Staff_AskingForUnassignedBranch_GetsEmptyResultWithoutQuery()
        {
            var (service, store) = Create(new EmployeeAccessScope(5, false, new[] { 1 }));

            var page = await service.GetDanhSachThanhToanAsync(5, new DataTableRequest { Draw = 9 }, Loc(2));
            var stats = await service.GetThongKeThanhToanAsync(5, Loc(2));

            Assert.False(store.Called);
            Assert.Equal(9, page.draw);
            Assert.Empty(page.data);
            Assert.Equal(0, stats.TongSoGiaoDich);
        }

        [Fact]
        public async Task NoScope_GetsEmptyResultWithoutQuery()
        {
            var (service, store) = Create(null);

            var page = await service.GetDanhSachThanhToanAsync(5, new DataTableRequest(), Loc());
            var stats = await service.GetThongKeThanhToanAsync(5, Loc());

            Assert.False(store.Called);
            Assert.Empty(page.data);
            Assert.Equal(0, stats.TongSoGiaoDich);
        }

        // Fail closed: không có phạm vi thì kỳ/người thu gửi lên cũng không làm store bị gọi.
        [Fact]
        public async Task NoScope_GuiKyVaNguoiThu_KhongGoiStore()
        {
            var (service, store) = Create(null);

            var page = await service.GetDanhSachThanhToanAsync(5, new DataTableRequest(), Loc(kyThang: 3, kyNam: 2026, nguoiThu: "toi"));
            var stats = await service.GetThongKeThanhToanAsync(5, Loc(kyThang: 3, kyNam: 2026, nguoiThu: "toi"));

            Assert.False(store.Called);
            Assert.Empty(page.data);
            Assert.Equal(0, stats.TongSoGiaoDich);
            Assert.False(stats.LocTheoKy);
        }

        // Nhân viên chưa được phân công chi nhánh nào: store nhận danh sách chi nhánh RỖNG (store coi rỗng là không thấy gì),
        // tuyệt đối không phải null (null = Admin, không giới hạn), và kỳ gửi lên bị bỏ qua.
        [Fact]
        public async Task StaffKhongCoChiNhanh_StoreNhanDanhSachRong_KhongBaoGioNull_KhongLocKy()
        {
            var (service, store) = Create(new EmployeeAccessScope(5, false));

            await service.GetDanhSachThanhToanAsync(5, new DataTableRequest(), Loc(kyThang: 3, kyNam: 2026));
            var stats = await service.GetThongKeThanhToanAsync(5, Loc(kyThang: 3, kyNam: 2026));

            Assert.NotNull(store.ListBranches);
            Assert.Empty(store.ListBranches!);
            Assert.NotNull(store.StatsBranches);
            Assert.Empty(store.StatsBranches!);
            Assert.Null(store.LastStatsLoc!.KyThang);
            Assert.Null(store.LastListLoc!.KyThang);
            Assert.False(stats.LocTheoKy);
        }

        [Fact]
        public async Task Staff_StatsAreLimitedToAssignedBranches()
        {
            var (service, store) = Create(new EmployeeAccessScope(5, false, new[] { 4 }));

            var stats = await service.GetThongKeThanhToanAsync(5, Loc(4));

            Assert.Equal(7, stats.TongSoGiaoDich);
            Assert.Equal(new[] { 4 }, store.StatsBranches!.ToArray());
        }

        // ----- Bộ lọc kỳ hóa đơn (chỉ Admin) và người thu -----

        [Fact]
        public async Task Staff_GuiKy_BiBoQua_ThongKeKhongLocTheoKy()
        {
            var (service, store) = Create(new EmployeeAccessScope(5, false, new[] { 1 }));

            var stats = await service.GetThongKeThanhToanAsync(5, Loc(kyThang: 3, kyNam: 2026));
            await service.GetDanhSachThanhToanAsync(5, new DataTableRequest(), Loc(kyThang: 3, kyNam: 2026));

            Assert.Null(store.LastStatsLoc!.KyThang);
            Assert.Null(store.LastStatsLoc.KyNam);
            Assert.Null(store.LastListLoc!.KyThang);
            Assert.Null(store.LastListLoc.KyNam);
            Assert.False(stats.LocTheoKy);
            Assert.Null(stats.KyThang);
        }

        [Fact]
        public async Task Admin_KyHopLe_StoreNhanKy_VaThongKeBaoLocTheoKy()
        {
            var (service, store) = Create(new EmployeeAccessScope(1, true));

            var stats = await service.GetThongKeThanhToanAsync(1, Loc(kyThang: 3, kyNam: 2026));
            await service.GetDanhSachThanhToanAsync(1, new DataTableRequest(), Loc(kyThang: 3, kyNam: 2026));

            Assert.Equal(3, store.LastStatsLoc!.KyThang);
            Assert.Equal(2026, store.LastStatsLoc.KyNam);
            Assert.Equal(3, store.LastListLoc!.KyThang);
            Assert.True(stats.LocTheoKy);
            Assert.Equal(3, stats.KyThang);
            Assert.Equal(2026, stats.KyNam);
        }

        [Theory]
        [InlineData(13, 2026)]
        [InlineData(0, 2026)]
        [InlineData(3, 1999)]
        [InlineData(3, 2101)]
        [InlineData(3, null)]
        [InlineData(null, 2026)]
        public async Task Admin_KyKhongHopLeHoacThieu_BiBoQua(int? thang, int? nam)
        {
            var (service, store) = Create(new EmployeeAccessScope(1, true));

            var stats = await service.GetThongKeThanhToanAsync(1, Loc(kyThang: thang, kyNam: nam));

            Assert.Null(store.LastStatsLoc!.KyThang);
            Assert.Null(store.LastStatsLoc.KyNam);
            Assert.False(stats.LocTheoKy);
        }

        [Theory]
        [InlineData("toi", NguoiThuLoai.NguoiDung, 5)]
        [InlineData("TOI", NguoiThuLoai.NguoiDung, 5)]
        [InlineData("he-thong", NguoiThuLoai.HeThong, null)]
        [InlineData("7", NguoiThuLoai.NguoiDung, 7)]
        [InlineData("abc", NguoiThuLoai.TatCa, null)]
        [InlineData("0", NguoiThuLoai.TatCa, null)]
        [InlineData("-3", NguoiThuLoai.TatCa, null)]
        [InlineData("", NguoiThuLoai.TatCa, null)]
        [InlineData(null, NguoiThuLoai.TatCa, null)]
        public async Task NguoiThu_ChuanHoaDung(string? nguoiThu, NguoiThuLoai loai, int? id)
        {
            var (service, store) = Create(new EmployeeAccessScope(5, false, new[] { 1 }));

            await service.GetDanhSachThanhToanAsync(5, new DataTableRequest(), Loc(nguoiThu: nguoiThu));
            Assert.Equal(loai, store.LastListLoc!.NguoiThu);
            Assert.Equal(id, store.LastListLoc.NguoiThuId);

            await service.GetThongKeThanhToanAsync(5, Loc(nguoiThu: nguoiThu));
            Assert.Equal(loai, store.LastStatsLoc!.NguoiThu);
            Assert.Equal(id, store.LastStatsLoc.NguoiThuId);
        }
    }
}
