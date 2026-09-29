using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    /// <summary>
    /// Vòng 3 (b') (theo .bangiao/ke-hoach.md, mục "BỔ SUNG VÒNG 3", thay thế Vòng 2 (a)): kiểm
    /// chứng Q2 sau khi coder vá lại `Program.cs` — `OnValidatePrincipal` bọc lời gọi
    /// `IsSessionValidAsync` bằng `try/catch`; khi lỗi, `RejectPrincipal()` + cất
    /// `ExceptionDispatchInfo` vào `HttpContext.Items[Program.SessionCheckExceptionKey]`, không
    /// `SignOutAsync`, không log. Một middleware inline mới giữa `UseAuthentication` và
    /// `UseAuthorization` lấy exception đã cất ra; nếu KHÔNG đang ở lượt phát lại của
    /// `UseExceptionHandler` (`IExceptionHandlerFeature == null`) thì `edi.Throw()` — nhờ đó
    /// exception nổi lên ở một middleware THƯỜNG (không phải trong `AuthenticationHandler`), nên
    /// khi `UseExceptionHandler("/Home/Error")` phát lại request, kết quả xác thực đã cache trên
    /// `HttpContext` là "ẩn danh" (không phải lỗi) — lượt phát lại không throw lại, trang lỗi
    /// render được, response cuối cùng là 500 kèm trang lỗi đầy đủ.
    ///
    /// Dùng một `CustomWebApplicationFactory` ĐỘC LẬP (không phải factory dùng chung của
    /// `WebTestCollection`) + `WithWebHostBuilder` để thay `IUserSessionService` bằng fake ném
    /// exception, tránh ảnh hưởng `LogSink`/kiểm tra log của các test khác trong collection.
    /// </summary>
    [Collection(WebTestCollection.Name)]
    public class SessionDbErrorTests
    {
        private const string SimulatedFailureMessage = "[T2-simulated] Mat ket noi CSDL khi kiem tra lai phien.";
        private const string AuthCookieName = ".AspNetCore.Cookies"; // CookieAuthenticationDefaults, không đặt Cookie.Name riêng trong Program.cs

        private readonly CustomWebApplicationFactory _sharedFactory;

        public SessionDbErrorTests(CustomWebApplicationFactory sharedFactory)
        {
            _sharedFactory = sharedFactory;
        }

        /// <summary>
        /// Fake có thể CHUYỂN CHẾ ĐỘ qua field tĩnh <see cref="ShouldThrow"/>: lần đầu ném lỗi mô
        /// phỏng, sau đó (khi test đặt <c>ShouldThrow = false</c>) trả <c>true</c> như một tài
        /// khoản vẫn còn hợp lệ. Chọn cách này (một fake đổi chế độ, dùng lại đúng MỘT
        /// factory/client/cookie cho cả hai lượt gọi) thay vì dựng hai <c>CustomWebApplicationFactory</c>
        /// riêng: hai host riêng sẽ có vòng khóa Data Protection riêng, cookie xác thực ký ở host A
        /// (dùng để mã hóa/giải mã ticket) không chắc giải mã được ở host B — nghĩa là "cùng cookie"
        /// mà đề bài yêu cầu chỉ thật sự có ý nghĩa khi cả hai request đi qua CÙNG một host/factory.
        /// </summary>
        private class ToggleableUserSessionService : IUserSessionService
        {
            public static int CallCount;
            public static bool ShouldThrow;

            public Task<bool> IsSessionValidAsync(int nguoiDungId, AppRole role, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref CallCount);
                if (ShouldThrow)
                {
                    throw new InvalidOperationException(SimulatedFailureMessage);
                }

                // Tài khoản vẫn còn hợp lệ (NhanVien, IsActive=true, role không đổi) trong kịch bản
                // test này — mô phỏng đúng việc IUserSessionService hoạt động lại bình thường.
                return Task.FromResult(true);
            }
        }

        [Fact]
        public async Task SessionCheckThrows_Returns500ErrorPage_AndKeepsAuthCookie()
        {
            var username = $"dberr_{Guid.NewGuid():N}".Substring(0, 20);
            const string password = "DbErrorTest123!";
            ToggleableUserSessionService.CallCount = 0;
            ToggleableUserSessionService.ShouldThrow = true;

            // Seed qua factory dùng chung (chỉ ghi DB, không sinh log lỗi) — an toàn cho các test khác.
            using (var seedScope = _sharedFactory.Services.CreateScope())
            {
                var nguoiDungService = seedScope.ServiceProvider.GetRequiredService<INguoiDungService>();
                var createResult = await nguoiDungService.AddAsync(new QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.CreateNguoiDungReq
                {
                    TenDangNhap = username,
                    MatKhau = password,
                    Role = AppRole.NhanVien
                });
                Assert.True(createResult.Success);
            }

            var standaloneFactory = new CustomWebApplicationFactory();
            try
            {
                var overriddenFactory = standaloneFactory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IUserSessionService>();
                        services.AddScoped<IUserSessionService, ToggleableUserSessionService>();
                    });
                });

                var client = overriddenFactory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });

                var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
                var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
                var token = ExtractAntiForgeryToken(loginHtml);

                var loginContent = new System.Net.Http.FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("__RequestVerificationToken", token),
                    new System.Collections.Generic.KeyValuePair<string, string>("TenDangNhap", username),
                    new System.Collections.Generic.KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);
                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
                Assert.True(
                    loginResponse.Headers.TryGetValues("Set-Cookie", out var loginCookies) &&
                    loginCookies.Any(c => c.Contains(AuthCookieName)),
                    "Đăng nhập phải tạo cookie xác thực trước khi kiểm tra hành vi khi IUserSessionService ném lỗi.");

                // Request kế tiếp: OnValidatePrincipal gọi IUserSessionService.IsSessionValidAsync,
                // fake ném InvalidOperationException. Theo bản vá Vòng 3 (a'): exception được bắt
                // ngay trong OnValidatePrincipal (RejectPrincipal + cất ExceptionDispatchInfo vào
                // HttpContext.Items, không SignOutAsync, không log), rồi được ném lại ở middleware
                // inline (giữa UseAuthentication và UseAuthorization) — một vị trí middleware
                // THƯỜNG, không phải trong AuthenticationHandler. Nhờ vậy khi UseExceptionHandler
                // phát lại request tới "/Home/Error" trên CÙNG HttpContext, kết quả xác thực đã
                // cache là "ẩn danh" (không phải Task lỗi) nên lượt phát lại không throw lại — trang
                // lỗi render được, response cuối cùng là 500 kèm trang lỗi đầy đủ.
                var response = await client.GetAsync("/QuanLyNhaTro/Dashboard");

                // 1) Fake chỉ bị GỌI đúng 1 lần.
                Assert.Equal(1, ToggleableUserSessionService.CallCount);

                // 2) StatusCode == 500 và body là trang lỗi thật (chuỗi lấy từ
                //    src/QuanLyChoThuePhongTroWeb.Web/Views/Shared/Error.cshtml).
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
                var body = await response.Content.ReadAsStringAsync();
                Assert.Contains("An error occurred", body, StringComparison.OrdinalIgnoreCase);

                // 3) Không được đăng xuất: catch trong OnValidatePrincipal chỉ RejectPrincipal, không
                //    SignOutAsync, nên không có Set-Cookie xóa cookie xác thực tên thật .AspNetCore.Cookies.
                var hasSignOutCookie = response.Headers.TryGetValues("Set-Cookie", out var responseCookies) &&
                    responseCookies.Any(c => c.Contains(AuthCookieName) &&
                        (c.Contains("expires=Thu, 01 Jan 1970") || c.Contains($"{AuthCookieName}=;")));
                Assert.False(hasSignOutCookie, "Lỗi DB khi kiểm tra phiên không được phép đăng xuất người dùng (Q2, lựa chọn 2).");

                // 4) Log sink có ĐÚNG 1 log Error chứa thông điệp mô phỏng (không phải 2 như Vòng 2):
                //    dùng cơ chế log sink có sẵn của CustomWebApplicationFactory (LogSink.Entries,
                //    public, không sửa file Fixtures) để đếm chính xác, không suy đoán qua số lần
                //    xuất hiện chuỗi con trong một message ghép.
                var matchingErrorLogs = standaloneFactory.LogSink.Entries
                    .Where(e => e.Level >= LogLevel.Error &&
                                (e.Message.Contains(SimulatedFailureMessage, StringComparison.Ordinal) ||
                                 (e.Exception?.Message?.Contains(SimulatedFailureMessage, StringComparison.Ordinal) ?? false)))
                    .ToList();
                Assert.Single(matchingErrorLogs);

                // (Yêu cầu "gọi lại bằng cùng cookie khi IUserSessionService hoạt động bình thường
                // thì phải nhận 200" được kiểm chứng bằng test riêng
                // SessionCheckThrows_ThenRecoversWithSameCookie_UserStaysLoggedIn ngay dưới đây,
                // theo đúng chỉ đạo "Thêm 1 test" của Vòng 3 (b') — không lặp lại trong test này.)
            }
            finally
            {
                using (var cleanupScope = _sharedFactory.Services.CreateScope())
                {
                    try
                    {
                        var db = cleanupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var user = await db.NguoiDungs.FirstOrDefaultAsync(u => u.TenDangNhap == username);
                        if (user != null)
                        {
                            db.NguoiDungs.Remove(user);
                            await db.SaveChangesAsync();
                        }
                    }
                    catch
                    {
                        // Best-effort cleanup
                    }
                }

                // Factory độc lập này đã ghi lại đúng 1 log Error mô phỏng (đã assert ở bước 4).
                // AssertSuiteLogIntegrity() (không predicate) khi Dispose sẽ throw vì log đó — đây là
                // hành vi có chủ đích của UserSessionService (không try/catch, không tự log), nên
                // bắt đúng exception đó tại đây để dọn dẹp; không phải factory dùng chung của
                // WebTestCollection nên không ảnh hưởng test khác.
                try
                {
                    standaloneFactory.Dispose();
                }
                catch (InvalidOperationException)
                {
                    // Kỳ vọng: AssertSuiteLogIntegrity() phát hiện đúng 1 log Error mô phỏng.
                }
            }
        }

        [Fact]
        public async Task SessionCheckThrows_ThenRecoversWithSameCookie_UserStaysLoggedIn()
        {
            // Test riêng theo yêu cầu Vòng 3 (b'): sau lỗi DB khi kiểm tra phiên, gọi lại bằng CÙNG
            // cookie khi IUserSessionService hoạt động bình thường (tài khoản vẫn hợp lệ) phải nhận
            // 200 — chứng minh người dùng không bị đăng xuất bởi lỗi ở request trước. Tách thành test
            // riêng (ngoài các assertion đã có trong SessionCheckThrows_Returns500ErrorPage_...) để
            // tên test tự mô tả rõ mục tiêu kiểm chứng theo đúng yêu cầu.
            var username = $"dbrecov_{Guid.NewGuid():N}".Substring(0, 20);
            const string password = "DbRecoverTest123!";
            ToggleableUserSessionService.CallCount = 0;
            ToggleableUserSessionService.ShouldThrow = true;

            using (var seedScope = _sharedFactory.Services.CreateScope())
            {
                var nguoiDungService = seedScope.ServiceProvider.GetRequiredService<INguoiDungService>();
                var createResult = await nguoiDungService.AddAsync(new QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.CreateNguoiDungReq
                {
                    TenDangNhap = username,
                    MatKhau = password,
                    Role = AppRole.NhanVien
                });
                Assert.True(createResult.Success);
            }

            var standaloneFactory = new CustomWebApplicationFactory();
            try
            {
                var overriddenFactory = standaloneFactory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IUserSessionService>();
                        services.AddScoped<IUserSessionService, ToggleableUserSessionService>();
                    });
                });

                var client = overriddenFactory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });

                var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
                var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
                var token = ExtractAntiForgeryToken(loginHtml);

                var loginContent = new System.Net.Http.FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("__RequestVerificationToken", token),
                    new System.Collections.Generic.KeyValuePair<string, string>("TenDangNhap", username),
                    new System.Collections.Generic.KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);
                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

                // Lượt 1: IUserSessionService ném lỗi -> 500 (đã kiểm chứng đầy đủ chi tiết ở test
                // SessionCheckThrows_Returns500ErrorPage_AndKeepsAuthCookie; ở đây chỉ cần xác nhận
                // lỗi THẬT SỰ đã xảy ra trước khi kiểm tra khả năng "hồi phục" của cookie).
                var firstResponse = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.InternalServerError, firstResponse.StatusCode);
                Assert.Equal(1, ToggleableUserSessionService.CallCount);

                // Lượt 2: IUserSessionService hoạt động lại bình thường (tài khoản vẫn hợp lệ), dùng
                // ĐÚNG cookie của lượt 1 (cùng client, chưa đăng nhập lại) -> phải nhận 200.
                ToggleableUserSessionService.ShouldThrow = false;
                var secondResponse = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

                // Task 0.5 (kế hoạch khắc phục review đợt 2B lần 2): xác nhận lượt 1 (lỗi DB khi
                // kiểm tra phiên) chỉ sinh ĐÚNG 1 log Error mô phỏng trên factory độc lập này —
                // giống hệt cách lọc ở SessionCheckThrows_Returns500ErrorPage_AndKeepsAuthCookie
                // (dòng 165–170), dùng LogSink.Entries có sẵn của CustomWebApplicationFactory.
                var matchingErrorLogs = standaloneFactory.LogSink.Entries
                    .Where(e => e.Level >= LogLevel.Error &&
                                (e.Message.Contains(SimulatedFailureMessage, StringComparison.Ordinal) ||
                                 (e.Exception?.Message?.Contains(SimulatedFailureMessage, StringComparison.Ordinal) ?? false)))
                    .ToList();
                Assert.Single(matchingErrorLogs);
            }
            finally
            {
                using (var cleanupScope = _sharedFactory.Services.CreateScope())
                {
                    try
                    {
                        var db = cleanupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var user = await db.NguoiDungs.FirstOrDefaultAsync(u => u.TenDangNhap == username);
                        if (user != null)
                        {
                            db.NguoiDungs.Remove(user);
                            await db.SaveChangesAsync();
                        }
                    }
                    catch
                    {
                        // Best-effort cleanup
                    }
                }

                try
                {
                    standaloneFactory.Dispose();
                }
                catch (InvalidOperationException)
                {
                    // Kỳ vọng: AssertSuiteLogIntegrity() phát hiện đúng 1 log Error mô phỏng (từ lượt 1).
                }
            }
        }

        [Fact]
        public async Task DirectHomeError_WithLockedAccountCookie_IsTreatedAsSignedOut()
        {
            // Request này KHÔNG đi qua UseExceptionHandler (không có IExceptionHandlerFeature),
            // nên nhánh mới ở Vòng 2 (a) không kích hoạt — OnValidatePrincipal phải chạy
            // IsSessionValidAsync như bình thường. Test này chứng minh "/Home/Error" không phải
            // một đường tắt để bỏ qua kiểm tra phiên: gọi thẳng nó bằng cookie của tài khoản đã bị
            // khóa vẫn phải bị coi là chưa đăng nhập (SignOutAsync chạy, cookie bị xóa).
            var username = $"lockerr_{Guid.NewGuid():N}".Substring(0, 20);
            const string password = "LockedErrTest123!";

            using (var seedScope = _sharedFactory.Services.CreateScope())
            {
                var nguoiDungService = seedScope.ServiceProvider.GetRequiredService<INguoiDungService>();
                var createResult = await nguoiDungService.AddAsync(new QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.CreateNguoiDungReq
                {
                    TenDangNhap = username,
                    MatKhau = password,
                    Role = AppRole.NhanVien
                });
                Assert.True(createResult.Success);
            }

            try
            {
                var client = _sharedFactory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });

                var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
                var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
                var token = ExtractAntiForgeryToken(loginHtml);

                var loginContent = new System.Net.Http.FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("__RequestVerificationToken", token),
                    new System.Collections.Generic.KeyValuePair<string, string>("TenDangNhap", username),
                    new System.Collections.Generic.KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);
                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

                // Xác nhận phiên còn hợp lệ trước khi khóa: /Home/Error vẫn 200 (trang cho phép ẩn danh).
                var beforeLockResponse = await client.GetAsync("/Home/Error");
                Assert.Equal(HttpStatusCode.OK, beforeLockResponse.StatusCode);
                Assert.False(
                    beforeLockResponse.Headers.TryGetValues("Set-Cookie", out var beforeLockCookies) &&
                    beforeLockCookies.Any(c => c.Contains(AuthCookieName) && c.Contains("expires=Thu, 01 Jan 1970")),
                    "Phiên còn hợp lệ thì /Home/Error không được đăng xuất người dùng.");

                using (var lockScope = _sharedFactory.Services.CreateScope())
                {
                    var db = lockScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var user = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);
                    user.IsActive = false;
                    await db.SaveChangesAsync();
                }

                // Gọi thẳng /Home/Error (không qua exception handler) bằng cookie cũ của tài khoản
                // vừa bị khóa: OnValidatePrincipal phải chạy IsSessionValidAsync như bình thường,
                // phát hiện IsActive=false -> RejectPrincipal + SignOutAsync -> cookie bị xóa.
                var afterLockResponse = await client.GetAsync("/Home/Error");

                Assert.Equal(HttpStatusCode.OK, afterLockResponse.StatusCode); // /Home/Error cho phép ẩn danh, không redirect
                var hasSignOutCookie = afterLockResponse.Headers.TryGetValues("Set-Cookie", out var afterLockCookies) &&
                    afterLockCookies.Any(c => c.Contains(AuthCookieName) &&
                        (c.Contains("expires=Thu, 01 Jan 1970") || c.Contains($"{AuthCookieName}=;")));
                Assert.True(hasSignOutCookie,
                    "Gọi thẳng /Home/Error bằng cookie của tài khoản đã bị khóa phải bị coi là chưa đăng nhập " +
                    "(SignOutAsync phải chạy, cookie phải bị xóa) — không có đường bỏ qua kiểm tra phiên.");

                // Bằng chứng bổ sung: cookie đã bị vô hiệu, request kế tiếp tới trang cần đăng nhập
                // phải bị coi là chưa đăng nhập (302 về DangNhap), không còn được xem là NhanVien.
                var afterSignOutDashboard = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.Redirect, afterSignOutDashboard.StatusCode);
                Assert.Contains("/QuanLyNhaTro/DangNhap", afterSignOutDashboard.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                using var cleanupScope = _sharedFactory.Services.CreateScope();
                try
                {
                    var db = cleanupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var user = await db.NguoiDungs.FirstOrDefaultAsync(u => u.TenDangNhap == username);
                    if (user != null)
                    {
                        db.NguoiDungs.Remove(user);
                        await db.SaveChangesAsync();
                    }
                }
                catch
                {
                    // Best-effort cleanup
                }
            }
        }

        private static string ExtractAntiForgeryToken(string html)
        {
            var match = System.Text.RegularExpressions.Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!match.Success)
            {
                match = System.Text.RegularExpressions.Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
            }

            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}
