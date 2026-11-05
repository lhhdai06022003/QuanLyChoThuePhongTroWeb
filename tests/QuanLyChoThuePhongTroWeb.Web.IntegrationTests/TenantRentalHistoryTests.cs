using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Chi tiết người thuê (lịch sử thuê theo chi nhánh), nợ cũ, bộ lọc hóa đơn còn nợ của hợp đồng đã kết thúc, tìm kiếm hợp đồng.
    [Collection(WebTestCollection.Name)]
    public class TenantRentalHistoryTests
    {
        private const string Page = "/QuanLyNhaTro/QuanLyHopDong";

        private readonly CustomWebApplicationFactory _factory;

        public TenantRentalHistoryTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Người thuê A đứng tên hợp đồng A (chi nhánh A) và ở ghép hợp đồng B (chi nhánh B).
        private async Task<BranchScopeWorld> SeedWorldAsync()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ChiTietThanhVienHopDongs.Add(new ChiTietThanhVienHopDong { HopDongId = world.ContractB, NguoiThueId = world.TenantA, NgayVao = DateTime.UtcNow.AddMonths(-1) });
            await db.SaveChangesAsync();
            return world;
        }

        private async Task<(int HopDongId, string MaHopDong, int HoaDonId)> AddEndedContractWithDebtAsync(int phongId, int nguoiThueId, decimal tong)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var contract = new HopDong
            {
                MaHopDong = $"HDE_{Guid.NewGuid():N}"[..16],
                PhongTroId = phongId,
                NguoiThueId = nguoiThueId,
                ThoiDiemBatDau = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2025, 12, 31, 16, 59, 59, DateTimeKind.Utc),
                TienCocPhong = 1m,
                TienThuePhong = 1m,
                TrangThaiHopDong = TrangThaiHopDong.DaKetThuc
            };
            db.HopDongs.Add(contract);
            await db.SaveChangesAsync();
            var invoice = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HDN_{Guid.NewGuid():N}"[..16],
                Thang = 12,
                Nam = 2025,
                TongTien = tong,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = DateTime.UtcNow
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();
            return (contract.HopDongId, contract.MaHopDong, invoice.HoaDonId);
        }

        private async Task<string> CodeOfAsync(int hopDongId)
            => await BranchScopeKit.ReadAsync(_factory, db => db.HopDongs.Where(h => h.HopDongId == hopDongId).Select(h => h.MaHopDong).FirstAsync());

        private static async Task<string> HtmlAsync(HttpResponseMessage res) => WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());

        [Fact]
        public async Task LichSuThue_Admin_SeesAllRows()
        {
            var world = await SeedWorldAsync();
            var (admin, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);

            var res = await admin.GetAsync($"/QuanLyNhaTro/NguoiThue/ChiTiet/{world.TenantA}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = await HtmlAsync(res);
            Assert.Contains(await CodeOfAsync(world.ContractA), html);
            Assert.Contains(await CodeOfAsync(world.ContractB), html);
            Assert.Contains("Ở ghép", html);
        }

        [Fact]
        public async Task LichSuThue_StaffBranchA_SeesOnlyBranchARows_SummaryCountsOnlyVisible()
        {
            var world = await SeedWorldAsync();
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            var res = await staff.GetAsync($"/QuanLyNhaTro/NguoiThue/ChiTiet/{world.TenantA}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = await HtmlAsync(res);
            Assert.Contains(await CodeOfAsync(world.ContractA), html);
            Assert.DoesNotContain(await CodeOfAsync(world.ContractB), html);
            Assert.DoesNotContain("Ở ghép", html);
        }

        [Fact]
        public async Task LichSuThue_StaffCannotSeeTenant_Is404()
        {
            var world = await SeedWorldAsync();
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/QuanLyNhaTro/NguoiThue/ChiTiet/{world.TenantB}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/NguoiThue/NoCu/{world.TenantB}")).StatusCode);
        }

        [Fact]
        public async Task NoCu_ReturnsDebtOfEndedContract()
        {
            var world = await SeedWorldAsync();
            var ended = await AddEndedContractWithDebtAsync(world.RoomA, world.TenantFree, 1_250_000m);
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            var debt = await BranchScopeKit.JsonAsync(await staff.GetAsync($"/NguoiThue/NoCu/{world.TenantFree}"));
            var clean = await BranchScopeKit.JsonAsync(await staff.GetAsync($"/NguoiThue/NoCu/{world.TenantA}"));

            Assert.Equal(1_250_000m, debt.GetProperty("tongNo").GetDecimal());
            Assert.Equal(ended.MaHopDong, debt.GetProperty("hoaDons")[0].GetProperty("maHopDong").GetString());
            Assert.Equal(0m, clean.GetProperty("tongNo").GetDecimal());
        }

        [Fact]
        public async Task InvoiceList_FilterEndedContractDebt()
        {
            var world = await SeedWorldAsync();
            var ended = await AddEndedContractWithDebtAsync(world.RoomA, world.TenantFree, 1_250_000m);
            // Hóa đơn đã gửi còn nợ của hợp đồng ĐANG hoạt động cùng chi nhánh: bộ lọc phải loại.
            var activeDebtInvoiceId = await BranchScopeKit.ReadAsync(_factory, async db =>
            {
                var invoice = new HoaDon
                {
                    HopDongId = world.ContractA,
                    MaHoaDon = $"HDA_{Guid.NewGuid():N}"[..16],
                    Thang = 1,
                    Nam = 2024,
                    TongTien = 900_000m,
                    TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                    TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                    NgayGui = DateTime.UtcNow
                };
                db.HoaDons.Add(invoice);
                await db.SaveChangesAsync();
                return invoice.HoaDonId;
            });
            var (staff, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, "/QuanLyNhaTro/QuanLyHoaDon");

            var res = await BranchScopeKit.PostFormAsync(staff, token, "/HoaDon/GetList", new Dictionary<string, string>
            {
                ["draw"] = "1",
                ["start"] = "0",
                ["length"] = "50",
                ["chiNhanhId"] = world.BranchA.ToString(),
                ["thang"] = "0",
                ["nam"] = "0",
                ["trangThaiPhatHanh"] = "3",
                ["hopDongDaKetThucConNo"] = "1"
            });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var ids = (await BranchScopeKit.JsonAsync(res)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("hoaDonId").GetInt32()).ToList();
            Assert.Contains(ended.HoaDonId, ids);
            Assert.DoesNotContain(activeDebtInvoiceId, ids);
            var contractStatuses = await BranchScopeKit.ReadAsync(_factory, db => db.HoaDons
                .Where(h => ids.Contains(h.HoaDonId))
                .Select(h => h.HopDong.TrangThaiHopDong)
                .ToListAsync());
            Assert.All(contractStatuses, s => Assert.Equal(TrangThaiHopDong.DaKetThuc, s));
        }

        [Fact]
        public async Task ContractList_SearchByMemberPhone()
        {
            var world = await SeedWorldAsync();
            var phone = await BranchScopeKit.ReadAsync(_factory, db => db.NguoiThues.Where(n => n.NguoiThueId == world.TenantB).Select(n => n.SoDienThoai).FirstAsync());
            var (admin, token) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);

            var res = await BranchScopeKit.PostFormAsync(admin, token, "/HopDong/DanhSachHopDongSideAsync", new Dictionary<string, string>
            {
                ["draw"] = "1",
                ["start"] = "0",
                ["length"] = "50",
                ["search[value]"] = phone
            });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var ids = (await BranchScopeKit.JsonAsync(res)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("hopDongId").GetInt32()).ToList();
            Assert.Contains(world.ContractB, ids);
        }

        [Fact]
        public async Task ContractList_BoLocQuaHan()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                (await db.HopDongs.FirstAsync(h => h.HopDongId == world.ContractA)).ThoiDiemKetThuc = DateTime.UtcNow.AddDays(-3);
                await db.SaveChangesAsync();
            }

            var code = await CodeOfAsync(world.ContractA);
            var (admin, token) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);
            var res = await BranchScopeKit.PostFormAsync(admin, token, "/HopDong/DanhSachHopDongSideAsync", new Dictionary<string, string>
            {
                ["draw"] = "1",
                ["start"] = "0",
                ["length"] = "100",
                ["search[value]"] = code,
                ["boLoc"] = "QuaHan"
            });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var ids = (await BranchScopeKit.JsonAsync(res)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("hopDongId").GetInt32()).ToList();
            Assert.Contains(world.ContractA, ids);
        }
    }
}
