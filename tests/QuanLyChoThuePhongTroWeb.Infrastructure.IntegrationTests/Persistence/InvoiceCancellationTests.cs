using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class InvoiceCancellationTests
    {
        private readonly PostgreSqlFixture _fixture;

        public InvoiceCancellationTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetUnpaidInvoices_ReturnsOnlySentInvoicesThatRemainUnpaid()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN {suffix}",
                MaChiNhanh = $"U{suffix}",
                DiaChi = "123 Street",
                SoDienThoai = "0900000002",
                MoTa = "Mo ta CN"
            };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"U_{suffix}",
                GiaThue = 2_000_000m,
                DienTich = 20,
                MoTa = "Mo ta phong"
            };
            var tenant = new NguoiThue
            {
                HoVaTen = $"Tenant {suffix}",
                SoDienThoai = "0912345679",
                Email = $"unpaid_{suffix}@test.com",
                CCCD = $"080{suffix}00"
            };
            context.AddRange(room, tenant);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"UH_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TienThuePhong = 2_000_000m,
                TienCocPhong = 2_000_000m,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-1),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            var statuses = new[]
            {
                TrangThaiPhatHanhHoaDon.Nhap,
                TrangThaiPhatHanhHoaDon.ChoDuyet,
                TrangThaiPhatHanhHoaDon.DaChot,
                TrangThaiPhatHanhHoaDon.DaGui
            };
            for (var index = 0; index < statuses.Length; index++)
            {
                var status = statuses[index];
                context.HoaDons.Add(new HoaDon
                {
                    MaHoaDon = $"UNPAID_{status}_{suffix}",
                    HopDongId = contract.HopDongId,
                    Thang = 6 + index,
                    Nam = 2026,
                    TongTien = 2_000_000m,
                    TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                    TrangThaiPhatHanh = status
                });
            }
            await context.SaveChangesAsync();

            var results = await new HoaDonStore(context)
                .GetUnpaidInvoicesAsync(branch.ChiNhanhId, 0, 2026);

            var invoice = Assert.Single(results);
            Assert.Contains(nameof(TrangThaiPhatHanhHoaDon.DaGui), invoice.MaHoaDon);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task CustomerPortal_OnlyShowsDaGuiAndSentCancelled_NeverDraftOrUnsentCancelled()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N")[..6];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "D", SoDienThoai = "0900000001", MoTa = "M" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 2000000m, DienTich = 20, MoTa = "M" };
            var tenant = new NguoiThue { HoVaTen = $"T_{suffix}", SoDienThoai = "0911223344", Email = $"t_{suffix}@test.com", CCCD = $"079{suffix}11" };
            context.AddRange(room, tenant);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TienThuePhong = 2000000m,
                TienCocPhong = 2000000m,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-3),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            var invNhap = new HoaDon { HopDongId = contract.HopDongId, MaHoaDon = $"HD-NHAP-{suffix}", Thang = 1, Nam = 2026, TongTien = 2000000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan, IsDeleted = false };
            var invChoDuyet = new HoaDon { HopDongId = contract.HopDongId, MaHoaDon = $"HD-CHODUYET-{suffix}", Thang = 2, Nam = 2026, TongTien = 2000000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan, IsDeleted = false };
            var invDaChot = new HoaDon { HopDongId = contract.HopDongId, MaHoaDon = $"HD-DACHOT-{suffix}", Thang = 3, Nam = 2026, TongTien = 2000000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan, IsDeleted = false };
            var invDaGui = new HoaDon { HopDongId = contract.HopDongId, MaHoaDon = $"HD-DAGUI-{suffix}", Thang = 4, Nam = 2026, TongTien = 2000000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan, IsDeleted = false, NgayGui = DateTime.UtcNow.AddDays(-10) };
            var invDaHuySent = new HoaDon { HopDongId = contract.HopDongId, MaHoaDon = $"HD-DAHUYSENT-{suffix}", Thang = 5, Nam = 2026, TongTien = 2000000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan, IsDeleted = true, NgayGui = DateTime.UtcNow.AddDays(-20) };
            var invDaHuyUnsent = new HoaDon { HopDongId = contract.HopDongId, MaHoaDon = $"HD-DAHUYUNSENT-{suffix}", Thang = 6, Nam = 2026, TongTien = 2000000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan, IsDeleted = true, NgayGui = null };

            context.HoaDons.AddRange(invNhap, invChoDuyet, invDaChot, invDaGui, invDaHuySent, invDaHuyUnsent);
            await context.SaveChangesAsync();

            var store = new HoaDonStore(context);
            var customerInvoices = await store.GetInvoicesByContractIdsAsync(new[] { contract.HopDongId });

            // Chỉ có đúng 2 hóa đơn: DaGui và DaHuy có NgayGui != null
            Assert.Equal(2, customerInvoices.Count);
            var codes = customerInvoices.Select(x => x.MaHoaDon).ToList();
            Assert.Contains(invDaGui.MaHoaDon, codes);
            Assert.Contains(invDaHuySent.MaHoaDon, codes);
            Assert.DoesNotContain(invNhap.MaHoaDon, codes);
            Assert.DoesNotContain(invChoDuyet.MaHoaDon, codes);
            Assert.DoesNotContain(invDaChot.MaHoaDon, codes);
            Assert.DoesNotContain(invDaHuyUnsent.MaHoaDon, codes);

            // Kiểm tra CheckHoaDonOwnershipAsync
            Assert.True(await store.CheckHoaDonOwnershipAsync(invDaGui.HoaDonId, tenant.NguoiThueId));
            Assert.True(await store.CheckHoaDonOwnershipAsync(invDaHuySent.HoaDonId, tenant.NguoiThueId));
            Assert.False(await store.CheckHoaDonOwnershipAsync(invNhap.HoaDonId, tenant.NguoiThueId));
            Assert.False(await store.CheckHoaDonOwnershipAsync(invChoDuyet.HoaDonId, tenant.NguoiThueId));
            Assert.False(await store.CheckHoaDonOwnershipAsync(invDaChot.HoaDonId, tenant.NguoiThueId));
            Assert.False(await store.CheckHoaDonOwnershipAsync(invDaHuyUnsent.HoaDonId, tenant.NguoiThueId));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task OverdueQuery_OnlyReturnsDaGui_NeverDraftOrDaChot_UsesCorrectThreshold()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N")[..6];
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "D", SoDienThoai = "0900000001", MoTa = "M" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 2000000m, DienTich = 20, MoTa = "M" };
            var tenant = new NguoiThue { HoVaTen = $"T_{suffix}", SoDienThoai = "0911223344", Email = $"t_{suffix}@test.com", CCCD = $"079{suffix}11" };
            context.AddRange(room, tenant);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TienThuePhong = 2000000m,
                TienCocPhong = 2000000m,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-3),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            var now = DateTime.UtcNow;
            var threshold = now.AddDays(-3);

            // 1. DaGui, Chưa TT, HanThanhToan <= threshold -> ĐƯỢC TÍNH
            var hdOverdueByDueDate = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HD-OVERDUE-DUE-{suffix}",
                Thang = 1,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                HanThanhToan = now.AddDays(-5),
                NgayGui = now.AddDays(-10),
                IsDeleted = false
            };

            // 2. DaGui, Chưa TT, HanThanhToan = null, NgayGui <= threshold -> ĐƯỢC TÍNH
            var hdOverdueBySentDate = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HD-OVERDUE-SENT-{suffix}",
                Thang = 2,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                HanThanhToan = null,
                NgayGui = now.AddDays(-5),
                IsDeleted = false
            };

            // 3. DaChot (chưa gửi), Chưa TT -> KHÔNG ĐƯỢC TÍNH
            var hdDaChot = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HD-DACHOT-OVERDUE-{suffix}",
                Thang = 3,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayTao = now.AddDays(-10),
                IsDeleted = false
            };

            // 4. Nháp -> KHÔNG ĐƯỢC TÍNH
            var hdNhap = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HD-NHAP-OVERDUE-{suffix}",
                Thang = 4,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayTao = now.AddDays(-10),
                IsDeleted = false
            };

            // 5. DaGui, ĐÃ THANH TOÁN -> KHÔNG ĐƯỢC TÍNH
            var hdPaid = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HD-PAID-OVERDUE-{suffix}",
                Thang = 5,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan,
                HanThanhToan = now.AddDays(-5),
                NgayGui = now.AddDays(-10),
                IsDeleted = false
            };

            context.HoaDons.AddRange(hdOverdueByDueDate, hdOverdueBySentDate, hdDaChot, hdNhap, hdPaid);
            await context.SaveChangesAsync();

            var store = new HoaDonStore(context);
            var overdue = await store.GetOverdueInvoicesAsync(threshold);

            var overdueCodes = overdue.Select(x => x.MaHoaDon).ToList();
            Assert.Contains(hdOverdueByDueDate.MaHoaDon, overdueCodes);
            Assert.Contains(hdOverdueBySentDate.MaHoaDon, overdueCodes);
            Assert.DoesNotContain(hdDaChot.MaHoaDon, overdueCodes);
            Assert.DoesNotContain(hdNhap.MaHoaDon, overdueCodes);
            Assert.DoesNotContain(hdPaid.MaHoaDon, overdueCodes);

            await tx.RollbackAsync();
        }
    }
}
