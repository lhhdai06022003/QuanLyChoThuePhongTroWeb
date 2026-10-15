using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterAnomalyCheckServiceTests
    {
        private const int Actor = 2;
        private const int Branch = 10;
        private const int Room = 1;

        private readonly FakeMeterImageStore _store = new();
        private readonly FakeMeterConsumptionHistoryStore _history = new();
        private readonly FakeEmployeeAccessService _access = new();
        private readonly MeterAnomalyCheckService _service;

        public MeterAnomalyCheckServiceTests()
        {
            // Lịch sử điện 6 kỳ trung vị 40 kWh: tiêu thụ 91 kWh là bất thường (ngưỡng 90).
            _history.History = new MeterConsumptionHistory(
                new[] { 38m, 40m, 40m, 42m, 45m, 39m }.Select(k => new MeterConsumption(k, 0m)).ToList(), 31, 31, 10);
            _store.RoomBranches[Room] = Branch;
            _access.Permissions.Add((Actor, Branch, EmployeeActionCodes.MeterRead));
            _service = new MeterAnomalyCheckService(_store, _history, _access, new AutoInvoiceOptions());
        }

        private static MeterAnomalyCheckRequest Req(decimal? dien, decimal? nuoc = null, int room = Room) =>
            new() { PhongTroId = room, Thang = 10, Nam = 2026, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc };

        [Fact]
        public async Task KiemTra_TraCanhBaoDienKhiVuotNguong()
        {
            _store.Periods[1] = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = Room, Thang = 10, Nam = 2026, ChiSoDienCu = 1000, ChiSoNuocCu = 50 };

            var bat = await _service.KiemTraAsync(Actor, Req(1091m));
            var thuong = await _service.KiemTraAsync(Actor, Req(1090m));

            Assert.True(bat.Success, bat.Message);
            Assert.Equal("TieuThuBatThuong", bat.Data!.Dien!.MaLyDo);
            Assert.Contains("Điện", bat.Data.Dien.MoTa);
            Assert.Null(bat.Data.Nuoc);
            Assert.Null(thuong.Data!.Dien);
        }

        [Fact]
        public async Task KiemTra_ChuaCoKy_DungChiSoKyTruoc()
        {
            _store.DefaultChiSoDienMoi = 1000m;

            var bat = await _service.KiemTraAsync(Actor, Req(1091m));

            Assert.NotNull(bat.Data!.Dien);
        }

        [Fact]
        public async Task KiemTra_ChiSoGiam_BaoChiSoGiam()
        {
            _store.Periods[1] = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = Room, Thang = 10, Nam = 2026, ChiSoDienCu = 1000, ChiSoNuocCu = 50 };

            var r = await _service.KiemTraAsync(Actor, Req(null, 40m));

            Assert.Null(r.Data!.Dien);
            Assert.Equal("ChiSoGiam", r.Data.Nuoc!.MaLyDo);
        }

        [Fact]
        public async Task KiemTra_ThieuQuyen_Forbidden()
        {
            var r = await _service.KiemTraAsync(99, Req(1091m));

            Assert.False(r.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, r.ErrorKind);
        }

        [Fact]
        public async Task KiemTra_PhongKhongTonTai_NotFound()
        {
            var r = await _service.KiemTraAsync(Actor, Req(1091m, room: 77));

            Assert.False(r.Success);
            Assert.Equal(ServiceErrorKind.NotFound, r.ErrorKind);
        }

        [Theory]
        [InlineData(0, 2026)]
        [InlineData(13, 2026)]
        [InlineData(10, 1999)]
        [InlineData(10, 2101)]
        public async Task KiemTra_KyKhongHopLe_Fail(int thang, int nam)
        {
            var r = await _service.KiemTraAsync(Actor, new MeterAnomalyCheckRequest { PhongTroId = Room, Thang = thang, Nam = nam, ChiSoDienMoi = 1091m });

            Assert.False(r.Success);
            Assert.Equal("Dữ liệu không hợp lệ.", r.Message);
        }
    }
}
