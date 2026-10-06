using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Mô hình AI luôn là fake: test không bao giờ gọi Gemini thật.
    [Collection(WebTestCollection.Name)]
    public class AiAssistantEndpointTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public AiAssistantEndpointTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _factory.FakeAiChatModelInstance.Reset();
        }

        private static readonly string[] ManagerTools =
        {
            "GetPhongTroChuaChotDienNuocAsync", "GetHoaDonChuaThanhToanAsync", "GetDoanhThuThucThuAsync", "GetPhongTrongAsync",
            "GetHopDongSapHetHanAsync", "GetThongTinKhachThueAsync", "GetCongNoPhongAsync", "GetDoanhThuChiNhanhAsync", "GetChiSoDienNuocAsync"
        };

        private static readonly string[] TenantTools = { "GetMyHopDongInfoAsync", "GetMyChiSoDienNuocAsync", "GetMyHoaDonChuaThanhToanAsync" };

        private async Task<string> CreateTenantAccountAsync(int nguoiThueId)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var name = "kh_" + Guid.NewGuid().ToString("N")[..8];
            var result = await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = name, MatKhau = BranchScopeKit.Password, Role = AppRole.KhachThue, NguoiThueId = nguoiThueId });
            Assert.True(result.Success);
            return name;
        }

        private static string FunctionResponses(AiModelRequest request)
            => string.Join("\n", request.Turns.SelectMany(t => t.Parts).OfType<AiFunctionResponsePart>().Select(p => p.ResponseJson));

        [Fact]
        public async Task Staff_AsksVacantRooms_SeesOnlyOwnBranch_AndGetsManagerTools()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            string roomVacantA = "VA" + Guid.NewGuid().ToString("N")[..6];
            string roomVacantB = "VB" + Guid.NewGuid().ToString("N")[..6];
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.PhongTros.Add(new PhongTro { ChiNhanhId = w.BranchA, SoPhong = roomVacantA, GiaThue = 1m, DienTich = 10, MoTa = "x", TrangThai = TrangThaiPhong.Trong });
                db.PhongTros.Add(new PhongTro { ChiNhanhId = w.BranchB, SoPhong = roomVacantB, GiaThue = 1m, DienTich = 10, MoTa = "x", TrangThai = TrangThaiPhong.Trong });
                await db.SaveChangesAsync();
            }

            var fake = _factory.FakeAiChatModelInstance;
            fake.Responder = (_, n) => n == 1
                ? AiModelResult.Ok(null, new[] { new AiFunctionCall("GetPhongTrongAsync", "{}", "c1") },
                    new AiTurn(AiTurnRole.Model, new AiPart[] { new AiFunctionCallPart(new AiFunctionCall("GetPhongTrongAsync", "{}", "c1")) }, "{\"role\":\"model\",\"parts\":[]}"))
                : AiModelResult.Ok("Có phòng trống");

            var (client, token) = await BranchScopeKit.LoginAsync(_factory, w.Staff);
            var res = await BranchScopeKit.SendJsonAsync(client, token, HttpMethod.Post, "/QuanLyNhaTro/AiAssistant/Chat", new { message = "Phòng nào còn trống?", history = Array.Empty<object>() });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var json = await BranchScopeKit.JsonAsync(res);
            Assert.True(json.GetProperty("success").GetBoolean());
            Assert.Equal("Có phòng trống", json.GetProperty("message").GetString());

            var requests = fake.Requests;
            Assert.Equal(2, requests.Count);
            Assert.Equal(ManagerTools.OrderBy(x => x), requests[0].Tools.Select(t => t.Name).OrderBy(x => x));
            var responses = FunctionResponses(requests[1]);
            Assert.Contains(roomVacantA, responses);
            Assert.DoesNotContain(roomVacantB, responses);
        }

        [Fact]
        public async Task Tenant_AsksUnpaidInvoices_SeesOnlyOwnInvoices_AndGetsTenantTools()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var s = Guid.NewGuid().ToString("N")[..8];
            string codeA = $"AIHA_{s}", codeB = $"AIHB_{s}";
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.HoaDons.Add(new HoaDon { MaHoaDon = codeA, HopDongId = w.ContractA, Thang = 10, Nam = 2026, TongTien = 1_000_000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan });
                db.HoaDons.Add(new HoaDon { MaHoaDon = codeB, HopDongId = w.ContractB, Thang = 10, Nam = 2026, TongTien = 2_000_000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan });
                await db.SaveChangesAsync();
            }
            var tenantUser = await CreateTenantAccountAsync(w.TenantA);

            var fake = _factory.FakeAiChatModelInstance;
            fake.Responder = (_, n) => n == 1
                ? AiModelResult.Ok(null, new[] { new AiFunctionCall("GetMyHoaDonChuaThanhToanAsync", "{\"nguoiThueId\":999}", null) },
                    new AiTurn(AiTurnRole.Model, new AiPart[] { new AiFunctionCallPart(new AiFunctionCall("GetMyHoaDonChuaThanhToanAsync", "{\"nguoiThueId\":999}", null)) }))
                : AiModelResult.Ok("Bạn còn nợ 1 hóa đơn");

            var (client, token) = await BranchScopeKit.LoginAsync(_factory, tenantUser, "/KhachThue/HoaDon");
            var res = await BranchScopeKit.SendJsonAsync(client, token, HttpMethod.Post, "/KhachThue/AiAssistant/Chat", new { message = "Tôi còn nợ hóa đơn nào?", history = Array.Empty<object>() });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.True((await BranchScopeKit.JsonAsync(res)).GetProperty("success").GetBoolean());

            var requests = fake.Requests;
            Assert.Equal(TenantTools.OrderBy(x => x), requests[0].Tools.Select(t => t.Name).OrderBy(x => x));
            var responses = FunctionResponses(requests[1]);
            Assert.Contains(codeA, responses);
            Assert.DoesNotContain(codeB, responses);
        }

        [Fact]
        public async Task Tenant_CannotCallManagerEndpoint_AndStaffCannotCallTenantEndpoint()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var tenantUser = await CreateTenantAccountAsync(w.TenantA);
            var fake = _factory.FakeAiChatModelInstance;
            var body = new { message = "xin chào", history = Array.Empty<object>() };

            var (tenant, tenantToken) = await BranchScopeKit.LoginAsync(_factory, tenantUser, "/KhachThue/HoaDon");
            var tenantToManager = await BranchScopeKit.SendJsonAsync(tenant, tenantToken, HttpMethod.Post, "/QuanLyNhaTro/AiAssistant/Chat", body);
            Assert.Equal(HttpStatusCode.Redirect, tenantToManager.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", tenantToManager.Headers.Location!.ToString());

            var (staff, staffToken) = await BranchScopeKit.LoginAsync(_factory, w.Staff);
            var staffToTenant = await BranchScopeKit.SendJsonAsync(staff, staffToken, HttpMethod.Post, "/KhachThue/AiAssistant/Chat", body);
            Assert.Equal(HttpStatusCode.Redirect, staffToTenant.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", staffToTenant.Headers.Location!.ToString());

            Assert.Empty(fake.Requests);
        }

        [Fact]
        public async Task Anonymous_CannotCallEitherEndpoint()
        {
            var client = BranchScopeKit.NewClient(_factory);
            var content = new StringContent("{\"message\":\"hi\"}", System.Text.Encoding.UTF8, "application/json");

            var manager = await client.PostAsync("/QuanLyNhaTro/AiAssistant/Chat", content);
            var tenant = await client.PostAsync("/KhachThue/AiAssistant/Chat", new StringContent("{\"message\":\"hi\"}", System.Text.Encoding.UTF8, "application/json"));

            Assert.NotEqual(HttpStatusCode.OK, manager.StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, tenant.StatusCode);
            Assert.Empty(_factory.FakeAiChatModelInstance.Requests);
        }

        [Fact]
        public async Task Widget_RendersPerPortal_WithOwnEndpointAndStorageKey_AndLoginPageClearsHistory()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var tenantUser = await CreateTenantAccountAsync(w.TenantA);
            int staffId = await BranchScopeKit.ReadAsync(_factory, db => db.NguoiDungs.Where(u => u.TenDangNhap == w.Staff).Select(u => u.NguoiDungId).FirstAsync());

            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff);
            var staffHtml = await (await staff.GetAsync("/PhongTros/QuanLyPhongTro")).Content.ReadAsStringAsync();
            Assert.Contains("/QuanLyNhaTro/AiAssistant/Chat", staffHtml);
            Assert.Contains($"ai_chat_history_{staffId}", staffHtml);
            Assert.DoesNotContain("/KhachThue/AiAssistant/Chat", staffHtml);
            Assert.Contains("btn-ai-suggest", staffHtml);

            var (tenant, _) = await BranchScopeKit.LoginAsync(_factory, tenantUser, "/KhachThue/HoaDon");
            var tenantHtml = await (await tenant.GetAsync("/KhachThue/HoaDon")).Content.ReadAsStringAsync();
            Assert.Contains("/KhachThue/AiAssistant/Chat", tenantHtml);
            Assert.DoesNotContain("/QuanLyNhaTro/AiAssistant/Chat", tenantHtml);

            var anonymous = BranchScopeKit.NewClient(_factory);
            var loginHtml = await (await anonymous.GetAsync("/QuanLyNhaTro/DangNhap")).Content.ReadAsStringAsync();
            Assert.Contains("ai_chat_history", loginHtml);
            Assert.DoesNotContain("AiAssistant/Chat", loginHtml);
        }
    }
}
