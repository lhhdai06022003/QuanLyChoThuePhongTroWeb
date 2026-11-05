using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Store của thanh lý, gia hạn, lịch sử thuê và các truy vấn liên quan (mã gia hạn, tìm kiếm, bộ lọc, hóa đơn nợ).
    // Dữ liệu seed có hậu tố Guid riêng và assert chỉ trên dữ liệu vừa seed (DB dùng chung).
    [Collection("PostgreSqlCollection")]
    public class HopDongLifecycleStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public HopDongLifecycleStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static string UniquePhone() => "0" + Random.Shared.NextInt64(100_000_000, 999_999_999);

        private static async Task<HopDong> WithCodeAsync(ApplicationDbContext db, HopDong contract, string code)
        {
            contract.MaHopDong = code;
            await db.SaveChangesAsync();
            return contract;
        }

        [Fact]
        public async Task CountContractsWithPrefix_IgnoresRenewalCodes()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("P");
            var tenant = await seed.TenantAsync("P");
            var prefix = $"HD-X{seed.Suffix}-2611-";
            await WithCodeAsync(db, await seed.ContractAsync(await seed.RoomAsync(branch, "P"), tenant), prefix + "001");
            await WithCodeAsync(db, await seed.ContractAsync(await seed.RoomAsync(branch, "P"), tenant, status: TrangThaiHopDong.DaKetThuc), prefix + "001-GH1");
            await WithCodeAsync(db, await seed.ContractAsync(await seed.RoomAsync(branch, "P"), tenant), prefix + "002");

            var count = await new HopDongStore(db).CountContractsWithPrefixAsync(prefix);

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task CountActiveRenewals_CountsOnlyNonDeletedRenewalsOfRoot()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("R");
            var tenant = await seed.TenantAsync("R");
            var root = $"HD-R{seed.Suffix}-001";
            async Task Add(string code, bool deleted = false) =>
                await WithCodeAsync(db, await seed.ContractAsync(await seed.RoomAsync(branch, "R"), tenant, status: TrangThaiHopDong.DaKetThuc, deleted: deleted), code);
            await Add(root);
            await Add(root + "-GH1");
            await Add(root + "-GH2", deleted: true);
            await Add(root + "0-GH1");

            var store = new HopDongLifecycleStore(db);

            Assert.Equal(1, await store.CountActiveRenewalsAsync(root));
            Assert.True(await store.ExistsContractWithCodeAsync(root + "-GH2"));
        }

        [Fact]
        public async Task GetChain_ReturnsRootAndRenewals_NotSimilarPrefix()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("C");
            var tenant = await seed.TenantAsync("C");
            var root = $"HD-C{seed.Suffix}-001";
            foreach (var code in new[] { root, root + "-GH1", root + "0" })
            {
                await WithCodeAsync(db, await seed.ContractAsync(await seed.RoomAsync(branch, "C"), tenant, status: TrangThaiHopDong.DaKetThuc), code);
            }

            var chain = await new HopDongLifecycleStore(db).GetChainAsync(root);

            Assert.Equal(new[] { root, root + "-GH1" }, chain.Select(c => c.MaHopDong).OrderBy(c => c).ToArray());
        }

        [Fact]
        public async Task RentalHistory_IncludesRepresentativeAndMember_FilteredByBranch_WithDebt()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var b1 = await seed.BranchAsync("H1");
            var b2 = await seed.BranchAsync("H2");
            var a = await seed.TenantAsync("A");
            var other = await seed.TenantAsync("O");
            var c1 = await seed.ContractAsync(await seed.RoomAsync(b1, "H"), a, status: TrangThaiHopDong.DaKetThuc);
            var c2 = await seed.ContractAsync(await seed.RoomAsync(b2, "H"), other);
            var cancelled = await seed.ContractAsync(await seed.RoomAsync(b1, "H"), a, status: TrangThaiHopDong.DaHuy);
            db.ChiTietThanhVienHopDongs.Add(new ChiTietThanhVienHopDong { HopDongId = c2.HopDongId, NguoiThueId = a.NguoiThueId, NgayVao = c2.ThoiDiemBatDau });
            await db.SaveChangesAsync();
            await seed.InvoiceAsync(c1, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 1_000_000m, null, paid: 400_000m, paidDeleted: 100_000m);
            await seed.InvoiceAsync(c1, TrangThaiPhatHanhHoaDon.Nhap, TrangThaiHoaDon.ChuaThanhToan, 9_000_000m, null);

            var store = new HopDongLifecycleStore(db);
            var all = await store.GetRentalHistoryAsync(a.NguoiThueId, null);
            var onlyB1 = await store.GetRentalHistoryAsync(a.NguoiThueId, new[] { b1.ChiNhanhId });

            Assert.Equal(2, all.Count);
            Assert.DoesNotContain(all, r => r.HopDongId == cancelled.HopDongId);
            Assert.True(all.Single(r => r.HopDongId == c1.HopDongId).LaNguoiDungTen);
            Assert.False(all.Single(r => r.HopDongId == c2.HopDongId).LaNguoiDungTen);
            Assert.Equal(600_000m, all.Single(r => r.HopDongId == c1.HopDongId).ConNo);
            var single = Assert.Single(onlyB1);
            Assert.Equal(c1.HopDongId, single.HopDongId);
        }

        [Fact]
        public async Task OldDebts_OnlyEndedContractsOfRepresentative()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("D");
            var a = await seed.TenantAsync("A");
            var other = await seed.TenantAsync("O");
            var ended = await seed.ContractAsync(await seed.RoomAsync(branch, "D"), a, status: TrangThaiHopDong.DaKetThuc);
            var active = await seed.ContractAsync(await seed.RoomAsync(branch, "D"), a);
            var othersEnded = await seed.ContractAsync(await seed.RoomAsync(branch, "D"), other, status: TrangThaiHopDong.DaKetThuc);
            await seed.InvoiceAsync(ended, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 1_000_000m, null, paid: 250_000m);
            await seed.InvoiceAsync(ended, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan, 500_000m, null, paid: 500_000m);
            await seed.InvoiceAsync(active, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 700_000m, null);
            await seed.InvoiceAsync(othersEnded, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 800_000m, null);

            var debts = await new HopDongLifecycleStore(db).GetOldDebtsAsync(a.NguoiThueId, null);

            var row = Assert.Single(debts);
            Assert.Equal(750_000m, row.ConLai);
            Assert.Equal(ended.MaHopDong, row.MaHopDong);
            Assert.Empty(await new HopDongLifecycleStore(db).GetOldDebtsAsync(a.NguoiThueId, new[] { branch.ChiNhanhId + 100_000 }));
        }

        [Fact]
        public async Task LockHopDong_And_LockInvoices_ReturnTrackedEntities()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("L");
            var tenant = await seed.TenantAsync("L");
            var contract = await seed.ContractAsync(await seed.RoomAsync(branch, "L"), tenant);
            var invoice = await seed.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 1_000_000m, null);
            db.ChangeTracker.Clear();

            var store = new HopDongLifecycleStore(db);
            await using var tx = await db.Database.BeginTransactionAsync();
            var locked = await store.LockHopDongAsync(contract.HopDongId);
            var lockedInvoices = await store.LockInvoicesOfContractsAsync(new[] { contract.HopDongId });
            Assert.NotNull(locked);
            locked!.TrangThaiHopDong = TrangThaiHopDong.DaKetThuc;
            lockedInvoices.Single().TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan;
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await using var verify = _fixture.CreateDbContext();
            Assert.Equal(TrangThaiHopDong.DaKetThuc, (await verify.HopDongs.FindAsync(contract.HopDongId))!.TrangThaiHopDong);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await verify.HoaDons.FindAsync(invoice.HoaDonId))!.TrangThaiHoaDon);
        }

        [Fact]
        public async Task LockInvoicesOfContracts_MultipleIds_AnyArray_LocksWholeChainOnly_OrderedById_SkipsDeleted()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("A");
            var tenant = await seed.TenantAsync("A");
            var room = await seed.RoomAsync(branch, "A");
            var oldContract = await seed.ContractAsync(room, tenant, status: TrangThaiHopDong.DaKetThuc);
            var renewal = await seed.ContractAsync(room, tenant);
            var other = await seed.ContractAsync(await seed.RoomAsync(branch, "A"), tenant);
            var invOld = await seed.InvoiceAsync(oldContract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 1_000_000m, null);
            var invNew = await seed.InvoiceAsync(renewal, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 2_000_000m, null);
            await seed.InvoiceAsync(renewal, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 3_000_000m, null, deleted: true);
            await seed.InvoiceAsync(other, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 4_000_000m, null);
            db.ChangeTracker.Clear();

            var store = new HopDongLifecycleStore(db);
            await using var tx = await db.Database.BeginTransactionAsync();
            // Truyền lộn thứ tự và trùng id để chắc ANY(@ids) chạy thật trên PostgreSQL.
            var locked = await store.LockInvoicesOfContractsAsync(new[] { renewal.HopDongId, oldContract.HopDongId, renewal.HopDongId });
            await tx.RollbackAsync();

            Assert.Equal(new[] { invOld.HoaDonId, invNew.HoaDonId }, locked.Select(i => i.HoaDonId).ToArray());
            Assert.Empty(await store.LockInvoicesOfContractsAsync(Array.Empty<int>()));
        }

        [Fact]
        public async Task DataTable_SearchByMemberCccdAndPhone()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("S");
            var rep = await seed.TenantAsync("S");
            var member = await seed.TenantAsync("M");
            var phone = UniquePhone();
            member.SoDienThoai = phone;
            // CCCD thật chỉ gồm chữ số; seeder sinh chữ hoa mà bộ tìm kiếm so khớp với chuỗi đã hạ chữ thường.
            member.CCCD = "9" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
            await db.SaveChangesAsync();
            var contract = await seed.ContractAsync(await seed.RoomAsync(branch, "S"), rep);
            db.ChiTietThanhVienHopDongs.Add(new ChiTietThanhVienHopDong { HopDongId = contract.HopDongId, NguoiThueId = member.NguoiThueId, NgayVao = contract.ThoiDiemBatDau });
            await db.SaveChangesAsync();

            var store = new HopDongStore(db);
            var byPhone = await store.GetDataTableResponseAsync(new HopDongFilterReq { Start = 0, Length = 20, SearchValue = phone, AllowedBranchIds = new[] { branch.ChiNhanhId } });
            var byCccd = await store.GetDataTableResponseAsync(new HopDongFilterReq { Start = 0, Length = 20, SearchValue = member.CCCD, AllowedBranchIds = new[] { branch.ChiNhanhId } });

            Assert.Equal(contract.HopDongId, Assert.Single(byPhone.data).HopDongId);
            Assert.Equal(contract.HopDongId, Assert.Single(byCccd.data).HopDongId);
        }

        [Fact]
        public async Task DataTable_BoLoc_QuaHan_SapHetHan_GiaHan()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("F");
            var tenant = await seed.TenantAsync("F");
            // Hôm nay VN = 05/11/2026, đầu ngày = 2026-11-04T17:00Z.
            var moc = new DateTime(2026, 11, 4, 17, 0, 0, DateTimeKind.Utc);
            var overdue = await seed.ContractAsync(await seed.RoomAsync(branch, "F"), tenant, end: moc.AddSeconds(-1));
            var today = await seed.ContractAsync(await seed.RoomAsync(branch, "F"), tenant, end: moc);
            var in30 = await seed.ContractAsync(await seed.RoomAsync(branch, "F"), tenant, end: moc.AddDays(31).AddSeconds(-1));
            var in31 = await seed.ContractAsync(await seed.RoomAsync(branch, "F"), tenant, end: moc.AddDays(31));
            var renewal = await seed.ContractAsync(await seed.RoomAsync(branch, "F"), tenant);
            await WithCodeAsync(db, renewal, $"HD-F{seed.Suffix}-001-GH1");

            async Task<int[]> Ids(string boLoc)
            {
                var res = await new HopDongStore(db).GetDataTableResponseAsync(new HopDongFilterReq
                {
                    Start = 0,
                    Length = 50,
                    BoLoc = boLoc,
                    MocDauNgayHomNayUtc = moc,
                    AllowedBranchIds = new[] { branch.ChiNhanhId }
                });
                return res.data.Select(x => x.HopDongId).OrderBy(x => x).ToArray();
            }

            Assert.Equal(new[] { overdue.HopDongId }, await Ids("QuaHan"));
            Assert.Equal(new[] { today.HopDongId, in30.HopDongId }.OrderBy(x => x).ToArray(), await Ids("SapHetHan"));
            Assert.Equal(new[] { renewal.HopDongId }, await Ids("GiaHan"));
        }

        [Fact]
        public async Task DataTable_MaHopDongKeTiep_SkipsCancelledAndDeleted_AndFillsPhongTroId()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("K");
            var tenant = await seed.TenantAsync("K");
            var room = await seed.RoomAsync(branch, "K");
            var root = $"HD-K{seed.Suffix}-001";
            var goc = await WithCodeAsync(db, await seed.ContractAsync(room, tenant, status: TrangThaiHopDong.DaKetThuc), root);
            // GH1 bị hủy và GH2 đã xóa mềm: không được tính là kế tiếp; gốc phải trỏ thẳng tới GH3.
            await WithCodeAsync(db, await seed.ContractAsync(room, tenant, status: TrangThaiHopDong.DaHuy), root + "-GH1");
            await WithCodeAsync(db, await seed.ContractAsync(room, tenant, status: TrangThaiHopDong.DaKetThuc, deleted: true), root + "-GH2");
            var gh3 = await WithCodeAsync(db, await seed.ContractAsync(room, tenant, status: TrangThaiHopDong.DaKetThuc), root + "-GH3");
            var gh4 = await WithCodeAsync(db, await seed.ContractAsync(room, tenant), root + "-GH4");
            var khac = await seed.ContractAsync(await seed.RoomAsync(branch, "K"), tenant, status: TrangThaiHopDong.DaKetThuc);

            var res = await new HopDongStore(db).GetDataTableResponseAsync(new HopDongFilterReq
            {
                Start = 0,
                Length = 50,
                AllowedBranchIds = new[] { branch.ChiNhanhId }
            });
            var rows = res.data.ToDictionary(x => x.HopDongId);

            Assert.Equal(root + "-GH3", rows[goc.HopDongId].MaHopDongKeTiep);
            Assert.Equal(root + "-GH4", rows[gh3.HopDongId].MaHopDongKeTiep);
            Assert.Null(rows[gh4.HopDongId].MaHopDongKeTiep);
            Assert.Null(rows[khac.HopDongId].MaHopDongKeTiep);
            Assert.Equal(room.PhongTroId, rows[goc.HopDongId].PhongTroId);
        }

        [Fact]
        public async Task DataTable_SortByThoiDiemKetThuc()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("E");
            var tenant = await seed.TenantAsync("E");
            var muon = await seed.ContractAsync(await seed.RoomAsync(branch, "E"), tenant, end: new DateTime(2027, 3, 31, 16, 59, 59, DateTimeKind.Utc));
            var som = await seed.ContractAsync(await seed.RoomAsync(branch, "E"), tenant, end: new DateTime(2026, 12, 31, 16, 59, 59, DateTimeKind.Utc));

            async Task<int[]> Ids(string huong)
            {
                var res = await new HopDongStore(db).GetDataTableResponseAsync(new HopDongFilterReq
                {
                    Start = 0,
                    Length = 50,
                    SortColumnName = "thoiDiemKetThuc",
                    SortDirection = huong,
                    AllowedBranchIds = new[] { branch.ChiNhanhId }
                });
                return res.data.Select(x => x.HopDongId).ToArray();
            }

            Assert.Equal(new[] { som.HopDongId, muon.HopDongId }, await Ids("asc"));
            Assert.Equal(new[] { muon.HopDongId, som.HopDongId }, await Ids("desc"));
        }

        [Fact]
        public async Task InvoiceList_HopDongDaKetThucConNo_ExcludesRenewedTenant()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("I");
            var left = await seed.TenantAsync("L");
            var renewed = await seed.TenantAsync("R");
            var leftEnded = await seed.ContractAsync(await seed.RoomAsync(branch, "I"), left, status: TrangThaiHopDong.DaKetThuc);
            var renewedRoom = await seed.RoomAsync(branch, "I");
            var renewedOld = await seed.ContractAsync(renewedRoom, renewed, status: TrangThaiHopDong.DaKetThuc);
            await seed.ContractAsync(renewedRoom, renewed);
            var leftPaid = await seed.ContractAsync(await seed.RoomAsync(branch, "I"), left, status: TrangThaiHopDong.DaKetThuc);
            var debt = await seed.InvoiceAsync(leftEnded, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 1_000_000m, null);
            var renewedDebt = await seed.InvoiceAsync(renewedOld, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 1_000_000m, null);
            var paid = await seed.InvoiceAsync(leftPaid, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan, 1_000_000m, null, paid: 1_000_000m);

            var res = await new InvoiceViewStore(db).GetEmployeeListAsync(
                new DataTableRequest { Start = 0, Length = 50 },
                new InvoiceListFilter { ChiNhanhId = branch.ChiNhanhId, HopDongDaKetThucConNo = true },
                null);

            var ids = res.data.Select(x => x.HoaDonId).ToList();
            Assert.Contains(debt.HoaDonId, ids);
            Assert.DoesNotContain(renewedDebt.HoaDonId, ids);
            Assert.DoesNotContain(paid.HoaDonId, ids);
        }
    }
}
