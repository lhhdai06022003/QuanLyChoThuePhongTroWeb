using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
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
    public class MeterReadingSubmissionTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public MeterReadingSubmissionTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(HttpClient client, string antiForgeryToken)> CreateClientWithAntiforgeryAsync()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var getResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var html = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(html);
            return (client, token);
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

        [Fact]
        public async Task SaveChotDienNuoc_ManualMode_WithoutReason_ReturnsBadRequest()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN_{suffix}",
                MaChiNhanh = $"C{suffix}",
                DiaChi = "Dia chi 1",
                SoDienThoai = "0900000001",
                MoTa = "Mo ta CN"
            };
            dbContext.ChiNhanhs.Add(branch);
            await dbContext.SaveChangesAsync();

            var room = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"P_{suffix}",
                GiaThue = 2000000m,
                DienTich = 25,
                MoTa = "Mo ta phong"
            };
            dbContext.PhongTros.Add(room);
            await dbContext.SaveChangesAsync();

            var createResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = AppRole.NhanVien
            });
            Assert.True(createResult.Success);
            var staffUser = await dbContext.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            var adminUser = await dbContext.NguoiDungs.FirstOrDefaultAsync(u => u.Role == Role.Admin);
            int adminId = adminUser?.NguoiDungId ?? staffUser.NguoiDungId;

            dbContext.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staffUser.NguoiDungId,
                ChiNhanhId = branch.ChiNhanhId,
                IsActive = true,
                NguoiPhanCongId = adminId
            });
            await dbContext.SaveChangesAsync();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
            var loginToken = ExtractAntiForgeryToken(loginHtml);

            var loginRes = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", loginToken),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            }));
            Assert.Equal(HttpStatusCode.Redirect, loginRes.StatusCode);

            var getPageRes = await client.GetAsync("/QuanLyNhaTro/ChotDienNuoc");
            var pageHtml = await getPageRes.Content.ReadAsStringAsync();
            var authedToken = ExtractAntiForgeryToken(pageHtml);

            // Gửi request Manual mode nhưng không có lý do
            var req = new ChotDienNuocReq
            {
                ChiNhanhId = branch.ChiNhanhId,
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new()
                    {
                        PhongTroId = room.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienCu = 0,
                        ChiSoDienMoi = 50,
                        ChiSoNuocCu = 0,
                        ChiSoNuocMoi = 10,
                        LyDoNhapThuCongDien = null, // Thiếu lý do
                        LyDoNhapThuCongNuoc = null
                    }
                }
            };

            var postReq = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/SaveChotDienNuoc")
            {
                Content = JsonContent.Create(req)
            };
            if (!string.IsNullOrEmpty(authedToken))
            {
                postReq.Headers.Add("RequestVerificationToken", authedToken);
            }

            var postRes = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.BadRequest, postRes.StatusCode);
            var content = await postRes.Content.ReadAsStringAsync();
            Assert.Contains("lý do", content, StringComparison.OrdinalIgnoreCase);

            // Cleanup
            dbContext.PhongTros.Remove(room);
            dbContext.NhanVienChiNhanhs.RemoveRange(dbContext.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
            dbContext.ChiNhanhs.Remove(branch);
            await dbContext.SaveChangesAsync();
            await nguoiDungService.DeleteAsync(staffUser.NguoiDungId);
        }

        [Fact]
        public async Task SaveChotDienNuoc_ManualMode_WithReason_Succeeds_AndCreatesDaDuyetRecord()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN_{suffix}",
                MaChiNhanh = $"C{suffix}",
                DiaChi = "Dia chi 1",
                SoDienThoai = "0900000001",
                MoTa = "Mo ta CN"
            };
            dbContext.ChiNhanhs.Add(branch);
            await dbContext.SaveChangesAsync();

            var room = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"P_{suffix}",
                GiaThue = 2000000m,
                DienTich = 25,
                MoTa = "Mo ta phong"
            };
            dbContext.PhongTros.Add(room);
            await dbContext.SaveChangesAsync();

            var createResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = AppRole.NhanVien
            });
            Assert.True(createResult.Success);
            var staffUser = await dbContext.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            var adminUser = await dbContext.NguoiDungs.FirstOrDefaultAsync(u => u.Role == Role.Admin);
            int adminId = adminUser?.NguoiDungId ?? staffUser.NguoiDungId;

            dbContext.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staffUser.NguoiDungId,
                ChiNhanhId = branch.ChiNhanhId,
                IsActive = true,
                NguoiPhanCongId = adminId
            });
            await dbContext.SaveChangesAsync();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
            var loginToken = ExtractAntiForgeryToken(loginHtml);

            var loginRes = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", loginToken),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            }));
            Assert.Equal(HttpStatusCode.Redirect, loginRes.StatusCode);

            var getPageRes = await client.GetAsync("/QuanLyNhaTro/ChotDienNuoc");
            var pageHtml = await getPageRes.Content.ReadAsStringAsync();
            var authedToken = ExtractAntiForgeryToken(pageHtml);

            // Gửi request Manual mode có lý do hợp lệ
            var req = new ChotDienNuocReq
            {
                ChiNhanhId = branch.ChiNhanhId,
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new()
                    {
                        PhongTroId = room.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienCu = 0,
                        ChiSoDienMoi = 50,
                        ChiSoNuocCu = 0,
                        ChiSoNuocMoi = 10,
                        LyDoNhapThuCongDien = "Đồng hồ mờ số",
                        LyDoNhapThuCongNuoc = "Khách vắng nhà"
                    }
                }
            };

            var postReq = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/SaveChotDienNuoc")
            {
                Content = JsonContent.Create(req)
            };
            if (!string.IsNullOrEmpty(authedToken))
            {
                postReq.Headers.Add("RequestVerificationToken", authedToken);
            }

            var postRes = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

            // Kiểm tra DB bản ghi đã được tạo ở trạng thái DaDuyet
            using var verifyScope = _factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var savedRecord = await verifyDb.DichVuDienNuocCuaPhongs.FirstOrDefaultAsync(x => x.PhongTroId == room.PhongTroId && x.Thang == 10 && x.Nam == 2026);
            Assert.NotNull(savedRecord);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, savedRecord.TrangThaiGhiNhan);
            Assert.Equal(50, savedRecord.ChiSoDienMoi);
            Assert.Equal(10, savedRecord.ChiSoNuocMoi);

            // Cleanup
            verifyDb.DichVuDienNuocCuaPhongs.Remove(savedRecord);
            verifyDb.PhongTros.Remove(room);
            verifyDb.NhanVienChiNhanhs.RemoveRange(verifyDb.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
            verifyDb.ChiNhanhs.Remove(branch);
            await verifyDb.SaveChangesAsync();
            await nguoiDungService.DeleteAsync(staffUser.NguoiDungId);
        }

        [Fact]
        public async Task SaveChotDienNuoc_WrongBranch_ReturnsBadRequest()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch1 = new ChiNhanh { TenChiNhanh = $"CN1_{suffix}", MaChiNhanh = $"C1{suffix}", DiaChi = "Dia chi 1", SoDienThoai = "0900000001", MoTa = "Mo ta CN 1" };
            var branch2 = new ChiNhanh { TenChiNhanh = $"CN2_{suffix}", MaChiNhanh = $"C2{suffix}", DiaChi = "Dia chi 2", SoDienThoai = "0900000002", MoTa = "Mo ta CN 2" };
            dbContext.ChiNhanhs.AddRange(branch1, branch2);
            await dbContext.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch1.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 2000000m, DienTich = 20, MoTa = "Mo ta phong 1" };
            dbContext.PhongTros.Add(room1);
            await dbContext.SaveChangesAsync();

            var createResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await dbContext.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            dbContext.NhanVienChiNhanhs.Add(new NhanVienChiNhanh { NguoiDungId = staffUser.NguoiDungId, ChiNhanhId = branch2.ChiNhanhId, IsActive = true, NguoiPhanCongId = staffUser.NguoiDungId });
            await dbContext.SaveChangesAsync();

            var (client, token) = await CreateClientWithAntiforgeryAsync();
            await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            }));

            var getPageRes = await client.GetAsync("/QuanLyNhaTro/ChotDienNuoc");
            var authedToken = ExtractAntiForgeryToken(await getPageRes.Content.ReadAsStringAsync());

            // Nhân viên gửi ChiNhanhId = 2 nhưng phòng 1 thuộc ChiNhanhId = 1
            var req = new ChotDienNuocReq
            {
                ChiNhanhId = branch2.ChiNhanhId,
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new()
                    {
                        PhongTroId = room1.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienCu = 0,
                        ChiSoDienMoi = 50,
                        ChiSoNuocCu = 0,
                        ChiSoNuocMoi = 10,
                        LyDoNhapThuCongDien = "Đồng hồ mờ",
                        LyDoNhapThuCongNuoc = "Khách vắng"
                    }
                }
            };

            var postReq = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/SaveChotDienNuoc") { Content = JsonContent.Create(req) };
            if (!string.IsNullOrEmpty(authedToken)) postReq.Headers.Add("RequestVerificationToken", authedToken);

            var postRes = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.BadRequest, postRes.StatusCode);
            var content = await postRes.Content.ReadAsStringAsync();
            Assert.Contains("không khớp", content, StringComparison.OrdinalIgnoreCase);

            // Cleanup
            dbContext.PhongTros.Remove(room1);
            dbContext.NhanVienChiNhanhs.RemoveRange(dbContext.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
            dbContext.ChiNhanhs.RemoveRange(branch1, branch2);
            await dbContext.SaveChangesAsync();
            await nguoiDungService.DeleteAsync(staffUser.NguoiDungId);
        }

        [Fact]
        public async Task SaveChotDienNuoc_RevokedAssignment_ReturnsBadRequest()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "Dia chi 1", SoDienThoai = "0900000001", MoTa = "Mo ta CN" };
            dbContext.ChiNhanhs.Add(branch);
            await dbContext.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 2000000m, DienTich = 25, MoTa = "Mo ta phong" };
            dbContext.PhongTros.Add(room);
            await dbContext.SaveChangesAsync();

            var createResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await dbContext.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            var assignment = new NhanVienChiNhanh { NguoiDungId = staffUser.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, IsActive = true, NguoiPhanCongId = staffUser.NguoiDungId };
            dbContext.NhanVienChiNhanhs.Add(assignment);
            await dbContext.SaveChangesAsync();

            var (client, token) = await CreateClientWithAntiforgeryAsync();
            await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            }));

            var getPageRes = await client.GetAsync("/QuanLyNhaTro/ChotDienNuoc");
            var authedToken = ExtractAntiForgeryToken(await getPageRes.Content.ReadAsStringAsync());

            // Thu hồi phân công chi nhánh (IsActive = false)
            assignment.IsActive = false;
            await dbContext.SaveChangesAsync();

            var req = new ChotDienNuocReq
            {
                ChiNhanhId = branch.ChiNhanhId,
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new()
                    {
                        PhongTroId = room.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienCu = 0,
                        ChiSoDienMoi = 50,
                        ChiSoNuocCu = 0,
                        ChiSoNuocMoi = 10,
                        LyDoNhapThuCongDien = "Đồng hồ mờ",
                        LyDoNhapThuCongNuoc = "Khách vắng"
                    }
                }
            };

            var postReq = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/SaveChotDienNuoc") { Content = JsonContent.Create(req) };
            if (!string.IsNullOrEmpty(authedToken)) postReq.Headers.Add("RequestVerificationToken", authedToken);

            var postRes = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.BadRequest, postRes.StatusCode);
            var content = await postRes.Content.ReadAsStringAsync();
            Assert.Contains("không có quyền", content, StringComparison.OrdinalIgnoreCase);

            // Cleanup
            dbContext.PhongTros.Remove(room);
            dbContext.NhanVienChiNhanhs.RemoveRange(dbContext.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
            dbContext.ChiNhanhs.Remove(branch);
            await dbContext.SaveChangesAsync();
            await nguoiDungService.DeleteAsync(staffUser.NguoiDungId);
        }

        [Fact]
        public async Task GetChotDienNuocPage_DoesNotContainDefaultManualReason_AndContainsReasonInputElements()
        {
            var (client, token) = await CreateClientWithAntiforgeryAsync();
            var loginRes = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("TenDangNhap", "admin"),
                new KeyValuePair<string, string>("MatKhau", "admin")
            }));
            Assert.Equal(HttpStatusCode.Redirect, loginRes.StatusCode);

            var pageRes = await client.GetAsync("/QuanLyNhaTro/ChotDienNuoc");
            Assert.Equal(HttpStatusCode.OK, pageRes.StatusCode);
            var html = await pageRes.Content.ReadAsStringAsync();

            Assert.DoesNotContain("Ghi nhận trực tiếp tại phòng", html);
            Assert.DoesNotContain("value=\"${item.lyDoNhapThuCong", html);
            Assert.DoesNotContain("lyDoNhapThuCongDien ||", html);
            Assert.DoesNotContain("lyDoNhapThuCongNuoc ||", html);
            Assert.Contains("in-lydo-dien", html);
            Assert.Contains("in-lydo-nuoc", html);
        }

        [Fact]
        public async Task SaveChotDienNuoc_ManualMode_WhitespaceReason_ReturnsBadRequest()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "Dia chi", SoDienThoai = "0900000001", MoTa = "Mo ta" };
            dbContext.ChiNhanhs.Add(branch);
            await dbContext.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 2000000m, DienTich = 25, MoTa = "Mo ta" };
            dbContext.PhongTros.Add(room);
            await dbContext.SaveChangesAsync();

            var createResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await dbContext.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            var assignment = new NhanVienChiNhanh { NguoiDungId = staffUser.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, IsActive = true, NguoiPhanCongId = staffUser.NguoiDungId };
            dbContext.NhanVienChiNhanhs.Add(assignment);
            await dbContext.SaveChangesAsync();

            var (client, token) = await CreateClientWithAntiforgeryAsync();
            await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            }));

            var getPageRes = await client.GetAsync("/QuanLyNhaTro/ChotDienNuoc");
            var authedToken = ExtractAntiForgeryToken(await getPageRes.Content.ReadAsStringAsync());

            var req = new ChotDienNuocReq
            {
                ChiNhanhId = branch.ChiNhanhId,
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new()
                    {
                        PhongTroId = room.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienCu = 0,
                        ChiSoDienMoi = 50,
                        ChiSoNuocCu = 0,
                        ChiSoNuocMoi = 10,
                        LyDoNhapThuCongDien = "   ", // whitespace only
                        LyDoNhapThuCongNuoc = "Khách vắng"
                    }
                }
            };

            var postReq = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/SaveChotDienNuoc") { Content = JsonContent.Create(req) };
            if (!string.IsNullOrEmpty(authedToken)) postReq.Headers.Add("RequestVerificationToken", authedToken);

            var postRes = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.BadRequest, postRes.StatusCode);
            var content = await postRes.Content.ReadAsStringAsync();
            Assert.Contains("lý do", content, StringComparison.OrdinalIgnoreCase);

            // Cleanup
            dbContext.PhongTros.Remove(room);
            dbContext.NhanVienChiNhanhs.RemoveRange(dbContext.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
            dbContext.ChiNhanhs.Remove(branch);
            await dbContext.SaveChangesAsync();
            await nguoiDungService.DeleteAsync(staffUser.NguoiDungId);
        }

        [Fact]
        public async Task SaveChotDienNuoc_OfficialImageMode_WithoutReason_Succeeds()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "Dia chi", SoDienThoai = "0900000001", MoTa = "Mo ta" };
            dbContext.ChiNhanhs.Add(branch);
            await dbContext.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 2000000m, DienTich = 25, MoTa = "Mo ta" };
            dbContext.PhongTros.Add(room);
            await dbContext.SaveChangesAsync();

            var createResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await dbContext.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            var assignment = new NhanVienChiNhanh { NguoiDungId = staffUser.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, IsActive = true, NguoiPhanCongId = staffUser.NguoiDungId };
            dbContext.NhanVienChiNhanhs.Add(assignment);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 0,
                ChiSoNuocCu = 0,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            dbContext.DichVuDienNuocCuaPhongs.Add(period);
            await dbContext.SaveChangesAsync();

            var imgDien = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://res.cloudinary.com/test/image/upload/v1/dien.jpg",
                PublicId = "test/dien",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 150m,
                DuocChonLamChiSoChinhThuc = true,
                NguoiXacNhanId = staffUser.NguoiDungId,
                NgayXacNhan = DateTime.UtcNow
            };
            var imgNuoc = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Nuoc,
                Url = "https://res.cloudinary.com/test/image/upload/v1/nuoc.jpg",
                PublicId = "test/nuoc",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 50m,
                DuocChonLamChiSoChinhThuc = true,
                NguoiXacNhanId = staffUser.NguoiDungId,
                NgayXacNhan = DateTime.UtcNow
            };
            dbContext.AnhChiSoDongHos.AddRange(imgDien, imgNuoc);
            await dbContext.SaveChangesAsync();

            var (client, token) = await CreateClientWithAntiforgeryAsync();
            await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            }));

            var getPageRes = await client.GetAsync("/QuanLyNhaTro/ChotDienNuoc");
            var authedToken = ExtractAntiForgeryToken(await getPageRes.Content.ReadAsStringAsync());

            var req = new ChotDienNuocReq
            {
                ChiNhanhId = branch.ChiNhanhId,
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new()
                    {
                        PhongTroId = room.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.OfficialImage,
                        NuocMode = MeterReadingSubmissionMode.OfficialImage,
                        ChiSoDienCu = 0,
                        ChiSoDienMoi = 150,
                        ChiSoNuocCu = 0,
                        ChiSoNuocMoi = 50,
                        LyDoNhapThuCongDien = null,
                        LyDoNhapThuCongNuoc = null
                    }
                }
            };

            var postReq = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/SaveChotDienNuoc") { Content = JsonContent.Create(req) };
            if (!string.IsNullOrEmpty(authedToken)) postReq.Headers.Add("RequestVerificationToken", authedToken);

            var postRes = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

            // Kiểm tra DB: kỳ đã được duyệt với đúng số
            using var verifyScope = _factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var savedPeriod = await verifyDb.DichVuDienNuocCuaPhongs.FirstAsync(p => p.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, savedPeriod.TrangThaiGhiNhan);
            Assert.Equal(150m, savedPeriod.ChiSoDienMoi);
            Assert.Equal(50m, savedPeriod.ChiSoNuocMoi);

            // Cleanup
            dbContext.AnhChiSoDongHos.RemoveRange(imgDien, imgNuoc);
            dbContext.DichVuDienNuocCuaPhongs.Remove(period);
            dbContext.PhongTros.Remove(room);
            dbContext.NhanVienChiNhanhs.RemoveRange(dbContext.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
            dbContext.ChiNhanhs.Remove(branch);
            await dbContext.SaveChangesAsync();
            await nguoiDungService.DeleteAsync(staffUser.NguoiDungId);
        }

        [Fact]
        public async Task SaveChotDienNuoc_BatchTwoRooms_SecondRoomFails_FirstRoomUnmodifiedInDb()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "Dia chi", SoDienThoai = "0900000001", MoTa = "Mo ta" };
            dbContext.ChiNhanhs.Add(branch);
            await dbContext.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 2000000m, DienTich = 25, MoTa = "Mo ta" };
            var room2 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P2_{suffix}", GiaThue = 2000000m, DienTich = 25, MoTa = "Mo ta" };
            dbContext.PhongTros.AddRange(room1, room2);
            await dbContext.SaveChangesAsync();

            var createResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await dbContext.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            var assignment = new NhanVienChiNhanh { NguoiDungId = staffUser.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, IsActive = true, NguoiPhanCongId = staffUser.NguoiDungId };
            dbContext.NhanVienChiNhanhs.Add(assignment);

            var period1 = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room1.PhongTroId,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 0,
                ChiSoNuocCu = 0,
                ChiSoDienMoi = 0,
                ChiSoNuocMoi = 0,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            dbContext.DichVuDienNuocCuaPhongs.Add(period1);
            await dbContext.SaveChangesAsync();

            var (client, token) = await CreateClientWithAntiforgeryAsync();
            await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            }));

            var getPageRes = await client.GetAsync("/QuanLyNhaTro/ChotDienNuoc");
            var authedToken = ExtractAntiForgeryToken(await getPageRes.Content.ReadAsStringAsync());

            var req = new ChotDienNuocReq
            {
                ChiNhanhId = branch.ChiNhanhId,
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new()
                    {
                        PhongTroId = room1.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienCu = 0,
                        ChiSoDienMoi = 50,
                        ChiSoNuocCu = 0,
                        ChiSoNuocMoi = 10,
                        LyDoNhapThuCongDien = "Lý do hợp lệ phòng 1",
                        LyDoNhapThuCongNuoc = "Lý do hợp lệ phòng 1"
                    },
                    new()
                    {
                        PhongTroId = room2.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienCu = 0,
                        ChiSoDienMoi = 50,
                        ChiSoNuocCu = 0,
                        ChiSoNuocMoi = 10,
                        LyDoNhapThuCongDien = null, // Thiếu lý do -> fail phòng 2
                        LyDoNhapThuCongNuoc = null
                    }
                }
            };

            var postReq = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/SaveChotDienNuoc") { Content = JsonContent.Create(req) };
            if (!string.IsNullOrEmpty(authedToken)) postReq.Headers.Add("RequestVerificationToken", authedToken);

            var postRes = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.BadRequest, postRes.StatusCode);

            // Kiểm tra DB: room1 KHÔNG bị cập nhật (vẫn là Nhap, chỉ số mới vẫn là 0)
            using var verifyScope = _factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var savedPeriod1 = await verifyDb.DichVuDienNuocCuaPhongs.FirstAsync(p => p.DichVuDienNuocCuaPhongId == period1.DichVuDienNuocCuaPhongId);
            Assert.Equal(TrangThaiGhiNhan.Nhap, savedPeriod1.TrangThaiGhiNhan);
            Assert.Equal(0m, savedPeriod1.ChiSoDienMoi);
            Assert.Equal(0m, savedPeriod1.ChiSoNuocMoi);

            // Cleanup
            dbContext.DichVuDienNuocCuaPhongs.Remove(period1);
            dbContext.PhongTros.RemoveRange(room1, room2);
            dbContext.NhanVienChiNhanhs.RemoveRange(dbContext.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
            dbContext.ChiNhanhs.Remove(branch);
            await dbContext.SaveChangesAsync();
            await nguoiDungService.DeleteAsync(staffUser.NguoiDungId);
        }
    }
}
