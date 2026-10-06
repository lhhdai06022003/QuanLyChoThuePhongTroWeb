using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    [Collection(WebTestCollection.Name)]
    public class ThongBaoChiNhanhTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public ThongBaoChiNhanhTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<string> CreateTenantAccountAsync(int nguoiThueId)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var name = "kh_" + Guid.NewGuid().ToString("N")[..8];
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = name, MatKhau = BranchScopeKit.Password, Role = AppRole.KhachThue, NguoiThueId = nguoiThueId })).Success);
            return name;
        }

        // Nhân viên chỉ được phân công chi nhánh B.
        private async Task<int> CreateStaffOfBranchBAsync(BranchScopeWorld w)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var name = "nvb_" + Guid.NewGuid().ToString("N")[..8];
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = name, MatKhau = BranchScopeKit.Password, Role = AppRole.NhanVien })).Success);
            var user = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == name);
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = user.NguoiDungId, ChiNhanhId = w.BranchB, IsActive = true,
                NguoiPhanCongId = user.NguoiDungId, NgayPhanCong = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return user.NguoiDungId;
        }

        private static Dictionary<string, string> IncidentForm(int phongTroId, string title) => new()
        {
            ["PhongTroId"] = phongTroId.ToString(),
            ["TieuDe"] = title,
            ["MoTa"] = "Vòi nước bị rò"
        };

        [Fact]
        public async Task NewIncident_NotifiesAdminsAndRoomBranchStaff_NotOtherBranchStaff()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var staffBId = await CreateStaffOfBranchBAsync(w);
            var tenantUser = await CreateTenantAccountAsync(w.TenantA);
            var title = "Su co " + Guid.NewGuid().ToString("N")[..8];

            var (tenant, token) = await BranchScopeKit.LoginAsync(_factory, tenantUser, "/KhachThue/SuCo");
            var res = await BranchScopeKit.PostFormAsync(tenant, token, "/KhachThue/SuCo/Create", IncidentForm(w.RoomA, title));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.True((await BranchScopeKit.JsonAsync(res)).GetProperty("success").GetBoolean());

            var (adminId, staffAId) = await BranchScopeKit.ReadAsync(_factory, async db => (
                await db.NguoiDungs.Where(u => u.TenDangNhap == w.Admin).Select(u => u.NguoiDungId).FirstAsync(),
                await db.NguoiDungs.Where(u => u.TenDangNhap == w.Staff).Select(u => u.NguoiDungId).FirstAsync()));
            var recipients = await BranchScopeKit.ReadAsync(_factory, db => db.ThongBaos
                .Where(t => t.TieuDe == "Sự cố mới" && t.NoiDung.Contains(title))
                .Select(t => t.NguoiDungId).ToListAsync());

            Assert.Contains((int?)adminId, recipients);
            Assert.Contains((int?)staffAId, recipients);
            Assert.DoesNotContain((int?)staffBId, recipients);
        }

        [Fact]
        public async Task NewIncident_ForRoomNotInTenantsContract_IsRejected_NoIncidentNoNotification()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var tenantUser = await CreateTenantAccountAsync(w.TenantA);
            var title = "Su co la " + Guid.NewGuid().ToString("N")[..8];

            var (tenant, token) = await BranchScopeKit.LoginAsync(_factory, tenantUser, "/KhachThue/SuCo");
            var res = await BranchScopeKit.PostFormAsync(tenant, token, "/KhachThue/SuCo/Create", IncidentForm(w.RoomB, title));

            var json = await BranchScopeKit.JsonAsync(res);
            Assert.False(json.GetProperty("success").GetBoolean());
            Assert.Equal("Phòng không thuộc hợp đồng đang hoạt động của bạn.", json.GetProperty("message").GetString());

            Assert.False(await BranchScopeKit.ReadAsync(_factory, db => db.YeuCauSuCos.AnyAsync(s => s.TieuDe == title)));
            Assert.False(await BranchScopeKit.ReadAsync(_factory, db => db.ThongBaos.AnyAsync(t => t.NoiDung.Contains(title))));
        }

        [Fact]
        public async Task NewIncident_ForRoomOfEndedContract_IsRejected_NoIncidentNoNotification()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var contract = await db.HopDongs.FirstAsync(h => h.HopDongId == w.ContractA);
                contract.TrangThaiHopDong = TrangThaiHopDong.DaKetThuc;
                await db.SaveChangesAsync();
            }
            var tenantUser = await CreateTenantAccountAsync(w.TenantA);
            var title = "Su co cu " + Guid.NewGuid().ToString("N")[..8];

            var (tenant, token) = await BranchScopeKit.LoginAsync(_factory, tenantUser, "/KhachThue/SuCo");
            var res = await BranchScopeKit.PostFormAsync(tenant, token, "/KhachThue/SuCo/Create", IncidentForm(w.RoomA, title));

            var json = await BranchScopeKit.JsonAsync(res);
            Assert.False(json.GetProperty("success").GetBoolean());
            Assert.Equal("Phòng không thuộc hợp đồng đang hoạt động của bạn.", json.GetProperty("message").GetString());

            Assert.False(await BranchScopeKit.ReadAsync(_factory, db => db.YeuCauSuCos.AnyAsync(s => s.TieuDe == title)));
            Assert.False(await BranchScopeKit.ReadAsync(_factory, db => db.ThongBaos.AnyAsync(t => t.NoiDung.Contains(title))));
        }

        [Fact]
        public async Task MarkAsRead_OnlyOwnNotification()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var adminId = await BranchScopeKit.ReadAsync(_factory, db => db.NguoiDungs.Where(u => u.TenDangNhap == w.Admin).Select(u => u.NguoiDungId).FirstAsync());
            int notificationId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var tb = new ThongBao { NguoiDungId = adminId, TieuDe = "Test", NoiDung = "Của admin", CreatedAt = DateTime.UtcNow };
                db.ThongBaos.Add(tb);
                await db.SaveChangesAsync();
                notificationId = tb.Id;
            }

            var (staff, staffToken) = await BranchScopeKit.LoginAsync(_factory, w.Staff);
            var foreign = await BranchScopeKit.SendJsonAsync(staff, staffToken, HttpMethod.Post, $"/api/ThongBao/DanhDauDaDoc/{notificationId}", null);
            var foreignJson = await BranchScopeKit.JsonAsync(foreign);
            Assert.False(foreignJson.GetProperty("success").GetBoolean());
            Assert.Equal("Không tìm thấy thông báo.", foreignJson.GetProperty("message").GetString());
            Assert.False(await BranchScopeKit.ReadAsync(_factory, db => db.ThongBaos.Where(t => t.Id == notificationId).Select(t => t.IsRead).FirstAsync()));

            var (admin, adminToken) = await BranchScopeKit.LoginAsync(_factory, w.Admin);
            var own = await BranchScopeKit.SendJsonAsync(admin, adminToken, HttpMethod.Post, $"/api/ThongBao/DanhDauDaDoc/{notificationId}", null);
            Assert.True((await BranchScopeKit.JsonAsync(own)).GetProperty("success").GetBoolean());
            Assert.True(await BranchScopeKit.ReadAsync(_factory, db => db.ThongBaos.Where(t => t.Id == notificationId).Select(t => t.IsRead).FirstAsync()));
        }
    }
}
