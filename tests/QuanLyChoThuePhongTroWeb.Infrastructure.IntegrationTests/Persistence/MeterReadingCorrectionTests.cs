using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class MeterReadingCorrectionTests
    {
        private readonly PostgreSqlFixture _fixture;

        public MeterReadingCorrectionTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetActiveInvoicesByMeterReadingIds_ReturnsOnlyActiveInvoices_WithIncludedDetails()
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

            var reading = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 150,
                DonGiaDien = 3500,
                ChiSoNuocCu = 50,
                ChiSoNuocMoi = 60,
                DonGiaNuoc = 20000,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            context.DichVuDienNuocCuaPhongs.Add(reading);
            await context.SaveChangesAsync();

            var activeInvoice = new HoaDon
            {
                MaHoaDon = $"INV_ACT_{suffix}",
                HopDongId = contract.HopDongId,
                DichVuDienNuocCuaPhongId = reading.DichVuDienNuocCuaPhongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 2375000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                IsDeleted = false
            };
            var deletedInvoice = new HoaDon
            {
                MaHoaDon = $"INV_DEL_{suffix}",
                HopDongId = contract.HopDongId,
                DichVuDienNuocCuaPhongId = reading.DichVuDienNuocCuaPhongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 2375000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true
            };
            context.HoaDons.AddRange(activeInvoice, deletedInvoice);
            await context.SaveChangesAsync();

            var store = new HoaDonStore(context);
            var results = await store.GetInvoicesByMeterReadingIdsAsync(new[] { reading.DichVuDienNuocCuaPhongId });

            Assert.Equal(2, results.Count);
            Assert.Contains(results, x => x.HoaDonId == activeInvoice.HoaDonId && !x.IsDeleted);
            Assert.Contains(results, x => x.HoaDonId == deletedInvoice.HoaDonId && x.IsDeleted && x.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy);
            Assert.All(results, x => Assert.NotNull(x.HopDong));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task SaveChotDienNuoc_Service_WhenLockedByFinalizedInvoice_RollsBack_And_DoesNotModifyDatabase()
        {
            await using var context = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var staff = new NguoiDung
            {
                TenDangNhap = $"meter_{suffix}",
                MatKhauHash = "test-only",
                Role = Role.NhanVien,
                IsActive = true
            };
            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN {suffix}", MaChiNhanh = $"L{suffix}", DiaChi = "123 Street",
                SoDienThoai = "0900000001", MoTa = "Mo ta CN"
            };
            context.AddRange(staff, branch);
            await context.SaveChangesAsync();

            var assignment = new NhanVienChiNhanh
            {
                NguoiDungId = staff.NguoiDungId,
                ChiNhanhId = branch.ChiNhanhId,
                NguoiPhanCongId = staff.NguoiDungId,
                IsActive = true
            };
            var room = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 2_000_000m,
                DienTich = 20, MoTa = "Mo ta phong"
            };
            var tenant = new NguoiThue
            {
                HoVaTen = $"Tenant {suffix}", SoDienThoai = "0912345678",
                Email = $"tenant_{suffix}@test.com", CCCD = $"081{suffix}00"
            };
            context.AddRange(assignment, room, tenant);
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
            var previousReading = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId, Thang = 8, Nam = 2026,
                ChiSoDienCu = 90, ChiSoDienMoi = 100, ChiSoNuocCu = 45, ChiSoNuocMoi = 50,
                DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            var reading = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 150,
                DonGiaDien = 3500,
                ChiSoNuocCu = 50,
                ChiSoNuocMoi = 60,
                DonGiaNuoc = 20000,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            context.DichVuDienNuocCuaPhongs.AddRange(previousReading, reading);
            await context.SaveChangesAsync();

            var finalizedInvoice = new HoaDon
            {
                MaHoaDon = $"INV_FIN_{suffix}",
                HopDongId = contract.HopDongId,
                DichVuDienNuocCuaPhongId = reading.DichVuDienNuocCuaPhongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 2375000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };
            context.HoaDons.Add(finalizedInvoice);
            await context.SaveChangesAsync();

            try
            {
                var service = new DienNuocService(
                    new DienNuocStore(context),
                    new EfUnitOfWork(context),
                    new EmployeeAccessService(new EmployeeBranchStore(context)),
                    NullLogger<DienNuocService>.Instance,
                    new HoaDonStore(context),
                    new HoaDonCalculatorService());

                var result = await service.SaveChotDienNuocAsync(new ChotDienNuocReq
                {
                    ChiNhanhId = branch.ChiNhanhId,
                    Thang = 9,
                    Nam = 2026,
                    DanhSachPhong = new List<DienNuocPhongReq>
                    {
                        new()
                        {
                            PhongTroId = room.PhongTroId,
                            ChiSoDienCu = 100,
                            ChiSoDienMoi = 180,
                            ChiSoNuocCu = 50,
                            ChiSoNuocMoi = 70
                        }
                    }
                }, staff.NguoiDungId);

                Assert.False(result.IsSuccess);
                context.ChangeTracker.Clear();
                var freshReading = await context.DichVuDienNuocCuaPhongs.AsNoTracking()
                    .SingleAsync(r => r.DichVuDienNuocCuaPhongId == reading.DichVuDienNuocCuaPhongId);
                Assert.Equal(150, freshReading.ChiSoDienMoi);
                Assert.Equal(60, freshReading.ChiSoNuocMoi);
            }
            finally
            {
                context.ChangeTracker.Clear();
                context.HoaDons.RemoveRange(context.HoaDons.Where(x => x.HopDongId == contract.HopDongId));
                context.DichVuDienNuocCuaPhongs.RemoveRange(context.DichVuDienNuocCuaPhongs.Where(x => x.PhongTroId == room.PhongTroId));
                context.NhanVienChiNhanhs.RemoveRange(context.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staff.NguoiDungId));
                context.HopDongs.RemoveRange(context.HopDongs.Where(x => x.HopDongId == contract.HopDongId));
                context.PhongTros.RemoveRange(context.PhongTros.Where(x => x.PhongTroId == room.PhongTroId));
                context.NguoiThues.RemoveRange(context.NguoiThues.Where(x => x.NguoiThueId == tenant.NguoiThueId));
                context.ChiNhanhs.RemoveRange(context.ChiNhanhs.Where(x => x.ChiNhanhId == branch.ChiNhanhId));
                context.NguoiDungs.RemoveRange(context.NguoiDungs.Where(x => x.NguoiDungId == staff.NguoiDungId));
                await context.SaveChangesAsync();
            }
        }
    }
}
