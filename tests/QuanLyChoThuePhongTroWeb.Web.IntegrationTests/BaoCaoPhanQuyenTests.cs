using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Bổ sung do Tester: phân quyền chi nhánh của Báo cáo ở cả DuLieu và XuatExcel, mọi tab.
    [Collection(WebTestCollection.Name)]
    public class BaoCaoPhanQuyenTests
    {
        private const string Page = "/QuanLyNhaTro/BaoCao";
        private static readonly string[] Tabs = { "doanh-thu", "cong-no", "cong-suat", "hop-dong", "dien-nuoc" };

        private readonly CustomWebApplicationFactory _factory;

        public BaoCaoPhanQuyenTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Nhân viên không có dòng NhanVienChiNhanh nào.
        private async Task<string> SeedUnassignedStaffAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var name = $"nv0_{Guid.NewGuid():N}"[..14];
            var created = await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = name, MatKhau = BranchScopeKit.Password, Role = AppRole.NhanVien });
            Assert.True(created.Success);
            return name;
        }

        [Fact]
        public async Task NhanVienChonChiNhanhKhongDuocGiao_Nhan403ChuKhongRedirect_MoiTab_CaDuLieuLanXuatExcel()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            foreach (var tab in Tabs)
            {
                var data = await client.GetAsync($"{Page}/DuLieu?tab={tab}&chiNhanhId={world.BranchB}");
                Assert.Equal(HttpStatusCode.Forbidden, data.StatusCode);
                Assert.Null(data.Headers.Location);

                var excel = await client.GetAsync($"{Page}/XuatExcel?tab={tab}&chiNhanhId={world.BranchB}");
                Assert.Equal(HttpStatusCode.Forbidden, excel.StatusCode);
                Assert.Null(excel.Headers.Location);
            }
        }

        [Fact]
        public async Task NhanVien_ChiNhanhDuocGiaoVaKhongChon_Nhan200_MoiTab_CaDuLieuLanXuatExcel()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            foreach (var tab in Tabs)
            {
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Page}/DuLieu?tab={tab}")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Page}/DuLieu?tab={tab}&chiNhanhId={world.BranchA}")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Page}/XuatExcel?tab={tab}")).StatusCode);
            }
        }

        [Fact]
        public async Task NhanVienChuaGiaoChiNhanh_BiChan403_DuLieuVaXuatExcel_VaTrangHienCanhBao()
        {
            await BranchScopeKit.SeedAsync(_factory);
            var staff = await SeedUnassignedStaffAsync();
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, staff, Page);

            foreach (var tab in Tabs)
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Page}/DuLieu?tab={tab}")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Page}/XuatExcel?tab={tab}")).StatusCode);
            }

            var html = WebUtility.HtmlDecode(await (await client.GetAsync(Page)).Content.ReadAsStringAsync());
            Assert.Contains("Bạn chưa được phân công chi nhánh nào.", html);
        }

        [Fact]
        public async Task KhoangHon24Ky_Tra400_DuLieuVaXuatExcel_ChoCaAdminLanNhanVien()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (admin, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);
            const string q = "tuThang=1&tuNam=2024&denThang=1&denNam=2026";

            foreach (var client in new[] { admin, staff })
            {
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Page}/DuLieu?tab=doanh-thu&{q}")).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Page}/XuatExcel?tab=doanh-thu&{q}")).StatusCode);
                // 24 kỳ vẫn hợp lệ.
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Page}/DuLieu?tab=doanh-thu&tuThang=1&tuNam=2024&denThang=12&denNam=2025")).StatusCode);
            }
        }

        [Fact]
        public async Task KyKhongHopLeVaTabLa_Tra400()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);

            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Page}/DuLieu?tab=doanh-thu&tuThang=13&tuNam=2026&denThang=1&denNam=2026")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Page}/DuLieu?tab=doanh-thu&tuThang=5&tuNam=2026&denThang=4&denNam=2026")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Page}/DuLieu?tab=DOANH_THU")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Page}/XuatExcel?tab=")).StatusCode);
        }

        [Fact]
        public async Task TabKhongPhanBietHoaThuong_Tra200()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Page}/DuLieu?tab=Doanh-Thu&chiNhanhId={world.BranchA}")).StatusCode);
        }

        [Fact]
        public async Task Admin_ChiNhanhKhongTonTai_Tra404_VaChiNhanhAmBiBoQuaThanhTatCa()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Page}/DuLieu?tab=doanh-thu&chiNhanhId=2147483000")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Page}/DuLieu?tab=doanh-thu&chiNhanhId=0")).StatusCode);
        }

        [Fact]
        public async Task ChuaDangNhap_XuatExcelCungBiChuyenVeDangNhap()
        {
            var client = BranchScopeKit.NewClient(_factory);

            var res = await client.GetAsync($"{Page}/XuatExcel?tab=doanh-thu");

            Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
        }
    }
}
