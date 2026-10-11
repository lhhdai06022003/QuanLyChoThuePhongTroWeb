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
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Dùng chung cho test điều chỉnh và tự chốt: service thật trên DB test, chỉ thay OCR, lưu ảnh, email, PDF, SignalR và đồng hồ.
    internal sealed class DeadlineClock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);

        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc), TimeSpan.Zero);
    }

    internal sealed class DeadlineEmail : IEmailService
    {
        public ConcurrentQueue<(string To, ProvisionalInvoiceEmailData Data)> Provisional { get; } = new();
        public ConcurrentQueue<(string To, string MaHoaDon)> Invoice { get; } = new();
        public bool FailInvoiceEmail { get; set; }

        public Task<(bool IsSuccess, string ErrorMessage)> SendProvisionalInvoiceEmailAsync(string toEmail, ProvisionalInvoiceEmailData data)
        {
            Provisional.Enqueue((toEmail, data));
            return Task.FromResult((true, string.Empty));
        }

        // Email tạm tính khi chốt kỳ và khi điều chỉnh gửi nền sau khi request trả về: chờ có hạn cho tới khi đủ số email mong đợi.
        public async Task<List<(string To, ProvisionalInvoiceEmailData Data)>> WaitForProvisionalAsync(int expectedCount, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (Provisional.Count < expectedCount && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            return Provisional.ToList();
        }

        public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
        {
            if (FailInvoiceEmail)
            {
                throw new InvalidOperationException("SMTP không khả dụng");
            }

            Invoice.Enqueue((toEmail, hoaDon.MaHoaDon));
            return Task.FromResult((true, string.Empty));
        }

        public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
            => Task.FromResult((true, string.Empty));
    }

    internal sealed class DeadlineHost : IDisposable
    {
        public ServiceProvider Provider { get; }
        public DeadlineClock Clock { get; } = new();
        public DeadlineEmail Email { get; } = new();

        public DeadlineHost(string connectionString)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString })
                .Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddInfrastructure(config);
            services.AddApplication();
            services.RemoveAll<IMeterImageStorageService>();
            services.AddSingleton<IMeterImageStorageService>(new Storage());
            services.RemoveAll<IMeterOcrService>();
            services.AddSingleton<IMeterOcrService>(new Ocr());
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Email);
            services.RemoveAll<IInvoiceDocumentExporter>();
            services.AddSingleton<IInvoiceDocumentExporter>(new Exporter());
            services.AddScoped<IThongBaoNotifier, Notifier>();
            Provider = services.BuildServiceProvider(validateScopes: true);
        }

        public void Dispose() => Provider.Dispose();

        private sealed class Storage : IMeterImageStorageService
        {
            public Task<MeterImageUploadResult> UploadAsync(UploadFile file, string folder, CancellationToken cancellationToken = default)
            {
                var id = Guid.NewGuid().ToString("N");
                return Task.FromResult(new MeterImageUploadResult($"https://example.test/{id}.jpg", $"deadline_{id}"));
            }

            public Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default)
                => Task.FromResult(new MeterImageReadResult(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 }, "image/jpeg"));

            public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class Ocr : IMeterOcrService
        {
            public Task<MeterOcrResult> ProcessImageAsync(byte[] imageBytes, string contentType, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
                => Task.FromResult(loaiDongHo == LoaiDongHo.Dien ? MeterOcrResult.Readable(120m, 0.95) : MeterOcrResult.Readable(60m, 0.95));
        }

        private sealed class Exporter : IInvoiceDocumentExporter
        {
            public byte[] ExportExcel(HoaDonChiTietRes hoaDon) => new byte[] { 1 };

            public byte[] ExportPdf(HoaDonChiTietRes hoaDon, byte[]? qrBytes, string bankId, string accountNumber, string accountName) => new byte[] { 1, 2, 3 };
        }

        private sealed class Notifier : IThongBaoNotifier
        {
            public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    internal sealed class DeadlineSeed
    {
        public ChiNhanh Branch { get; init; } = null!;
        public PhongTro Room { get; init; } = null!;
        public NguoiThue Tenant { get; init; } = null!;
        public NguoiDung TenantAccount { get; init; } = null!;
        public NguoiDung Staff { get; init; } = null!;
        public NguoiDung Admin { get; init; } = null!;
        public HopDong Contract { get; init; } = null!;
    }

    internal static class DeadlineHarness
    {
        // 15/10/2026 17:00 giờ VN: kỳ hiện tại là 10/2026.
        public const int Thang = 10;
        public const int Nam = 2026;
        public const int SoGio = 48;

        // Chi nhánh có đơn giá điện 3.500 và nước 20.000, hợp đồng từ 01/10/2026, kỳ 09/2026 đã duyệt (điện 100, nước 50).
        public static async Task<DeadlineSeed> SeedAsync(PostgreSqlFixture fixture, DateTime now)
        {
            await using var db = fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var admin = new NguoiDung { TenDangNhap = $"da_{suffix}", MatKhauHash = "x", Role = Role.Admin, IsActive = true };
            var staff = new NguoiDung { TenDangNhap = $"ds_{suffix}", MatKhauHash = "x", Role = Role.NhanVien, IsActive = true };
            var branch = new ChiNhanh
            {
                TenChiNhanh = $"Deadline {suffix}",
                MaChiNhanh = TestCodes.MaChiNhanh("D"),
                DiaChi = "Dia chi",
                SoDienThoai = "0900000088",
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
                NgayPhanCong = now.AddDays(-30),
                IsActive = true
            });

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"D{suffix[..4]}", GiaThue = 3_000_000m, DienTich = 20, MoTa = "x" };
            var tenant = new NguoiThue
            {
                HoVaTen = $"Khach {suffix}",
                SoDienThoai = "0911222333",
                Email = $"khach_{suffix}@test.vn",
                CCCD = $"D{suffix}"
            };
            db.AddRange(room, tenant);
            await db.SaveChangesAsync();

            var account = new NguoiDung { TenDangNhap = $"dk_{suffix}", MatKhauHash = "x", Role = Role.KhachThue, IsActive = true, NguoiThueId = tenant.NguoiThueId };
            db.NguoiDungs.Add(account);

            var contract = new HopDong
            {
                MaHopDong = $"HDD{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc),
                TienCocPhong = 3_000_000m,
                TienThuePhong = 3_000_000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            db.HopDongs.Add(contract);
            db.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId, Thang = 9, Nam = 2026,
                ChiSoDienCu = 90, ChiSoDienMoi = 100, ChiSoNuocCu = 45, ChiSoNuocMoi = 50,
                DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            });

            var dien = new DichVu { TenDichVu = $"Dien {suffix}", DonVi = "kWh", LoaiDichVu = LoaiDichVu.Dien };
            var nuoc = new DichVu { TenDichVu = $"Nuoc {suffix}", DonVi = "m3", LoaiDichVu = LoaiDichVu.Nuoc };
            db.DichVus.AddRange(dien, nuoc);
            await db.SaveChangesAsync();
            db.DichVuChiNhanhs.AddRange(
                new DichVuChiNhanh { ChiNhanhId = branch.ChiNhanhId, DichVuId = dien.DichVuId, GiaDichVu = 3500m },
                new DichVuChiNhanh { ChiNhanhId = branch.ChiNhanhId, DichVuId = nuoc.DichVuId, GiaDichVu = 20000m });
            await db.SaveChangesAsync();

            return new DeadlineSeed
            {
                Branch = branch, Room = room, Tenant = tenant, TenantAccount = account, Staff = staff, Admin = admin, Contract = contract
            };
        }

        // Khách tải ảnh điện rồi nước (OCR 120 và 60): chuỗi tự tạo sinh hóa đơn tạm tính (điện tiêu thụ 20, nước 10).
        public static async Task<HoaDon> CreateProvisionalInvoiceAsync(DeadlineHost host, PostgreSqlFixture fixture, DeadlineSeed seed)
        {
            foreach (var loai in new[] { AppLoaiDongHo.Dien, AppLoaiDongHo.Nuoc })
            {
                await using var scope = host.Provider.CreateAsyncScope();
                var portal = scope.ServiceProvider.GetRequiredService<IMeterImagePortalService>();
                var result = await portal.UploadAsync(new MeterImageUploadCommand
                {
                    PhongTroId = seed.Room.PhongTroId,
                    Thang = Thang,
                    Nam = Nam,
                    LoaiDongHo = loai,
                    File = new UploadFile(new MemoryStream(new byte[] { 1, 2, 3, 4 }), "meter.jpg", "image/jpeg", 4)
                }, seed.TenantAccount.NguoiDungId);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.Message);
                }
            }

            await using var db = fixture.CreateDbContext();
            return await db.HoaDons.AsNoTracking()
                .SingleAsync(h => h.HopDongId == seed.Contract.HopDongId && !h.IsDeleted);
        }

        // DB test dùng chung: hóa đơn tạm tính còn sót từ các lần chạy trước cũng đến hạn khi đồng hồ test chạy tới,
        // làm job xử lý (và báo lỗi) hàng loạt. Bỏ hạn của chúng (thành hóa đơn gửi duyệt tay) để job chỉ còn thấy dữ liệu của test này.
        public static async Task IsolateJobAsync(PostgreSqlFixture fixture, params DeadlineSeed[] keep)
        {
            var keepContractIds = keep.Select(s => s.Contract.HopDongId).ToList();
            await using var db = fixture.CreateDbContext();
            await db.HoaDons
                .Where(h => h.HanPhanHoi != null
                            && h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.ChoDuyet
                            && !keepContractIds.Contains(h.HopDongId))
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.HanPhanHoi, (DateTime?)null));
        }

        public static async Task<List<HoaDon>> InvoicesAsync(PostgreSqlFixture fixture, DeadlineSeed seed, bool includeDeleted = false)
        {
            await using var db = fixture.CreateDbContext();
            return await db.HoaDons
                .AsNoTracking()
                .Include(h => h.LichSuTrangThaiHoaDons)
                .Include(h => h.ChiTietHoaDonDichVus)
                .Where(h => h.HopDongId == seed.Contract.HopDongId && (includeDeleted || !h.IsDeleted))
                .OrderBy(h => h.HoaDonId)
                .ToListAsync();
        }
    }
}
