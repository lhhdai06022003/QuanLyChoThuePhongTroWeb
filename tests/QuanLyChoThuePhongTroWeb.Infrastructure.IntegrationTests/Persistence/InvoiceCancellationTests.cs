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
        public async Task GetCancellationBlockers_AccuratelyDetectsPaymentsAndPendingRequests()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN {suffix}",
                MaChiNhanh = $"C{suffix}",
                DiaChi = "123 Street",
                SoDienThoai = "0900000001",
                MoTa = "Mo ta CN"
            };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"P_{suffix}",
                GiaThue = 2000000m,
                DienTich = 20,
                MoTa = "Mo ta phong"
            };
            context.PhongTros.Add(room);

            var tenant = new NguoiThue
            {
                HoVaTen = $"Tenant {suffix}",
                SoDienThoai = "0912345678",
                Email = $"tenant_{suffix}@test.com",
                CCCD = $"079{suffix}00"
            };
            context.NguoiThues.Add(tenant);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TienThuePhong = 2000000m,
                TienCocPhong = 2000000m,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-1),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            // Invoice 1: Clean
            var inv1 = new HoaDon
            {
                MaHoaDon = $"INV_CLEAN_{suffix}",
                HopDongId = contract.HopDongId,
                Thang = 6,
                Nam = 2026,
                TongTien = 2000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };

            // Invoice 2: Has Payment
            var inv2 = new HoaDon
            {
                MaHoaDon = $"INV_PAY_{suffix}",
                HopDongId = contract.HopDongId,
                Thang = 7,
                Nam = 2026,
                TongTien = 2000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };

            // Invoice 3: Has Pending Request
            var inv3 = new HoaDon
            {
                MaHoaDon = $"INV_REQ_{suffix}",
                HopDongId = contract.HopDongId,
                Thang = 8,
                Nam = 2026,
                TongTien = 2000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                IsDeleted = false
            };

            // Invoice 4: Has Pending Proof
            var inv4 = new HoaDon
            {
                MaHoaDon = $"INV_PRF_{suffix}",
                HopDongId = contract.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 2000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                IsDeleted = false
            };

            context.HoaDons.AddRange(inv1, inv2, inv3, inv4);
            await context.SaveChangesAsync();

            // Add payment to inv2
            context.LichSuThanhToans.Add(new LichSuThanhToan
            {
                HoaDonId = inv2.HoaDonId,
                MaGiaoDich = $"TX_{suffix}",
                SoTienThanhToan = 500000m,
                NgayThanhToan = DateTime.UtcNow,
                PhuongThucThanhToan = PhuongThucThanhToan.TienMat
            });

            // Add pending payment request to inv3
            context.YeuCauThanhToanHoaDons.Add(new YeuCauThanhToanHoaDon
            {
                HoaDonId = inv3.HoaDonId,
                MaYeuCau = $"YC_{suffix}_3",
                SoTien = 2000000m,
                NoiDungChuyenKhoan = $"NDCK_{suffix}_3",
                NgayTao = DateTime.UtcNow,
                TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan
            });

            // Add payment request with pending proof to inv4
            var yc4 = new YeuCauThanhToanHoaDon
            {
                HoaDonId = inv4.HoaDonId,
                MaYeuCau = $"YC_{suffix}_4",
                SoTien = 2000000m,
                NoiDungChuyenKhoan = $"NDCK_{suffix}_4",
                NgayTao = DateTime.UtcNow,
                TrangThai = TrangThaiYeuCauThanhToan.DaBaoChuyen
            };
            context.YeuCauThanhToanHoaDons.Add(yc4);
            await context.SaveChangesAsync();

            context.MinhChungThanhToanHoaDons.Add(new MinhChungThanhToanHoaDon
            {
                YeuCauThanhToanHoaDonId = yc4.YeuCauThanhToanHoaDonId,
                HinhAnhUrl = "https://example.com/proof.jpg",
                SoTienKhaiBao = 2000000m,
                NgayChuyenKhaiBao = DateTime.UtcNow,
                TrangThaiDoiChieu = TrangThaiMinhChungThanhToan.ChoXacNhan
            });

            await context.SaveChangesAsync();

            var store = new HoaDonStore(context);

            var blockers1 = await store.GetCancellationBlockersAsync(inv1.HoaDonId);
            Assert.False(blockers1.HasPayment);
            Assert.False(blockers1.HasPendingRequest);
            Assert.False(blockers1.HasPendingProof);

            var blockers2 = await store.GetCancellationBlockersAsync(inv2.HoaDonId);
            Assert.True(blockers2.HasPayment);

            var blockers3 = await store.GetCancellationBlockersAsync(inv3.HoaDonId);
            Assert.True(blockers3.HasPendingRequest);

            var blockers4 = await store.GetCancellationBlockersAsync(inv4.HoaDonId);
            Assert.True(blockers4.HasPendingProof);

            await tx.RollbackAsync();
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
    }
}
