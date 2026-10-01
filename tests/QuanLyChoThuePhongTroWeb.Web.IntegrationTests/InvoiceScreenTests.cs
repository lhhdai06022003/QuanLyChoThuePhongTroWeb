using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
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
    public class InvoiceScreenTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public InvoiceScreenTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
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

        private static async Task<string> LoginStaffAsync(HttpClient client, string username, string password)
        {
            var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
            var antiForgeryToken = ExtractAntiForgeryToken(loginHtml);

            var loginContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            });

            var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

            var getHoaDon = await client.GetAsync("/QuanLyNhaTro/QuanLyHoaDon");
            var hoaDonHtml = await getHoaDon.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(hoaDonHtml);
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Remove("RequestVerificationToken");
                client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
            }
            return token;
        }

        private class TestContextData
        {
            public ChiNhanh Branch1 { get; set; } = null!;
            public ChiNhanh Branch2 { get; set; } = null!;
            public string StaffUsername { get; set; } = string.Empty;
            public string StaffPassword { get; set; } = string.Empty;
            public int StaffUserId { get; set; }
            public HoaDon InvoiceNhap { get; set; } = null!;
            public HoaDon InvoiceChoDuyet { get; set; } = null!;
            public HoaDon InvoiceDaChot { get; set; } = null!;
            public HoaDon InvoiceDaGui { get; set; } = null!;
            public HoaDon InvoiceCancelled1 { get; set; } = null!;
            public HoaDon InvoiceCancelled2 { get; set; } = null!;
            public HoaDon InvoiceDaChot2 { get; set; } = null!;
        }

        private async Task<TestContextData> SeedTestDataAsync(string prefix)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var suffix = Guid.NewGuid().ToString("N")[..8];

            var branch1 = new ChiNhanh
            {
                TenChiNhanh = $"CN1_{prefix}_{suffix}",
                MaChiNhanh = $"C1{suffix[..5]}",
                DiaChi = "123 Duong 1",
                SoDienThoai = "0900000001",
                MoTa = "CN 1"
            };
            var branch2 = new ChiNhanh
            {
                TenChiNhanh = $"CN2_{prefix}_{suffix}",
                MaChiNhanh = $"C2{suffix[..5]}",
                DiaChi = "456 Duong 2",
                SoDienThoai = "0900000002",
                MoTa = "CN 2"
            };
            db.ChiNhanhs.AddRange(branch1, branch2);
            await db.SaveChangesAsync();

            var tenant = new NguoiThue
            {
                HoVaTen = $"Khach_{suffix}",
                SoDienThoai = $"091{suffix[..6]}",
                Email = $"tenant_{suffix}@example.com",
                CCCD = $"079{suffix[..6]}"
            };
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            var staffUsername = $"staff_{prefix}_{suffix}";
            const string staffPassword = "StaffPassword123!";
            var addRes = await nguoiDungService.AddAsync(new CreateNguoiDungReq
            {
                TenDangNhap = staffUsername,
                MatKhau = staffPassword,
                Role = AppRole.NhanVien
            });
            Assert.True(addRes.Success);

            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == staffUsername);
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staffUser.NguoiDungId,
                ChiNhanhId = branch1.ChiNhanhId,
                IsActive = true,
                NguoiPhanCongId = staffUser.NguoiDungId,
                NgayPhanCong = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            HopDong CreateContract(ChiNhanh b, string code)
            {
                var room = new PhongTro
                {
                    ChiNhanhId = b.ChiNhanhId,
                    SoPhong = $"P_{code}_{suffix[..4]}",
                    GiaThue = 2000000m,
                    DienTich = 20,
                    MoTa = "Room test"
                };
                db.PhongTros.Add(room);
                db.SaveChanges();

                var contract = new HopDong
                {
                    MaHopDong = $"HD_{code}_{suffix[..4]}",
                    PhongTroId = room.PhongTroId,
                    NguoiThueId = tenant.NguoiThueId,
                    ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-1),
                    TienCocPhong = 2000000m,
                    TienThuePhong = 2000000m,
                    TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
                };
                db.HopDongs.Add(contract);
                db.SaveChanges();
                return contract;
            }

            var cNhap = CreateContract(branch1, "NHAP");
            var cChoDuyet = CreateContract(branch1, "CHODUYET");
            var cDaChot = CreateContract(branch1, "DACHOT");
            var cDaGui = CreateContract(branch1, "DAGUI");
            var cCancelled1 = CreateContract(branch1, "HUY1");
            var cCancelled2 = CreateContract(branch2, "HUY2");
            var cDaChot2 = CreateContract(branch2, "DACHOT2");

            HoaDon CreateInvoice(HopDong c, string code, TrangThaiPhatHanhHoaDon pubStatus, bool isDeleted)
            {
                var inv = new HoaDon
                {
                    HopDongId = c.HopDongId,
                    MaHoaDon = $"HD_{code}_{suffix[..4]}",
                    Thang = 10,
                    Nam = 2026,
                    TongTien = 2000000m,
                    TrangThaiPhatHanh = pubStatus,
                    TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                    IsDeleted = isDeleted,
                    NgayTao = DateTime.UtcNow.AddDays(-2),
                    NgayGui = pubStatus == TrangThaiPhatHanhHoaDon.DaGui || pubStatus == TrangThaiPhatHanhHoaDon.DaHuy ? DateTime.UtcNow.AddDays(-1) : null,
                    ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                    {
                        new ChiTietHoaDon { TenDichVu = "Tiền phòng", TongTien = 2000000m, DonGia = 2000000m, SoLuong = 1 }
                    }
                };
                db.HoaDons.Add(inv);
                db.SaveChanges();
                return inv;
            }

            var invNhap = CreateInvoice(cNhap, "NHAP", TrangThaiPhatHanhHoaDon.Nhap, false);
            var invChoDuyet = CreateInvoice(cChoDuyet, "CHODUYET", TrangThaiPhatHanhHoaDon.ChoDuyet, false);
            var invDaChot = CreateInvoice(cDaChot, "DACHOT", TrangThaiPhatHanhHoaDon.DaChot, false);
            var invDaGui = CreateInvoice(cDaGui, "DAGUI", TrangThaiPhatHanhHoaDon.DaGui, false);
            var invCancelled1 = CreateInvoice(cCancelled1, "HUY1", TrangThaiPhatHanhHoaDon.DaHuy, true);
            var invCancelled2 = CreateInvoice(cCancelled2, "HUY2", TrangThaiPhatHanhHoaDon.DaHuy, true);
            var invDaChot2 = CreateInvoice(cDaChot2, "DACHOT2", TrangThaiPhatHanhHoaDon.DaChot, false);

            // History for invCancelled1
            db.LichSuTrangThaiHoaDons.AddRange(
                new LichSuTrangThaiHoaDon
                {
                    HoaDonId = invCancelled1.HoaDonId,
                    TrangThaiPhatHanhCu = TrangThaiPhatHanhHoaDon.ChoDuyet,
                    TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.DaChot,
                    TrangThaiThanhToanCu = TrangThaiHoaDon.ChuaThanhToan,
                    TrangThaiThanhToanMoi = TrangThaiHoaDon.ChuaThanhToan,
                    NguoiThucHienId = staffUser.NguoiDungId,
                    NgayThucHien = DateTime.UtcNow.AddHours(-2),
                    LyDo = "cũ"
                },
                new LichSuTrangThaiHoaDon
                {
                    HoaDonId = invCancelled1.HoaDonId,
                    TrangThaiPhatHanhCu = TrangThaiPhatHanhHoaDon.DaChot,
                    TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.DaHuy,
                    TrangThaiThanhToanCu = TrangThaiHoaDon.ChuaThanhToan,
                    TrangThaiThanhToanMoi = TrangThaiHoaDon.ChuaThanhToan,
                    NguoiThucHienId = null,
                    NgayThucHien = DateTime.UtcNow.AddHours(-1),
                    LyDo = "mới"
                }
            );
            await db.SaveChangesAsync();

            return new TestContextData
            {
                Branch1 = branch1,
                Branch2 = branch2,
                StaffUsername = staffUsername,
                StaffPassword = staffPassword,
                StaffUserId = staffUser.NguoiDungId,
                InvoiceNhap = invNhap,
                InvoiceChoDuyet = invChoDuyet,
                InvoiceDaChot = invDaChot,
                InvoiceDaGui = invDaGui,
                InvoiceCancelled1 = invCancelled1,
                InvoiceCancelled2 = invCancelled2,
                InvoiceDaChot2 = invDaChot2
            };
        }

        private HttpClient CreateHttpClient()
        {
            return _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
        }

        [Fact]
        public async Task GetList_DefaultFilter_NoCancelled()
        {
            var data = await SeedTestDataAsync("GL_Def");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("chiNhanhId", data.Branch1.ChiNhanhId.ToString()),
                new KeyValuePair<string, string>("trangThaiPhatHanh", "-1")
            });

            var response = await client.PostAsync("/HoaDon/GetList", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = doc.RootElement.GetProperty("data").EnumerateArray()
                .Select(e => e.GetProperty("hoaDonId").GetInt32()).ToList();

            Assert.DoesNotContain(data.InvoiceCancelled1.HoaDonId, items);
            Assert.Contains(data.InvoiceDaChot.HoaDonId, items);
        }

        [Fact]
        public async Task GetList_FilterCancelled_OnlyCancelled()
        {
            var data = await SeedTestDataAsync("GL_Can");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("chiNhanhId", data.Branch1.ChiNhanhId.ToString()),
                new KeyValuePair<string, string>("trangThaiPhatHanh", "4")
            });

            var response = await client.PostAsync("/HoaDon/GetList", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = doc.RootElement.GetProperty("data").EnumerateArray()
                .Select(e => e.GetProperty("hoaDonId").GetInt32()).ToList();

            Assert.Contains(data.InvoiceCancelled1.HoaDonId, items);
            Assert.DoesNotContain(data.InvoiceDaChot.HoaDonId, items);
            Assert.DoesNotContain(data.InvoiceNhap.HoaDonId, items);
        }

        [Fact]
        public async Task GetList_FilterDaChot_OnlyDaChot()
        {
            var data = await SeedTestDataAsync("GL_Chot");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("chiNhanhId", data.Branch1.ChiNhanhId.ToString()),
                new KeyValuePair<string, string>("trangThaiPhatHanh", "2")
            });

            var response = await client.PostAsync("/HoaDon/GetList", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = doc.RootElement.GetProperty("data").EnumerateArray()
                .Select(e => e.GetProperty("hoaDonId").GetInt32()).ToList();

            Assert.Contains(data.InvoiceDaChot.HoaDonId, items);
            Assert.DoesNotContain(data.InvoiceNhap.HoaDonId, items);
            Assert.DoesNotContain(data.InvoiceCancelled1.HoaDonId, items);
        }

        [Fact]
        public async Task GetList_StaffOtherBranch_Empty()
        {
            var data = await SeedTestDataAsync("GL_Other");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("chiNhanhId", data.Branch2.ChiNhanhId.ToString())
            });

            var response = await client.PostAsync("/HoaDon/GetList", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(0, doc.RootElement.GetProperty("recordsTotal").GetInt32());
            Assert.Empty(doc.RootElement.GetProperty("data").EnumerateArray());
        }

        [Fact]
        public async Task GetById_Cancelled_SameBranch_200_HasReasonAndHistory()
        {
            var data = await SeedTestDataAsync("GBI_Same");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var response = await client.GetAsync($"/HoaDon/GetById/{data.InvoiceCancelled1.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.True(root.GetProperty("daHuy").GetBoolean());
            Assert.Equal("mới", root.GetProperty("lyDoHuy").GetString());
            Assert.Equal("Đã hủy", root.GetProperty("trangThaiPhatHanh").GetString());
            var history = root.GetProperty("lichSuTrangThais").EnumerateArray().ToList();
            Assert.Equal(2, history.Count);

            // Tab "Lịch sử trạng thái" trong view đọc đúng các khóa này.
            foreach (var key in new[] { "thoiGian", "phatHanhTu", "phatHanhDen", "thanhToanTu", "thanhToanDen", "nguoiThucHien", "lyDo" })
            {
                Assert.True(history[0].TryGetProperty(key, out _), $"Thiếu khóa '{key}' trong lịch sử trạng thái.");
            }
            Assert.False(string.IsNullOrEmpty(history[0].GetProperty("thoiGian").GetString()));
        }

        [Fact]
        public async Task IndexHtml_HistoryTabReadsDtoFieldNames()
        {
            var data = await SeedTestDataAsync("HistFields");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var html = await client.GetStringAsync("/QuanLyNhaTro/QuanLyHoaDon");

            foreach (var field in new[] { "ls.thoiGian", "ls.phatHanhTu", "ls.phatHanhDen", "ls.thanhToanTu", "ls.thanhToanDen", "ls.nguoiThucHien" })
            {
                Assert.Contains(field, html);
            }
            Assert.DoesNotContain("ls.ngayThucHien", html);
            Assert.DoesNotContain("ls.tenNguoiThucHien", html);
        }

        [Fact]
        public async Task GetById_Cancelled_OtherBranch_404()
        {
            var data = await SeedTestDataAsync("GBI_Other");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var response = await client.GetAsync($"/HoaDon/GetById/{data.InvoiceCancelled2.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ExportPdf_Cancelled_200_ApplicationPdf()
        {
            var data = await SeedTestDataAsync("ExpPdf");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var response = await client.GetAsync($"/HoaDon/ExportPdf/{data.InvoiceCancelled1.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task ExportExcel_Cancelled_200()
        {
            var data = await SeedTestDataAsync("ExpExcel");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var response = await client.GetAsync($"/HoaDon/ExportExcel/{data.InvoiceCancelled1.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task RevokedStaff_ListEmpty_DetailAndPdf404()
        {
            var data = await SeedTestDataAsync("Revoked");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            // Thu hồi phân công của nhân viên
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var assignment = await db.NhanVienChiNhanhs.FirstAsync(a => a.NguoiDungId == data.StaffUserId && a.ChiNhanhId == data.Branch1.ChiNhanhId);
                assignment.IsActive = false;
                await db.SaveChangesAsync();
            }

            // Gọi lại bằng CÙNG PHIÊN ĐĂNG NHẬP
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("chiNhanhId", data.Branch1.ChiNhanhId.ToString())
            });
            var listRes = await client.PostAsync("/HoaDon/GetList", content);
            Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
            using var doc = JsonDocument.Parse(await listRes.Content.ReadAsStringAsync());
            Assert.Equal(0, doc.RootElement.GetProperty("recordsTotal").GetInt32());

            var detailRes = await client.GetAsync($"/HoaDon/GetById/{data.InvoiceCancelled1.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, detailRes.StatusCode);

            var pdfRes = await client.GetAsync($"/HoaDon/ExportPdf/{data.InvoiceCancelled1.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, pdfRes.StatusCode);
        }

        [Fact]
        public async Task CongBo_BatchWithOneCancelled_OtherStillPublished()
        {
            var data = await SeedTestDataAsync("CongBoBatch");
            var client = CreateHttpClient();
            var token = await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
            {
                Content = JsonContent.Create(new
                {
                    hoaDonIds = new[] { data.InvoiceDaChot.HoaDonId, data.InvoiceCancelled1.HoaDonId }
                })
            };

            var response = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var arr = doc.RootElement.GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(2, arr.Count);

            var chotResult = arr.First(x => x.GetProperty("hoaDonId").GetInt32() == data.InvoiceDaChot.HoaDonId);
            Assert.True(chotResult.GetProperty("congBoThanhCong").GetBoolean());

            var huyResult = arr.First(x => x.GetProperty("hoaDonId").GetInt32() == data.InvoiceCancelled1.HoaDonId);
            Assert.False(huyResult.GetProperty("congBoThanhCong").GetBoolean());

            // Check in DB
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reloadedChot = await db.HoaDons.FindAsync(data.InvoiceDaChot.HoaDonId);
            Assert.NotNull(reloadedChot);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, reloadedChot.TrangThaiPhatHanh);
        }

        [Fact]
        public async Task IndexHtml_HasPublishFilterBulkToolbarAndHistoryTab()
        {
            var data = await SeedTestDataAsync("HtmlCheck");
            var client = CreateHttpClient();
            await LoginStaffAsync(client, data.StaffUsername, data.StaffPassword);

            var response = await client.GetAsync("/QuanLyNhaTro/QuanLyHoaDon");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("id=\"filterPhatHanh\"", html);
            Assert.Contains("id=\"congBoKetQuaModal\"", html);
            Assert.Contains("Lịch sử trạng thái", html);
        }
    }
}
