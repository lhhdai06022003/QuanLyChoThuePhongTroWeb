using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Các trường hợp biên: hóa đơn thay thế tính lại mọi dòng, và ThieuDonGia không chặn điều chỉnh.
    [Collection("PostgreSqlCollection")]
    public class InvoiceAdjustmentEdgeCaseTests
    {
        private readonly PostgreSqlFixture _fixture;

        public InvoiceAdjustmentEdgeCaseTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<InvoiceAdjustmentResult?> AdjustAsync(DeadlineHost host, int actorId, int hoaDonId, TrangThaiPhatHanhHoaDon hienTai, decimal dien, decimal nuoc)
        {
            await using var scope = host.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
            var result = await service.AdjustAsync(actorId, new AdjustInvoiceRequest
            {
                HoaDonId = hoaDonId, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc, TrangThaiPhatHanhHienTai = (int)hienTai
            });
            Assert.True(result.Success, result.Message);
            return result.Data;
        }

        [Fact]
        public async Task AdjustSent_ReplacementRecalculatesEveryLine_WhenFixedServiceAndIncidentChangedSinceFirstCalculation()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            await DeadlineHarness.CreateInvoiceAsync(host, _fixture, seed, TrangThaiPhatHanhHoaDon.DaGui);

            var sent = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, sent.TrangThaiPhatHanh);
            Assert.DoesNotContain(sent.ChiTietHoaDonDichVus, c => c.DichVuId != null);

            int fixedServiceId;
            await using (var db = _fixture.CreateDbContext())
            {
                var suffix = Guid.NewGuid().ToString("N")[..6];
                var wifi = new DichVu { TenDichVu = $"Internet {suffix}", DonVi = "phòng", LoaiDichVu = LoaiDichVu.Khac };
                db.DichVus.Add(wifi);
                await db.SaveChangesAsync();
                fixedServiceId = wifi.DichVuId;
                var link = new DichVuChiNhanh { ChiNhanhId = seed.Branch.ChiNhanhId, DichVuId = wifi.DichVuId, GiaDichVu = 100_000m };
                db.DichVuChiNhanhs.Add(link);
                await db.SaveChangesAsync();
                db.DangKyDichVus.Add(new DangKyDichVu
                {
                    PhongTroId = seed.Room.PhongTroId,
                    DichVuChiNhanhId = link.DichVuChiNhanhId,
                    SoLuong = 1,
                    NgayBatDau = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                });
                db.YeuCauSuCos.Add(new YeuCauSuCo
                {
                    PhongTroId = seed.Room.PhongTroId,
                    NguoiThueId = seed.Tenant.NguoiThueId,
                    TieuDe = "Hong voi nuoc",
                    MoTa = "x",
                    TrangThai = TrangThaiSuCo.DaHoanThanh,
                    CongVaoHoaDon = true,
                    ChiPhiSuaChua = 250_000m,
                    NgayXuLy = new DateTime(2026, 10, 10, 3, 0, 0, DateTimeKind.Utc)
                });
                await db.SaveChangesAsync();
            }

            var data = await AdjustAsync(host, seed.Staff.NguoiDungId, sent.HoaDonId, TrangThaiPhatHanhHoaDon.DaGui, dien: 130m, nuoc: 60m);

            var replacement = (await DeadlineHarness.InvoicesAsync(_fixture, seed)).Single(h => h.HoaDonId == data!.HoaDonIdMoi);
            var lines = replacement.ChiTietHoaDonDichVus.Where(c => !c.IsDeleted).ToList();
            Assert.Contains(lines, c => c.DichVuId == fixedServiceId && c.TongTien > 0);
            Assert.Contains(lines, c => c.TenDichVu.Contains("Hong voi nuoc") && c.TongTien == 250_000m);
            var fixedTotal = lines.Single(c => c.DichVuId == fixedServiceId).TongTien;
            // Điện +10 kWh x 3.500, cộng dịch vụ cố định và sự cố mới phát sinh.
            Assert.Equal(sent.TongTien + 10 * 3500m + fixedTotal + 250_000m, replacement.TongTien);
            Assert.Equal(replacement.TongTien, lines.Sum(c => c.TongTien));
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, replacement.TrangThaiPhatHanh);
        }

        [Fact]
        public async Task AdjustChoDuyet_DoesNotCheckMissingUnitPrice()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateInvoiceAsync(host, _fixture, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);
            await using (var db = _fixture.CreateDbContext())
            {
                var period = await db.DichVuDienNuocCuaPhongs.SingleAsync(p => p.DichVuDienNuocCuaPhongId == invoice.DichVuDienNuocCuaPhongId);
                period.DonGiaDien = 0;
                await db.SaveChangesAsync();
            }

            host.Clock.Now = host.Clock.Now.AddHours(1);
            var data = await AdjustAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, TrangThaiPhatHanhHoaDon.ChoDuyet, dien: 130m, nuoc: 60m);

            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(data!.TongTienMoi, after.TongTien);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);
            Assert.Equal(after.TongTien, after.ChiTietHoaDonDichVus.Where(c => !c.IsDeleted).Sum(c => c.TongTien));
        }
    }
}
