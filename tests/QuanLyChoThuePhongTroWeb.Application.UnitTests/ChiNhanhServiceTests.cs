using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Số giờ phản hồi hóa đơn của chi nhánh (1-168, mặc định 24).
    public class ChiNhanhServiceTests
    {
        private const string LoiSoGio = "Số giờ phản hồi hóa đơn phải là số nguyên từ 1 đến 168.";

        private sealed class FakeChiNhanhStore : IChiNhanhStore
        {
            public List<ChiNhanh> Items { get; } = new();
            public List<ChiNhanh> Added { get; } = new();
            public int ExistsCalls { get; private set; }

            public Task<IReadOnlyList<ChiNhanh>> GetAllActiveAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<ChiNhanh>>(Items.Where(x => !x.IsDeleted).ToList());

            public Task<ChiNhanh?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
                => Task.FromResult(Items.FirstOrDefault(x => x.ChiNhanhId == id));

            public Task<bool> ExistsMaAsync(string maChiNhanh, int excludeId = 0, CancellationToken cancellationToken = default)
            {
                ExistsCalls++;
                return Task.FromResult(false);
            }

            public void Add(ChiNhanh chiNhanh) => Added.Add(chiNhanh);
            public void Update(ChiNhanh chiNhanh) { }
        }

        private static (ChiNhanhService Service, FakeChiNhanhStore Store, RecordingUnitOfWork Uow) Create()
        {
            var store = new FakeChiNhanhStore();
            var uow = new RecordingUnitOfWork();
            return (new ChiNhanhService(store, uow, NullLogger<ChiNhanhService>.Instance), store, uow);
        }

        private static ChiNhanhReq Req(int id = 0, int? soGio = null) => new()
        {
            ChiNhanhId = id,
            MaChiNhanh = "cn1",
            TenChiNhanh = "Chi nhánh 1",
            DiaChi = "Địa chỉ",
            SoDienThoai = "0900000000",
            MoTa = "",
            SoGioPhanHoiHoaDon = soGio
        };

        [Theory]
        [InlineData(0)]
        [InlineData(169)]
        [InlineData(-1)]
        public async Task Create_OutOfRange_IsRejected_WithoutTouchingStore(int soGio)
        {
            var (service, store, uow) = Create();

            var (ok, message) = await service.ThemChiNhanhMoi(Req(soGio: soGio));

            Assert.False(ok);
            Assert.Equal(LoiSoGio, message);
            Assert.Empty(store.Added);
            Assert.Equal(0, store.ExistsCalls);
            Assert.Equal(0, uow.SaveChangesCalls);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(168)]
        public async Task Create_Boundaries_AreSaved(int soGio)
        {
            var (service, store, uow) = Create();

            var (ok, _) = await service.ThemChiNhanhMoi(Req(soGio: soGio));

            Assert.True(ok);
            Assert.Equal(soGio, Assert.Single(store.Added).SoGioPhanHoiHoaDon);
            Assert.Equal(1, uow.SaveChangesCalls);
        }

        [Fact]
        public async Task Create_WithoutValue_DefaultsTo24()
        {
            var (service, store, _) = Create();

            var (ok, _) = await service.ThemChiNhanhMoi(Req(soGio: null));

            Assert.True(ok);
            Assert.Equal(24, Assert.Single(store.Added).SoGioPhanHoiHoaDon);
        }

        [Fact]
        public async Task Update_WithValue_ChangesEntity()
        {
            var (service, store, uow) = Create();
            store.Items.Add(new ChiNhanh { ChiNhanhId = 5, MaChiNhanh = "CN1", SoGioPhanHoiHoaDon = 24 });

            var (ok, _) = await service.ThemChiNhanhMoi(Req(id: 5, soGio: 48));

            Assert.True(ok);
            Assert.Equal(48, store.Items[0].SoGioPhanHoiHoaDon);
            Assert.Equal(1, uow.SaveChangesCalls);
        }

        [Fact]
        public async Task Update_WithoutValue_KeepsCurrentValue()
        {
            var (service, store, _) = Create();
            store.Items.Add(new ChiNhanh { ChiNhanhId = 5, MaChiNhanh = "CN1", SoGioPhanHoiHoaDon = 72 });

            var (ok, _) = await service.ThemChiNhanhMoi(Req(id: 5, soGio: null));

            Assert.True(ok);
            Assert.Equal(72, store.Items[0].SoGioPhanHoiHoaDon);
        }

        [Fact]
        public async Task Update_WithZero_IsRejected_AndEntityUnchanged()
        {
            var (service, store, uow) = Create();
            store.Items.Add(new ChiNhanh { ChiNhanhId = 5, MaChiNhanh = "CN1", SoGioPhanHoiHoaDon = 72 });

            var (ok, message) = await service.ThemChiNhanhMoi(Req(id: 5, soGio: 0));

            Assert.False(ok);
            Assert.Equal(LoiSoGio, message);
            Assert.Equal(72, store.Items[0].SoGioPhanHoiHoaDon);
            Assert.Equal(0, uow.SaveChangesCalls);
        }

        [Fact]
        public async Task GetChiNhanh_ReturnsSoGioPhanHoiHoaDon()
        {
            var (service, store, _) = Create();
            store.Items.Add(new ChiNhanh { ChiNhanhId = 5, MaChiNhanh = "CN1", SoGioPhanHoiHoaDon = 36 });

            var res = await service.GetChiNhanh(5);

            Assert.Equal(36, res!.SoGioPhanHoiHoaDon);
        }
    }
}
