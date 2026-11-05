using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class HopDongTraCuuServiceTests
    {
        private const int Staff = 7;
        private const string Root = "HD-CN-2510-001";

        private static readonly ChiNhanh Branch1 = new() { ChiNhanhId = 1, TenChiNhanh = "CN Một" };
        private static readonly PhongTro Room = new() { PhongTroId = 5, ChiNhanhId = 1, ChiNhanh = Branch1, SoPhong = "P101" };
        private static readonly NguoiThue Tenant = new() { NguoiThueId = 3, HoVaTen = "Nguyễn Văn A", CCCD = "0123", SoDienThoai = "0911" };

        private static HopDong Contract(int id, string ma, TrangThaiHopDong st) => new()
        {
            HopDongId = id,
            MaHopDong = ma,
            PhongTroId = 5,
            PhongTro = Room,
            NguoiThueId = 3,
            NguoiThue = Tenant,
            ThoiDiemBatDau = new DateTime(2025, 10, 31, 17, 0, 0, DateTimeKind.Utc),
            ThoiDiemKetThuc = new DateTime(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc),
            TrangThaiHopDong = st
        };

        private static (HopDongTraCuuService Service, FakeHopDongLifecycleStore Store, FakeEmployeeAccessService Access, FakeTenantVisibilityStore Visible) Create(EmployeeAccessScope? scope = null)
        {
            var store = new FakeHopDongLifecycleStore();
            var access = new FakeEmployeeAccessService { ScopeToReturn = scope ?? new EmployeeAccessScope(Staff, false, new[] { 1 }) };
            var visible = new FakeTenantVisibilityStore();
            var service = new HopDongTraCuuService(store, access, visible, new FixedTimeProvider(new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero)));
            return (service, store, access, visible);
        }

        [Fact]
        public async Task Detail_ShowsChainInOrder_RenewedLabel_AndSkipsCancelledSuccessor()
        {
            var (service, store, _, _) = Create();
            store.HopDongs.Add(Contract(12, Root + "-GH2", TrangThaiHopDong.DangHoatDong));
            store.HopDongs.Add(Contract(10, Root, TrangThaiHopDong.DaKetThuc));
            store.HopDongs.Add(Contract(11, Root + "-GH1", TrangThaiHopDong.DaKetThuc));
            store.HopDongs.Add(Contract(13, Root + "-GH3", TrangThaiHopDong.DaHuy));

            var first = (await service.GetChiTietAsync(Staff, 10)).Data!;
            var last = (await service.GetChiTietAsync(Staff, 12)).Data!;

            Assert.Equal(new[] { Root, Root + "-GH1", Root + "-GH2", Root + "-GH3" }, first.ChuoiHopDong.Select(c => c.MaHopDong).ToArray());
            Assert.Equal(new[] { true, false, false, false }, first.ChuoiHopDong.Select(c => c.LaHienTai).ToArray());
            Assert.Equal("Đã gia hạn → " + Root + "-GH1", first.TrangThaiText);
            Assert.Equal(11, first.HopDongKeTiepId);
            Assert.False(first.CoBienBanThanhLy);
            Assert.False(first.CoTheThanhLy);

            // Hợp đồng GH3 đã hủy nên không tính là kế tiếp; GH2 đang hoạt động thì thanh lý/gia hạn được.
            Assert.Null(last.HopDongKeTiepId);
            Assert.True(last.CoTheThanhLy);
            Assert.True(last.CoTheGiaHan);
            Assert.Equal("Gia hạn lần 2", last.LoaiText);
        }

        [Fact]
        public async Task Detail_EndedWithoutSuccessor_HasSettlementReport()
        {
            var (service, store, _, _) = Create();
            store.HopDongs.Add(Contract(10, Root, TrangThaiHopDong.DaKetThuc));

            var dto = (await service.GetChiTietAsync(Staff, 10)).Data!;

            Assert.True(dto.CoBienBanThanhLy);
            Assert.Equal("Đã kết thúc", dto.TrangThaiText);
        }

        [Fact]
        public async Task Detail_OtherBranch_IsForbidden_AndNullScopeToo()
        {
            var (service, store, access, _) = Create(new EmployeeAccessScope(Staff, false, new[] { 2 }));
            store.HopDongs.Add(Contract(10, Root, TrangThaiHopDong.DangHoatDong));

            Assert.Equal(ServiceErrorKind.Forbidden, (await service.GetChiTietAsync(Staff, 10)).ErrorKind);

            access.ScopeToReturn = null;
            Assert.Equal(ServiceErrorKind.Forbidden, (await service.GetChiTietAsync(Staff, 10)).ErrorKind);
            Assert.Equal(ServiceErrorKind.NotFound, (await service.GetChiTietAsync(Staff, 999)).ErrorKind);
        }

        [Fact]
        public async Task History_SummarisesRoots_Roles_Months_AndDebt()
        {
            var (service, store, _, _) = Create();
            store.NguoiThues.Add(Tenant);
            store.RentalRows.Add((1, new LichSuThueRow { HopDongId = 10, MaHopDong = Root, LaNguoiDungTen = true, SoPhong = "P101", TenChiNhanh = "CN Một", ThoiDiemBatDau = new DateTime(2025, 10, 31, 17, 0, 0, DateTimeKind.Utc), ThoiDiemKetThuc = new DateTime(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc), TrangThai = 2, ConNo = 0 }));
            store.RentalRows.Add((1, new LichSuThueRow { HopDongId = 11, MaHopDong = Root + "-GH1", LaNguoiDungTen = true, SoPhong = "P101", TenChiNhanh = "CN Một", ThoiDiemBatDau = new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), ThoiDiemKetThuc = null, TrangThai = 1, ConNo = 700_000m }));
            store.RentalRows.Add((1, new LichSuThueRow { HopDongId = 20, MaHopDong = "HD-CN-2401-005", LaNguoiDungTen = false, SoPhong = "P202", TenChiNhanh = "CN Một", ThoiDiemBatDau = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), ThoiDiemKetThuc = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), TrangThai = 2, ConNo = 999m }));

            var dto = (await service.GetLichSuThueAsync(Staff, 3)).Data!;

            Assert.Equal(1, dto.SoLanThue);
            Assert.Equal(3, dto.SoHopDong);
            Assert.Equal(1, dto.SoLanOGhep);
            Assert.Equal(700_000m, dto.NoHienTai);
            Assert.Equal(new[] { "Đứng tên", "Đứng tên", "Ở ghép" }, dto.Dong.Select(d => d.VaiTro).ToArray());
            // 01/11/2025 -> 05/11/2026 (hiện tại) hợp nhất = 369 ngày, cộng 2024-01-01 -> 2024-03-01 = 60 ngày: 429 / 30 = 14.
            Assert.Equal(14, dto.TongSoThang);
        }

        [Fact]
        public async Task History_StaffCannotSeeTenant_IsNotFound_ScopeNullForbidden()
        {
            var (service, store, access, visible) = Create();
            store.NguoiThues.Add(Tenant);
            visible.Hidden.Add(3);

            var hidden = await service.GetLichSuThueAsync(Staff, 3);
            Assert.Equal(ServiceErrorKind.NotFound, hidden.ErrorKind);
            Assert.Equal(ServiceErrorKind.NotFound, (await service.GetNoCuAsync(Staff, 3)).ErrorKind);

            access.ScopeToReturn = null;
            Assert.Equal(ServiceErrorKind.Forbidden, (await service.GetLichSuThueAsync(Staff, 3)).ErrorKind);
            Assert.Equal(ServiceErrorKind.NotFound, (await Create().Service.GetLichSuThueAsync(Staff, 404)).ErrorKind);
        }

        [Fact]
        public async Task OldDebt_SumsDebtAndFiltersByBranch()
        {
            var (service, store, _, _) = Create();
            store.NguoiThues.Add(Tenant);
            store.OldDebtRows.Add((1, new NoCuRow { MaHoaDon = "HDN-1", MaHopDong = Root, Thang = 9, Nam = 2026, ConLai = 400_000m }));
            store.OldDebtRows.Add((1, new NoCuRow { MaHoaDon = "HDN-2", MaHopDong = Root, Thang = 10, Nam = 2026, ConLai = 600_000m }));
            store.OldDebtRows.Add((2, new NoCuRow { MaHoaDon = "HDN-3", MaHopDong = "X", Thang = 1, Nam = 2026, ConLai = 9_000m }));

            var dto = (await service.GetNoCuAsync(Staff, 3)).Data!;
            Assert.Equal(1_000_000m, dto.TongNo);
            Assert.Equal(2, dto.HoaDons.Count);

            var admin = Create(new EmployeeAccessScope(1, true));
            admin.Store.NguoiThues.Add(Tenant);
            admin.Store.OldDebtRows.AddRange(store.OldDebtRows);
            Assert.Equal(1_009_000m, (await admin.Service.GetNoCuAsync(1, 3)).Data!.TongNo);
        }
    }
}
