using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    [Collection(WebTestCollection.Name)]
    public class AdminScreenAuthorizationTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public AdminScreenAuthorizationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // (HttpMethod, url, ["form"|"json"] body kind cho POST)
        public static IEnumerable<object[]> AdminOnlyGetRoutes => new[]
        {
            new object[] { "/QuanLyNhaTro/QuanLyTaiKhoanDangNhap" },
            new object[] { "/QuanLyNhaTro/NguoiDung/GetAllApi" },
            new object[] { "/ChiNhanhs/QuanLyChiNhanh" },
            new object[] { "/ChiNhanhs/DanhSachChiNhanh" },
            new object[] { "/ChiNhanhs/GetChiNhanh?id=1" },
            new object[] { "/QuanLyNhaTro/PhanCongChiNhanh" },
            new object[] { "/QuanLyNhaTro/PhanCongChiNhanh/DanhSach" },
            new object[] { "/QuanLyNhaTro/PhanCongChiNhanh/ChiTiet/1" },
            new object[] { "/QuanLyNhaTro/PhanCongChiNhanh/ChiNhanhs" }
        };

        // Chỉ dùng cho test "staff bị chặn": bị chặn ở tầng Authorize trước khi vào action,
        // nên id không cần tồn tại. Không dùng lại cho test "admin gọi được" (route trả 404 khi id không tồn tại).
        public static IEnumerable<object[]> AdminOnlyGetRoutesWithPathId => new[]
        {
            new object[] { "/QuanLyNhaTro/NguoiDung/GetByIdApi/999999" }
        };

        public static IEnumerable<object[]> AdminOnlyPostRoutes => new[]
        {
            new object[] { "/QuanLyNhaTro/NguoiDung/CreateApi" },
            new object[] { "/QuanLyNhaTro/NguoiDung/EditApi/999999" },
            new object[] { "/QuanLyNhaTro/NguoiDung/DeleteApi/999999" },
            new object[] { "/QuanLyNhaTro/NguoiDung/ResetPasswordToPhoneApi/999999" },
            new object[] { "/ChiNhanhs/ThemChiNhanhMoi" },
            new object[] { "/ChiNhanhs/CapNhatChiNhanh" },
            new object[] { "/ChiNhanhs/XoaChiNhanh?id=999999" },
            new object[] { "/QuanLyNhaTro/PhanCongChiNhanh/PhanCong" },
            new object[] { "/QuanLyNhaTro/PhanCongChiNhanh/ThuHoi" }
        };

        [Theory]
        [MemberData(nameof(AdminOnlyGetRoutes))]
        [MemberData(nameof(AdminOnlyGetRoutesWithPathId))]
        public async Task Staff_GetAdminOnlyRoute_Returns302ToDangNhap(string url)
        {
            var (username, client) = await CreateLoggedInStaffClientAsync();
            try
            {
                var response = await client.GetAsync(url);

                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.NotNull(response.Headers.Location);
                Assert.Contains("/QuanLyNhaTro/DangNhap", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Theory]
        [MemberData(nameof(AdminOnlyPostRoutes))]
        public async Task Staff_PostAdminOnlyRoute_WithValidAntiforgery_Returns302ToDangNhap_NotForbidden(string url)
        {
            var (username, client, antiforgeryToken) = await CreateLoggedInStaffClientWithFreshAntiforgeryAsync();
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
                request.Headers.Add("RequestVerificationToken", antiforgeryToken);

                var response = await client.SendAsync(request);

                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.NotNull(response.Headers.Location);
                Assert.Contains("/QuanLyNhaTro/DangNhap", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Theory]
        [MemberData(nameof(AdminOnlyGetRoutes))]
        public async Task Admin_GetAdminOnlyRoute_Returns200(string url)
        {
            var (username, client) = await CreateLoggedInAdminClientAsync();
            try
            {
                var response = await client.GetAsync(url);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Fact]
        public async Task Admin_GetByIdApi_OwnAccount_Returns200()
        {
            var username = $"test_{AppRole.Admin}_{Guid.NewGuid():N}".Substring(0, 20);
            const string password = "TestPassword123!";

            using var scope = _factory.Services.CreateScope();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var createResult = await nguoiDungService.AddAsync(new QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = AppRole.Admin
            });
            Assert.True(createResult.Success);

            var createdUser = (await nguoiDungService.GetAllAsync()).First(u => u.TenDangNhap == username);

            string? loginAdminUsername = null;
            try
            {
                var (adminUsername, client) = await CreateLoggedInAdminClientAsync();
                loginAdminUsername = adminUsername;
                var response = await client.GetAsync($"/QuanLyNhaTro/NguoiDung/GetByIdApi/{createdUser.NguoiDungId}");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            finally
            {
                await CleanupUserAsync(username);
                if (loginAdminUsername != null)
                {
                    await CleanupUserAsync(loginAdminUsername);
                }
            }
        }

        [Theory]
        [InlineData("/QuanLyNhaTro/NguoiDung/DeleteApi/999999")]
        [InlineData("/QuanLyNhaTro/NguoiDung/ResetPasswordToPhoneApi/999999")]
        [InlineData("/ChiNhanhs/XoaChiNhanh?id=999999")]
        public async Task Admin_PostAdminOnlyRoute_NonExistentId_Returns200_NotForbidden(string url)
        {
            var (username, client, antiforgeryToken) = await CreateLoggedInAdminClientWithFreshAntiforgeryAsync();
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("RequestVerificationToken", antiforgeryToken);

                var response = await client.SendAsync(request);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Fact]
        public async Task Admin_PostPhanCong_NonExistentTarget_Returns200_NotForbidden()
        {
            var (username, client, antiforgeryToken) = await CreateLoggedInAdminClientWithFreshAntiforgeryAsync();
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/QuanLyNhaTro/PhanCongChiNhanh/PhanCong")
                {
                    Content = new StringContent("{\"nguoiDungId\":999999,\"chiNhanhId\":999999}", Encoding.UTF8, "application/json")
                };
                request.Headers.Add("RequestVerificationToken", antiforgeryToken);

                var response = await client.SendAsync(request);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Fact]
        public async Task DangNhap_Get_And_Post_AccessibleAnonymously()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var getResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var html = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(html);

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("TenDangNhap", "khong_ton_tai"),
                new KeyValuePair<string, string>("MatKhau", "sai_mat_khau")
            });

            var postResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", formContent);
            Assert.True(postResponse.StatusCode == HttpStatusCode.OK || postResponse.StatusCode == HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task PhanCong_Post_MissingAntiforgeryToken_Returns400()
        {
            var (username, client) = await CreateLoggedInAdminClientAsync();
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/QuanLyNhaTro/PhanCongChiNhanh/PhanCong")
                {
                    Content = new StringContent("{\"nguoiDungId\":999999,\"chiNhanhId\":999999}", Encoding.UTF8, "application/json")
                };
                // Không gửi header RequestVerificationToken.

                var response = await client.SendAsync(request);

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Fact]
        public async Task StaffDashboardHtml_DoesNotExpose_AdminOnlyLinks()
        {
            var (username, client) = await CreateLoggedInStaffClientAsync();
            try
            {
                var response = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var html = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("/QuanLyNhaTro/QuanLyTaiKhoanDangNhap", html, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("/ChiNhanhs/QuanLyChiNhanh", html, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("/QuanLyNhaTro/PhanCongChiNhanh", html, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        // ---------- Helpers ----------

        private async Task<(string username, HttpClient client)> CreateLoggedInStaffClientAsync()
        {
            var (username, client, _) = await CreateLoggedInClientAsync(AppRole.NhanVien);
            return (username, client);
        }

        private async Task<(string username, HttpClient client)> CreateLoggedInAdminClientAsync()
        {
            var (username, client, _) = await CreateLoggedInClientAsync(AppRole.Admin);
            return (username, client);
        }

        private async Task<(string username, HttpClient client, string antiforgeryToken)> CreateLoggedInStaffClientWithFreshAntiforgeryAsync()
        {
            return await CreateLoggedInClientAsync(AppRole.NhanVien, fetchFreshAntiforgery: true);
        }

        private async Task<(string username, HttpClient client, string antiforgeryToken)> CreateLoggedInAdminClientWithFreshAntiforgeryAsync()
        {
            return await CreateLoggedInClientAsync(AppRole.Admin, fetchFreshAntiforgery: true);
        }

        private async Task<(string username, HttpClient client, string antiforgeryToken)> CreateLoggedInClientAsync(AppRole role, bool fetchFreshAntiforgery = false)
        {
            var username = $"test_{role}_{Guid.NewGuid():N}".Substring(0, 20);
            const string password = "TestPassword123!";

            using var scope = _factory.Services.CreateScope();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var createResult = await nguoiDungService.AddAsync(new QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = role
            });
            if (!createResult.Success)
            {
                throw new InvalidOperationException($"Không tạo được người dùng test '{username}': {createResult.Message}");
            }

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
            var loginToken = ExtractAntiForgeryToken(loginHtml);

            var loginContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", loginToken),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            });

            var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);
            if (loginResponse.StatusCode != HttpStatusCode.Redirect)
            {
                throw new InvalidOperationException($"Đăng nhập thất bại cho '{username}': {loginResponse.StatusCode}");
            }

            var antiforgeryToken = string.Empty;
            if (fetchFreshAntiforgery)
            {
                var dashboardResponse = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                var dashboardHtml = await dashboardResponse.Content.ReadAsStringAsync();
                antiforgeryToken = ExtractAntiForgeryToken(dashboardHtml);
                if (string.IsNullOrWhiteSpace(antiforgeryToken))
                {
                    antiforgeryToken = loginToken;
                }
            }

            return (username, client, antiforgeryToken);
        }

        private async Task CleanupUserAsync(string username)
        {
            try
            {
                using var scope = _factory.Services.CreateScope();
                var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                var all = await nguoiDungService.GetAllAsync();
                var user = all.FirstOrDefault(u => u.TenDangNhap == username);
                if (user != null)
                {
                    await nguoiDungService.DeleteAsync(user.NguoiDungId);
                }
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        private static string ExtractAntiForgeryToken(string html)
        {
            var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!match.Success)
            {
                match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
            }

            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}
