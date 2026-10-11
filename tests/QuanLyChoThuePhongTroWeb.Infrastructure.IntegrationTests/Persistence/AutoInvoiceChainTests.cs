using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Chạy cả chuỗi hóa đơn tự động bằng service thật trên DB test; chỉ thay OCR, lưu ảnh, email, SignalR và đồng hồ.
    [Collection("PostgreSqlCollection")]
    public class AutoInvoiceChainTests
    {
        // 15/10/2026 17:00 giờ VN: kỳ hiện tại là 10/2026.
        private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);
        private const int Thang = 10;
        private const int Nam = 2026;
        private const int SoGio = 48;

        private readonly PostgreSqlFixture _fixture;

        public AutoInvoiceChainTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed class FixedClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
        }

        private sealed class FakeStorage : IMeterImageStorageService
        {
            public Task<MeterImageUploadResult> UploadAsync(UploadFile file, string folder, CancellationToken cancellationToken = default)
            {
                var id = Guid.NewGuid().ToString("N");
                return Task.FromResult(new MeterImageUploadResult($"https://example.test/{id}.jpg", $"chain_{id}"));
            }

            public Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default)
                => Task.FromResult(new MeterImageReadResult(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 }, "image/jpeg"));

            public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class FakeOcr : IMeterOcrService
        {
            public Func<LoaiDongHo, MeterOcrResult> OnProcess { get; set; } = loai => loai == LoaiDongHo.Dien
                ? MeterOcrResult.Readable(120m, 0.95)
                : MeterOcrResult.Readable(60m, 0.95);

            public Task<MeterOcrResult> ProcessImageAsync(byte[] imageBytes, string contentType, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
                => Task.FromResult(OnProcess(loaiDongHo));
        }

        private sealed class RecordingEmail : IEmailService
        {
            public ConcurrentQueue<(string To, ProvisionalInvoiceEmailData Data)> Provisional { get; } = new();

            public Task<(bool IsSuccess, string ErrorMessage)> SendProvisionalInvoiceEmailAsync(string toEmail, ProvisionalInvoiceEmailData data)
            {
                Provisional.Enqueue((toEmail, data));
                return Task.FromResult((true, string.Empty));
            }

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
                => Task.FromResult((true, string.Empty));

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
                => Task.FromResult((true, string.Empty));
        }

        private sealed class NoopNotifier : IThongBaoNotifier
        {
            public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class Seed
        {
            public ChiNhanh Branch { get; init; } = null!;
            public PhongTro Room { get; init; } = null!;
            public NguoiThue Tenant { get; init; } = null!;
            public NguoiDung TenantAccount { get; init; } = null!;
            public NguoiDung Staff { get; init; } = null!;
            public NguoiDung Admin { get; init; } = null!;
            public HopDong Contract { get; init; } = null!;
        }

        private sealed class Host : IDisposable
        {
            public ServiceProvider Provider { get; }
            public FakeOcr Ocr { get; } = new();
            public RecordingEmail Email { get; } = new();

            public Host(string connectionString)
            {
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString })
                    .Build();
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddSingleton<TimeProvider>(new FixedClock());
                services.AddInfrastructure(config);
                services.AddApplication();
                services.RemoveAll<IMeterImageStorageService>();
                services.AddSingleton<IMeterImageStorageService>(new FakeStorage());
                services.RemoveAll<IMeterOcrService>();
                services.AddSingleton<IMeterOcrService>(Ocr);
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService>(Email);
                services.AddScoped<IThongBaoNotifier, NoopNotifier>();
                Provider = services.BuildServiceProvider(validateScopes: true);
            }

            public void Dispose() => Provider.Dispose();
        }

        private async Task<Seed> SeedAsync(string? email = null, bool withAccount = true, bool withPreviousPeriod = true)
        {
            await using var db = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var admin = new NguoiDung { TenDangNhap = $"ca_{suffix}", MatKhauHash = "x", Role = Role.Admin, IsActive = true };
            var staff = new NguoiDung { TenDangNhap = $"cs_{suffix}", MatKhauHash = "x", Role = Role.NhanVien, IsActive = true };
            var branch = new ChiNhanh
            {
                TenChiNhanh = $"Chain {suffix}",
                MaChiNhanh = TestCodes.MaChiNhanh("H"),
                DiaChi = "Dia chi",
                SoDienThoai = "0900000099",
                MoTa = "x",
                SoGioPhanHoiHoaDon = SoGio
            };
            db.AddRange(admin, staff, branch);
            await db.SaveChangesAsync();

            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staff.NguoiDungId,
                ChiNhanhId = branch.ChiNhanhId,
                NguoiPhanCongId = admin.NguoiDungId,
                NgayPhanCong = Now.AddDays(-30),
                IsActive = true
            });

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"C{suffix[..4]}", GiaThue = 3_000_000m, DienTich = 20, MoTa = "x" };
            var tenant = new NguoiThue
            {
                HoVaTen = $"Khach {suffix}",
                SoDienThoai = "0911222333",
                Email = email ?? $"khach_{suffix}@test.vn",
                CCCD = $"C{suffix}"
            };
            db.AddRange(room, tenant);
            await db.SaveChangesAsync();

            NguoiDung? account = null;
            if (withAccount)
            {
                account = new NguoiDung { TenDangNhap = $"ck_{suffix}", MatKhauHash = "x", Role = Role.KhachThue, IsActive = true, NguoiThueId = tenant.NguoiThueId };
                db.NguoiDungs.Add(account);
            }

            var contract = new HopDong
            {
                MaHopDong = $"HDC{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc), // 01/10/2026 00:00 giờ VN
                TienCocPhong = 3_000_000m,
                TienThuePhong = 3_000_000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            db.HopDongs.Add(contract);
            if (withPreviousPeriod)
            {
                // Kỳ 09/2026 đã duyệt: chỉ số cũ của tháng 10 là 100 điện / 50 nước, tiêu thụ kỳ trước 10 và 5.
                db.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId, Thang = 9, Nam = 2026,
                    ChiSoDienCu = 90, ChiSoDienMoi = 100, ChiSoNuocCu = 45, ChiSoNuocMoi = 50,
                    DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
                });
            }
            await db.SaveChangesAsync();

            var seed = new Seed
            {
                Branch = branch, Room = room, Tenant = tenant, TenantAccount = account!, Staff = staff, Admin = admin, Contract = contract
            };
            // Chuỗi tự tạo dừng với lý do ThieuDonGia khi kỳ có tiêu thụ mà chi nhánh chưa có đơn giá, nên mọi chi nhánh seed đều có đơn giá.
            await SeedUtilityRatesAsync(seed);
            return seed;
        }

        // Đơn giá điện 3.500 và nước 20.000 của chi nhánh, để kỳ mới sinh ra có tiền điện nước.
        private async Task SeedUtilityRatesAsync(Seed seed)
        {
            await using var db = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var dien = new DichVu { TenDichVu = $"Dien {suffix}", DonVi = "kWh", LoaiDichVu = LoaiDichVu.Dien };
            var nuoc = new DichVu { TenDichVu = $"Nuoc {suffix}", DonVi = "m3", LoaiDichVu = LoaiDichVu.Nuoc };
            db.DichVus.AddRange(dien, nuoc);
            await db.SaveChangesAsync();
            db.DichVuChiNhanhs.AddRange(
                new DichVuChiNhanh { ChiNhanhId = seed.Branch.ChiNhanhId, DichVuId = dien.DichVuId, GiaDichVu = 3500m },
                new DichVuChiNhanh { ChiNhanhId = seed.Branch.ChiNhanhId, DichVuId = nuoc.DichVuId, GiaDichVu = 20000m });
            await db.SaveChangesAsync();
        }

        private static UploadFile Photo() => new(new MemoryStream(new byte[] { 1, 2, 3, 4 }), "meter.jpg", "image/jpeg", 4);

        private static async Task UploadAsync(Host host, Seed seed, int actorId, AppLoaiDongHo loai)
        {
            await using var scope = host.Provider.CreateAsyncScope();
            var portal = scope.ServiceProvider.GetRequiredService<IMeterImagePortalService>();
            var result = await portal.UploadAsync(new MeterImageUploadCommand
            {
                PhongTroId = seed.Room.PhongTroId,
                Thang = Thang,
                Nam = Nam,
                LoaiDongHo = loai,
                File = Photo()
            }, actorId);
            Assert.True(result.Success, result.Message);
        }

        private static async Task UploadWithoutTriggerAsync(Host host, Seed seed, int actorId, LoaiDongHo loai)
        {
            await using var scope = host.Provider.CreateAsyncScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IMeterReadingWorkflowService>();
            var result = await workflow.UploadImageAsync(new UploadMeterImageRequest
            {
                PhongTroId = seed.Room.PhongTroId,
                Thang = Thang,
                Nam = Nam,
                LoaiDongHo = loai,
                File = Photo()
            }, actorId);
            Assert.True(result.Success, result.Message);
        }

        private async Task<List<HoaDon>> InvoicesAsync(Seed seed)
        {
            await using var db = _fixture.CreateDbContext();
            return await db.HoaDons
                .AsNoTracking()
                .Include(h => h.LichSuTrangThaiHoaDons)
                .Where(h => h.HopDongId == seed.Contract.HopDongId && !h.IsDeleted)
                .ToListAsync();
        }

        [Fact]
        public async Task TenantUploadsBothMeters_CreatesOneProvisionalInvoice_WithConfirmationAndNotifications()
        {
            using var host = new Host(_fixture.ConnectionString);
            var seed = await SeedAsync();

            await UploadAsync(host, seed, seed.TenantAccount.NguoiDungId, AppLoaiDongHo.Dien);
            Assert.Empty(await InvoicesAsync(seed));
            await UploadAsync(host, seed, seed.TenantAccount.NguoiDungId, AppLoaiDongHo.Nuoc);

            var invoice = Assert.Single(await InvoicesAsync(seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, invoice.TrangThaiPhatHanh);
            Assert.Equal(Now.AddHours(SoGio), invoice.HanPhanHoi);
            Assert.True(invoice.TongTien > 0);
            var history = invoice.LichSuTrangThaiHoaDons.OrderBy(h => h.LichSuTrangThaiHoaDonId).ToList();
            Assert.Equal(2, history.Count);
            Assert.StartsWith("[TU_DONG]", history[1].LyDo);
            Assert.Equal(seed.Staff.NguoiDungId, history[1].NguoiThucHienId);

            await using var db = _fixture.CreateDbContext();
            var period = await db.DichVuDienNuocCuaPhongs.AsNoTracking()
                .SingleAsync(p => p.PhongTroId == seed.Room.PhongTroId && p.Thang == Thang && p.Nam == Nam);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
            Assert.Equal(invoice.DichVuDienNuocCuaPhongId, period.DichVuDienNuocCuaPhongId);

            var images = await db.AnhChiSoDongHos.AsNoTracking().Where(a => a.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId).ToListAsync();
            Assert.Equal(2, images.Count);
            Assert.All(images, a =>
            {
                Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, a.TrangThaiXuLy);
                Assert.True(a.DuocChonLamChiSoChinhThuc);
                Assert.Equal(seed.Staff.NguoiDungId, a.NguoiXacNhanId);
                Assert.StartsWith("Tự xác nhận, độ tin cậy 0,95", a.GhiChuXacNhan);
            });

            var notices = await db.ThongBaos.AsNoTracking()
                .Where(t => t.NguoiDungId == seed.TenantAccount.NguoiDungId).ToListAsync();
            var notice = Assert.Single(notices);
            Assert.Equal($"/KhachThue/HoaDon?hoaDonId={invoice.HoaDonId}", notice.LinkDieuHuong);
            Assert.Contains("tạm tính", notice.NoiDung);

            var mail = Assert.Single(host.Email.Provisional);
            Assert.Equal(seed.Tenant.Email, mail.To);
            Assert.Equal(invoice.MaHoaDon, mail.Data.MaHoaDon);
            Assert.Equal(invoice.TongTien, mail.Data.TongTien);
            Assert.Equal(Now.AddHours(SoGio), mail.Data.HanTuChotUtc);
        }

        [Fact]
        public async Task AutoInvoice_HasSameMoneyAsManualFlowForIdenticalRoom()
        {
            using var host = new Host(_fixture.ConnectionString);
            var auto = await SeedAsync();
            var manual = await SeedAsync();

            await UploadAsync(host, auto, auto.TenantAccount.NguoiDungId, AppLoaiDongHo.Dien);
            await UploadAsync(host, auto, auto.TenantAccount.NguoiDungId, AppLoaiDongHo.Nuoc);

            // Luồng tay: tải ảnh không kích hoạt, tự xác nhận, chốt kỳ, tạo nháp bằng đúng service phát hành.
            await UploadWithoutTriggerAsync(host, manual, manual.Staff.NguoiDungId, LoaiDongHo.Dien);
            await UploadWithoutTriggerAsync(host, manual, manual.Staff.NguoiDungId, LoaiDongHo.Nuoc);
            List<AnhChiSoDongHo> images;
            await using (var db = _fixture.CreateDbContext())
            {
                images = await db.AnhChiSoDongHos.AsNoTracking()
                    .Where(a => a.DichVuDienNuocCuaPhong.PhongTroId == manual.Room.PhongTroId).ToListAsync();
            }
            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var workflow = scope.ServiceProvider.GetRequiredService<IMeterReadingWorkflowService>();
                foreach (var image in images)
                {
                    var confirm = await workflow.ConfirmImageAsync(new ConfirmMeterImageRequest
                    {
                        AnhChiSoDongHoId = image.AnhChiSoDongHoId,
                        GiaTriXacNhan = image.LoaiDongHo == LoaiDongHo.Dien ? 120m : 60m
                    }, manual.Staff.NguoiDungId);
                    Assert.True(confirm.Success, confirm.Message);
                }
                var approve = await workflow.ApprovePeriodsAsync(new ApproveMeterPeriodsRequest
                {
                    Thang = Thang,
                    Nam = Nam,
                    DanhSachPhong = new[]
                    {
                        new ApproveMeterPeriodItem
                        {
                            PhongTroId = manual.Room.PhongTroId,
                            DienMode = MeterReadingSubmissionMode.OfficialImage,
                            NuocMode = MeterReadingSubmissionMode.OfficialImage
                        }
                    }
                }, manual.Staff.NguoiDungId);
                Assert.True(approve.Success, approve.Message);
            }

            // Phiên mới như một yêu cầu HTTP riêng: số liệu được đọc lại từ DB, giống chuỗi tự động (đã dọn ChangeTracker).
            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var drafts = await scope.ServiceProvider.GetRequiredService<IInvoiceIssuanceService>().CreateDraftsAsync(
                    new CreateInvoiceDraftsRequest { ChiNhanhId = manual.Branch.ChiNhanhId, Thang = Thang, Nam = Nam, PhongTroIds = new[] { manual.Room.PhongTroId } },
                    manual.Staff.NguoiDungId);
                Assert.True(drafts.Success, drafts.Message);
            }

            var autoInvoice = Assert.Single(await InvoicesAsync(auto));
            var manualInvoice = Assert.Single(await InvoicesAsync(manual));
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, autoInvoice.TrangThaiPhatHanh);
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, manualInvoice.TrangThaiPhatHanh);

            await using var verify = _fixture.CreateDbContext();
            async Task<List<(string Ten, decimal SoLuong, decimal DonGia, decimal Tong)>> LinesAsync(int id)
                => (await verify.ChiTietHoaDons.AsNoTracking().Where(c => c.HoaDonId == id).OrderBy(c => c.TenDichVu).ToListAsync())
                    .Select(c => (c.TenDichVu, c.SoLuong, c.DonGia, c.TongTien)).ToList();

            var autoLines = await LinesAsync(autoInvoice.HoaDonId);
            var manualLines = await LinesAsync(manualInvoice.HoaDonId);
            Assert.NotEmpty(autoLines);
            Assert.Equal(manualLines, autoLines);
            Assert.Equal(manualInvoice.TongTien, autoInvoice.TongTien);
            Assert.Equal(autoLines.Sum(l => l.Tong), autoInvoice.TongTien);
            // 3.000.000 tiền phòng + 20 kWh x 3.500 + 10 m3 x 20.000.
            Assert.Equal(3_270_000m, autoInvoice.TongTien);
        }

        [Fact]
        public async Task ProvisionalInvoice_ShowsDeadlineInVietnamTime_AndIsNotCountedAsDebt()
        {
            using var host = new Host(_fixture.ConnectionString);
            var seed = await SeedAsync();
            await UploadAsync(host, seed, seed.TenantAccount.NguoiDungId, AppLoaiDongHo.Dien);
            await UploadAsync(host, seed, seed.TenantAccount.NguoiDungId, AppLoaiDongHo.Nuoc);
            var invoice = Assert.Single(await InvoicesAsync(seed));

            await using var db = _fixture.CreateDbContext();
            var notice = Assert.Single(await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seed.TenantAccount.NguoiDungId).ToListAsync());
            // Hạn UTC 17/10/2026 10:00 hiển thị 17:00 giờ Việt Nam.
            Assert.Contains("17/10/2026 17:00", notice.NoiDung);
            Assert.DoesNotContain("17/10/2026 10:00", notice.NoiDung);

            await using var scope = host.Provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence.IHoaDonStore>();
            var unpaid = await store.GetUnpaidInvoicesAsync(seed.Branch.ChiNhanhId, Thang, Nam);
            Assert.DoesNotContain(unpaid, u => u.HoaDonId == invoice.HoaDonId);
            var overdue = await store.GetOverdueInvoicesAsync(Now.AddYears(1));
            Assert.DoesNotContain(overdue, o => o.HoaDonId == invoice.HoaDonId);
        }

        [Fact]
        public async Task StaffOfAnotherBranch_CannotRunAutoInvoice_AndNoInvoiceIsCreated()
        {
            using var host = new Host(_fixture.ConnectionString);
            var seed = await SeedAsync();
            var other = await SeedAsync();
            await UploadWithoutTriggerAsync(host, seed, seed.Staff.NguoiDungId, LoaiDongHo.Dien);
            await UploadWithoutTriggerAsync(host, seed, seed.Staff.NguoiDungId, LoaiDongHo.Nuoc);

            await using var scope = host.Provider.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IAutoInvoiceService>()
                .RunForRoomAsync(other.Staff.NguoiDungId, AutoInvoiceTrigger.XacNhanAnh, seed.Room.PhongTroId, Thang, Nam);

            Assert.Equal(AutoInvoiceOutcome.DungCoThongBao, result.Data!.Outcome);
            Assert.Contains(AutoInvoiceReason.NguoiThucHienThieuQuyen, result.Data.LyDos);
            Assert.Empty(await InvoicesAsync(seed));
            Assert.Empty(host.Email.Provisional);
        }

        [Fact]
        public async Task CustomerUpload_UsesFallbackStaffOfSameBranch_NotEarlierStaffOfOtherBranch()
        {
            using var host = new Host(_fixture.ConnectionString);
            var other = await SeedAsync();   // tạo trước nên nhân viên có Id nhỏ hơn
            var seed = await SeedAsync();

            await UploadAsync(host, seed, seed.TenantAccount.NguoiDungId, AppLoaiDongHo.Dien);
            await UploadAsync(host, seed, seed.TenantAccount.NguoiDungId, AppLoaiDongHo.Nuoc);

            var invoice = Assert.Single(await InvoicesAsync(seed));
            Assert.All(invoice.LichSuTrangThaiHoaDons, h => Assert.NotEqual(other.Staff.NguoiDungId, h.NguoiThucHienId));
            Assert.Contains(invoice.LichSuTrangThaiHoaDons, h => h.NguoiThucHienId == seed.Staff.NguoiDungId);
            Assert.Empty(await InvoicesAsync(other));
        }

        [Fact]
        public async Task CreateDrafts_ReturnsRealInvoiceId()
        {
            using var host = new Host(_fixture.ConnectionString);
            var seed = await SeedAsync();
            await UploadWithoutTriggerAsync(host, seed, seed.TenantAccount.NguoiDungId, LoaiDongHo.Dien);
            await UploadWithoutTriggerAsync(host, seed, seed.TenantAccount.NguoiDungId, LoaiDongHo.Nuoc);

            await using var scope = host.Provider.CreateAsyncScope();
            var auto = scope.ServiceProvider.GetRequiredService<IAutoInvoiceService>();
            var result = await auto.RunForRoomAsync(seed.Staff.NguoiDungId, AutoInvoiceTrigger.ChotKy, seed.Room.PhongTroId, Thang, Nam);

            Assert.Equal(AutoInvoiceOutcome.DaGuiTamTinh, result.Data!.Outcome);
            var invoice = Assert.Single(await InvoicesAsync(seed));
            Assert.Equal(invoice.HoaDonId, result.Data.HoaDonId);
            Assert.True(result.Data.HoaDonId > 0);
        }

        [Fact]
        public async Task RunningTwice_StillLeavesOneInvoice()
        {
            using var host = new Host(_fixture.ConnectionString);
            var seed = await SeedAsync();
            await UploadWithoutTriggerAsync(host, seed, seed.TenantAccount.NguoiDungId, LoaiDongHo.Dien);
            await UploadWithoutTriggerAsync(host, seed, seed.TenantAccount.NguoiDungId, LoaiDongHo.Nuoc);

            AutoInvoiceRunResult? first;
            AutoInvoiceRunResult? second;
            await using (var scope = host.Provider.CreateAsyncScope())
            {
                first = (await scope.ServiceProvider.GetRequiredService<IAutoInvoiceService>()
                    .RunForRoomAsync(seed.Staff.NguoiDungId, AutoInvoiceTrigger.ChotKy, seed.Room.PhongTroId, Thang, Nam)).Data;
            }
            await using (var scope = host.Provider.CreateAsyncScope())
            {
                second = (await scope.ServiceProvider.GetRequiredService<IAutoInvoiceService>()
                    .RunForRoomAsync(seed.Staff.NguoiDungId, AutoInvoiceTrigger.ChotKy, seed.Room.PhongTroId, Thang, Nam)).Data;
            }

            Assert.Equal(AutoInvoiceOutcome.DaGuiTamTinh, first!.Outcome);
            Assert.Equal(AutoInvoiceOutcome.DungImLang, second!.Outcome);
            Assert.Single(await InvoicesAsync(seed));
            Assert.Single(host.Email.Provisional);
        }

        [Fact]
        public async Task ConcurrentRuns_CreateOneInvoice_WithoutErrors()
        {
            using var host = new Host(_fixture.ConnectionString);
            var seed = await SeedAsync();
            await UploadWithoutTriggerAsync(host, seed, seed.TenantAccount.NguoiDungId, LoaiDongHo.Dien);
            await UploadWithoutTriggerAsync(host, seed, seed.TenantAccount.NguoiDungId, LoaiDongHo.Nuoc);

            async Task<AutoInvoiceRunResult?> RunAsync()
            {
                await using var scope = host.Provider.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IAutoInvoiceService>()
                    .RunForRoomAsync(seed.Staff.NguoiDungId, AutoInvoiceTrigger.XacNhanAnh, seed.Room.PhongTroId, Thang, Nam);
                Assert.True(result.Success);
                return result.Data;
            }

            var results = await Task.WhenAll(RunAsync(), RunAsync());

            Assert.Single(await InvoicesAsync(seed));
            Assert.Equal(1, results.Count(r => r!.Outcome == AutoInvoiceOutcome.DaGuiTamTinh));
            Assert.DoesNotContain(results, r => r!.LyDos.Contains(AutoInvoiceReason.LoiKhiTao));
        }

        [Fact]
        public async Task LowConfidenceImage_CreatesNoInvoice_AndNotifiesStaffAdminAndTenant()
        {
            using var host = new Host(_fixture.ConnectionString);
            host.Ocr.OnProcess = loai => loai == LoaiDongHo.Dien
                ? MeterOcrResult.Readable(120m, 0.89)
                : MeterOcrResult.Readable(60m, 0.95);
            var seed = await SeedAsync();

            await UploadAsync(host, seed, seed.TenantAccount.NguoiDungId, AppLoaiDongHo.Nuoc);
            await UploadAsync(host, seed, seed.TenantAccount.NguoiDungId, AppLoaiDongHo.Dien);

            Assert.Empty(await InvoicesAsync(seed));
            await using var db = _fixture.CreateDbContext();
            var staffNotices = await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seed.Staff.NguoiDungId).ToListAsync();
            Assert.Contains(staffNotices, t => t.TieuDe.Contains("chưa tạo được hóa đơn tạm tính") && t.NoiDung.Contains("0,89 < 0,90"));
            var adminNotices = await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seed.Admin.NguoiDungId).ToListAsync();
            Assert.Contains(adminNotices, t => t.TieuDe.Contains("chưa tạo được hóa đơn tạm tính"));
            var tenantNotices = await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seed.TenantAccount.NguoiDungId).ToListAsync();
            var tenantNotice = Assert.Single(tenantNotices);
            Assert.Equal("Cần chụp lại ảnh đồng hồ", tenantNotice.TieuDe);
            Assert.Empty(host.Email.Provisional);

            // Ảnh chưa bị tự xác nhận.
            Assert.DoesNotContain(await db.AnhChiSoDongHos.AsNoTracking().Where(a => a.DichVuDienNuocCuaPhong.PhongTroId == seed.Room.PhongTroId).ToListAsync(),
                a => a.DuocChonLamChiSoChinhThuc);
        }

        [Fact]
        public async Task ReadingBelowPreviousValue_NotifiesTenantExactlyOnce()
        {
            using var host = new Host(_fixture.ConnectionString);
            var seed = await SeedAsync(withPreviousPeriod: false);
            await using (var seedDb = _fixture.CreateDbContext())
            {
                seedDb.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
                {
                    PhongTroId = seed.Room.PhongTroId, Thang = 9, Nam = 2026,
                    ChiSoDienCu = 100, ChiSoDienMoi = 200, ChiSoNuocCu = 40, ChiSoNuocMoi = 50,
                    DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
                });
                await seedDb.SaveChangesAsync();
            }

            host.Ocr.OnProcess = loai => loai == LoaiDongHo.Dien
                ? MeterOcrResult.Readable(190m, 0.95)   // thấp hơn chỉ số cũ 200
                : MeterOcrResult.Readable(60m, 0.95);
            await UploadAsync(host, seed, seed.TenantAccount.NguoiDungId, AppLoaiDongHo.Nuoc);
            await UploadAsync(host, seed, seed.TenantAccount.NguoiDungId, AppLoaiDongHo.Dien);

            Assert.Empty(await InvoicesAsync(seed));
            await using var db = _fixture.CreateDbContext();
            var tenantNotices = await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seed.TenantAccount.NguoiDungId).ToListAsync();
            var notice = Assert.Single(tenantNotices);
            Assert.Equal("Chỉ số đồng hồ thấp hơn kỳ trước", notice.TieuDe);
            Assert.Contains("190", notice.NoiDung);
            Assert.Contains("200", notice.NoiDung);
        }

        [Fact]
        public async Task StaffManualClose_ThroughDienNuocService_CreatesProvisionalInvoice()
        {
            using var host = new Host(_fixture.ConnectionString);
            var seed = await SeedAsync(withAccount: false);
            await UploadWithoutTriggerAsync(host, seed, seed.Staff.NguoiDungId, LoaiDongHo.Dien);
            await UploadWithoutTriggerAsync(host, seed, seed.Staff.NguoiDungId, LoaiDongHo.Nuoc);

            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var dienNuoc = scope.ServiceProvider.GetRequiredService<IDienNuocService>();
                var result = await dienNuoc.SaveChotDienNuocAsync(new ChotDienNuocReq
                {
                    ChiNhanhId = seed.Branch.ChiNhanhId,
                    Thang = Thang,
                    Nam = Nam,
                    DanhSachPhong = new List<DienNuocPhongReq>
                    {
                        new()
                        {
                            PhongTroId = seed.Room.PhongTroId,
                            DienMode = MeterReadingSubmissionMode.Manual,
                            NuocMode = MeterReadingSubmissionMode.Manual,
                            ChiSoDienMoi = 125m,
                            ChiSoNuocMoi = 58m,
                            LyDoNhapThuCongDien = "Đồng hồ mờ",
                            LyDoNhapThuCongNuoc = "Đồng hồ mờ"
                        }
                    }
                }, seed.Staff.NguoiDungId);
                Assert.True(result.IsSuccess, result.ErrorMessage);
            }

            var invoice = Assert.Single(await InvoicesAsync(seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, invoice.TrangThaiPhatHanh);
            Assert.NotNull(invoice.HanPhanHoi);
            Assert.Single(host.Email.Provisional);
        }
    }
}
