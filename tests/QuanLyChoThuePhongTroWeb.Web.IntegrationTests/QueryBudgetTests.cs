using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;
using static QuanLyChoThuePhongTroWeb.Web.IntegrationTests.BranchScopeKit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Số lệnh SQL của các request chạy trên đường chính (mọi trang quản lý, trang hay mở, cổng khách thuê).
    // Số đo in ra output test để so trước/sau khi tối ưu; xem docs/features/toi-uu-hieu-nang/.
    [Collection(WebTestCollection.Name)]
    public class QueryBudgetTests : IDisposable
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly ITestOutputHelper _output;
        private readonly QueryCounter _counter = new();

        public QueryBudgetTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        {
            _factory = factory;
            _output = output;
        }

        public void Dispose() => _counter.Dispose();

        private async Task<int> MeasureGetAsync(HttpClient client, string url)
        {
            HttpResponseMessage? response = null;
            var count = await _counter.MeasureAsync(async () => response = await client.GetAsync(url));
            Assert.Equal(HttpStatusCode.OK, response!.StatusCode);
            _output.WriteLine($"GET {url}: {count} lệnh SQL");
            return count;
        }

        // Kỳ liền trước tháng hiện tại (giờ VN) luôn nằm trong khoảng quét của mục HD5.
        private static (int Thang, int Nam) KyTruoc()
        {
            var ky = DateTime.UtcNow.AddHours(7).AddMonths(-1);
            return (ky.Month, ky.Year);
        }

        // Thêm n phòng ở chi nhánh, mỗi phòng một kỳ đã chốt chưa có hóa đơn (ứng viên của mục HD5).
        private async Task AddHd5CandidatesAsync(int chiNhanhId, int n)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ky = KyTruoc();
            var s = Guid.NewGuid().ToString("N")[..5];
            var rooms = new List<PhongTro>();
            for (var i = 0; i < n; i++)
            {
                rooms.Add(new PhongTro
                {
                    ChiNhanhId = chiNhanhId, SoPhong = $"Q{s}{i:D2}", GiaThue = 2_000_000m, DienTich = 20, SoNguoiToiDa = 2,
                    MoTa = "Q", TrangThai = TrangThaiPhong.Trong
                });
            }
            db.PhongTros.AddRange(rooms);
            await db.SaveChangesAsync();

            foreach (var room in rooms)
            {
                db.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId, Thang = ky.Thang, Nam = ky.Nam,
                    ChiSoDienCu = 0, ChiSoDienMoi = 5_000_000m, DonGiaDien = 3500m,
                    ChiSoNuocCu = 0, ChiSoNuocMoi = 5m, DonGiaNuoc = 15000m,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
                });
            }
            await db.SaveChangesAsync();
        }

        private async Task<HttpClient> LoginTenantAsync(BranchScopeWorld w)
        {
            string tenantUser;
            using (var scope = _factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                tenantUser = "kq_" + Guid.NewGuid().ToString("N")[..8];
                Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = tenantUser, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = w.TenantA })).Success);
            }

            var (tenant, _) = await LoginAsync(_factory, tenantUser, "/KhachThue/HoaDon");
            return tenant;
        }

        [Fact]
        public async Task Baseline_ManagerPages_And_TenantPages()
        {
            var w = await SeedAsync(_factory);
            var (staff, token) = await LoginAsync(_factory, w.Staff);

            var tomTat0 = await MeasureGetAsync(staff, "/QuanLyNhaTro/ViecCanLam/TomTat");
            await AddHd5CandidatesAsync(w.BranchA, 5);
            var tomTat5 = await MeasureGetAsync(staff, "/QuanLyNhaTro/ViecCanLam/TomTat");
            await AddHd5CandidatesAsync(w.BranchA, 10);
            var tomTat15 = await MeasureGetAsync(staff, "/QuanLyNhaTro/ViecCanLam/TomTat");
            _output.WriteLine($"TomTat: 0 ứng viên HD5 = {tomTat0}, 5 ứng viên = {tomTat5}, 15 ứng viên = {tomTat15}");
            // Huy hiệu chạy trên mọi trang quản lý: số lệnh SQL không được tăng theo số ứng viên HD5.
            Assert.Equal(tomTat5, tomTat15);

            await MeasureGetAsync(staff, "/QuanLyNhaTro/Dashboard");
            await MeasureGetAsync(staff, "/QuanLyNhaTro/ViecCanLam");
            await MeasureGetAsync(staff, "/QuanLyNhaTro/QuanLyHoaDon");
            await MeasureGetAsync(staff, "/QuanLyNhaTro/QuanLyHopDong");
            await MeasureGetAsync(staff, "/api/ThongBao/LayMoiNhat");

            HttpResponseMessage? list = null;
            var listCount = await _counter.MeasureAsync(async () => list = await PostFormAsync(staff, token, "/HoaDon/GetList",
                new Dictionary<string, string> { ["draw"] = "1", ["start"] = "0", ["length"] = "10" }));
            Assert.Equal(HttpStatusCode.OK, list!.StatusCode);
            _output.WriteLine($"POST /HoaDon/GetList: {listCount} lệnh SQL");

            var tenant = await LoginTenantAsync(w);
            await MeasureGetAsync(tenant, "/KhachThue/Dashboard");
            await MeasureGetAsync(tenant, "/KhachThue/HoaDon");

            Assert.True(tomTat5 >= tomTat0);
        }
    }
}
