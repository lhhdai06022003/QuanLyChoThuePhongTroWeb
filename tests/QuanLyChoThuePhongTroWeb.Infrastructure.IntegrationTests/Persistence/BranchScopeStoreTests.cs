using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Các truy vấn lọc theo chi nhánh chạy được trên PostgreSQL thật (không đánh giá phía client) và lọc đúng.
    [Collection("PostgreSqlCollection")]
    public class BranchScopeStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public BranchScopeStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed record World(int BranchA, int BranchB, int RoomA, int RoomB,
            int EndedInA, int MemberOnlyInA, int OnlyInB, int NoContract, int DeletedContractOnly, int ContractA, int ContractB);

        private static async Task<World> SeedAsync(ApplicationDbContext db)
        {
            var s = Guid.NewGuid().ToString("N")[..8];
            var a = new ChiNhanh { TenChiNhanh = $"A{s}", MaChiNhanh = $"A{s[..5]}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "A" };
            var b = new ChiNhanh { TenChiNhanh = $"B{s}", MaChiNhanh = $"B{s[..5]}", DiaChi = "B", SoDienThoai = "0900000002", MoTa = "B" };
            db.ChiNhanhs.AddRange(a, b);
            await db.SaveChangesAsync();

            var roomA = new PhongTro { ChiNhanhId = a.ChiNhanhId, SoPhong = $"A{s[..5]}", GiaThue = 1m, DienTich = 10, MoTa = "A" };
            var roomB = new PhongTro { ChiNhanhId = b.ChiNhanhId, SoPhong = $"B{s[..5]}", GiaThue = 1m, DienTich = 10, MoTa = "B" };
            db.PhongTros.AddRange(roomA, roomB);

            NguoiThue T(string tag) => new() { HoVaTen = $"{tag} {s}", SoDienThoai = "09" + Random.Shared.Next(10000000, 99999999), CCCD = $"{tag}{s}", Email = $"{tag}{s}@t.vn" };
            var endedInA = T("ended");
            var memberOnlyInA = T("member");
            var onlyInB = T("onlyb");
            var noContract = T("free");
            var deletedOnly = T("deleted");
            db.NguoiThues.AddRange(endedInA, memberOnlyInA, onlyInB, noContract, deletedOnly);
            await db.SaveChangesAsync();

            HopDong C(string code, int room, int tenant, TrangThaiHopDong state, bool deleted = false) => new()
            {
                MaHopDong = $"{code}{s}", PhongTroId = room, NguoiThueId = tenant, ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-6),
                TienCocPhong = 1m, TienThuePhong = 1m, TrangThaiHopDong = state, IsDeleted = deleted
            };
            var ended = C("E", roomA.PhongTroId, endedInA.NguoiThueId, TrangThaiHopDong.DaKetThuc);
            var contractB = C("B", roomB.PhongTroId, onlyInB.NguoiThueId, TrangThaiHopDong.DangHoatDong);
            var deletedContract = C("D", roomB.PhongTroId, deletedOnly.NguoiThueId, TrangThaiHopDong.DaKetThuc, deleted: true);
            db.HopDongs.AddRange(ended, contractB, deletedContract);
            await db.SaveChangesAsync();

            // Chỉ ở ghép (không đứng tên) trong hợp đồng chi nhánh A.
            db.ChiTietThanhVienHopDongs.Add(new ChiTietThanhVienHopDong { HopDongId = ended.HopDongId, NguoiThueId = memberOnlyInA.NguoiThueId, NgayVao = DateTime.UtcNow.AddMonths(-5) });
            await db.SaveChangesAsync();

            return new World(a.ChiNhanhId, b.ChiNhanhId, roomA.PhongTroId, roomB.PhongTroId,
                endedInA.NguoiThueId, memberOnlyInA.NguoiThueId, onlyInB.NguoiThueId, noContract.NguoiThueId, deletedOnly.NguoiThueId,
                ended.HopDongId, contractB.HopDongId);
        }

        [Fact]
        public async Task NguoiThueStore_VisibilityByBranch()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new NguoiThueStore(db);
            var onlyA = new[] { w.BranchA };

            Assert.True(await store.IsVisibleAsync(w.EndedInA, onlyA));        // hợp đồng đã kết thúc vẫn tính
            Assert.True(await store.IsVisibleAsync(w.MemberOnlyInA, onlyA));   // chỉ ở ghép vẫn tính
            Assert.True(await store.IsVisibleAsync(w.NoContract, onlyA));      // chưa có hợp đồng: ai cũng thấy
            Assert.True(await store.IsVisibleAsync(w.DeletedContractOnly, onlyA)); // hợp đồng đã xóa mềm không tính
            Assert.False(await store.IsVisibleAsync(w.OnlyInB, onlyA));

            var empty = Array.Empty<int>();
            Assert.True(await store.IsVisibleAsync(w.NoContract, empty));
            Assert.False(await store.IsVisibleAsync(w.EndedInA, empty));

            var all = (await store.GetAllAsync(onlyA)).Select(x => x.NguoiThueId).ToList();
            Assert.Contains(w.EndedInA, all);
            Assert.Contains(w.NoContract, all);
            Assert.DoesNotContain(w.OnlyInB, all);

            var dropdown = (await store.GetDropdownListAsync(onlyA)).Select(x => int.Parse(x.Value)).ToList();
            Assert.DoesNotContain(w.OnlyInB, dropdown);
            var unrestricted = (await store.GetAllAsync()).Select(x => x.NguoiThueId).ToList();
            Assert.Contains(w.OnlyInB, unrestricted);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HopDongStore_DataTable_FiltersBeforeCountingTotal()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new HopDongStore(db);

            var page = await store.GetDataTableResponseAsync(new HopDongFilterReq { Start = 0, Length = 1000, AllowedBranchIds = new[] { w.BranchA } });

            var ids = page.data.Select(x => x.HopDongId).ToList();
            Assert.Contains(w.ContractA, ids);
            Assert.DoesNotContain(w.ContractB, ids);
            Assert.Equal(page.recordsFiltered, page.recordsTotal);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task PhongTroStore_ListsFilterByBranch()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new PhongTroStore(db);
            var onlyA = new[] { w.BranchA };

            Assert.DoesNotContain(await store.GetDanhSachPhongTroAsync(onlyA), x => x.PhongTroId == w.RoomB);
            Assert.DoesNotContain(await store.GetDanhSachPhongTroConTrongAsync(onlyA), x => x.PhongTroId == w.RoomB);
            Assert.DoesNotContain(await store.GetSoDoPhongAsync(0, onlyA), x => x.PhongTroId == w.RoomB);
            Assert.DoesNotContain(await store.GetChiNhanhDropdownAsync(onlyA), x => x.Value == w.BranchB.ToString());
            Assert.Contains(await store.GetDanhSachPhongTroAsync(), x => x.PhongTroId == w.RoomB);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task YeuCauSuCoStore_ListFiltersByBranch()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            YeuCauSuCo Incident(int room, int tenant) => new()
            {
                PhongTroId = room, NguoiThueId = tenant, TieuDe = "Hong den", MoTa = "M",
                TrangThai = TrangThaiSuCo.ChoTiepNhan, NgayGui = DateTime.UtcNow
            };
            var inA = Incident(w.RoomA, w.EndedInA);
            var inB = Incident(w.RoomB, w.OnlyInB);
            db.YeuCauSuCos.AddRange(inA, inB);
            await db.SaveChangesAsync();
            var store = new YeuCauSuCoStore(db);

            var onlyA = (await store.GetAllAsync(null, null, 6, new[] { w.BranchA })).Select(x => x.Id).ToList();
            Assert.Contains(inA.Id, onlyA);
            Assert.DoesNotContain(inB.Id, onlyA);

            // Lọc chi nhánh B trong khi chỉ được phép chi nhánh A: không có dữ liệu.
            Assert.Empty(await store.GetAllAsync(w.BranchB, null, 6, new[] { w.BranchA }));
            Assert.Empty(await store.GetAllAsync(null, null, 6, Array.Empty<int>()));

            var unrestricted = (await store.GetAllAsync(null, null, 6)).Select(x => x.Id).ToList();
            Assert.Contains(inA.Id, unrestricted);
            Assert.Contains(inB.Id, unrestricted);

            await tx.RollbackAsync();
        }
    }
}
