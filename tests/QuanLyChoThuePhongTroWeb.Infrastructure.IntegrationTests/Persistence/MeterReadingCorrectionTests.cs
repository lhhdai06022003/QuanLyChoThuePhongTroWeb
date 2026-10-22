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
                var workflowService = MeterImageStoreTests.CreateWorkflowService(
                    context,
                    storage: new MeterImageStoreTests.FakeStorageService(),
                    ocr: new MeterImageStoreTests.FakeOcrService(),
                    hoaDonStore: new HoaDonStore(context),
                    calculator: new HoaDonCalculatorService());

                var service = new DienNuocService(
                    new DienNuocStore(context),
                    new EmployeeAccessService(new EmployeeBranchStore(context)),
                    workflowService);

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

        [Fact]
        public async Task ApprovePeriodsAsync_WhenOneRoomFails_RollsBackEntireBatch()
        {
            await using var context = _fixture.CreateDbContext();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "P1" };
            var room2 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P2_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "P2" };
            context.PhongTros.AddRange(room1, room2);
            await context.SaveChangesAsync();

            var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin, IsActive = true };
            context.NguoiDungs.Add(admin);
            await context.SaveChangesAsync();

            var period1 = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room1.PhongTroId,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 100,
                ChiSoNuocCu = 50,
                ChiSoNuocMoi = 50,
                DonGiaDien = 3000,
                DonGiaNuoc = 15000,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            var period2 = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room2.PhongTroId,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 100,
                ChiSoNuocCu = 50,
                ChiSoNuocMoi = 50,
                DonGiaDien = 3000,
                DonGiaNuoc = 15000,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            context.DichVuDienNuocCuaPhongs.AddRange(period1, period2);
            await context.SaveChangesAsync();

            var workflowService = MeterImageStoreTests.CreateWorkflowService(
                context,
                storage: new MeterImageStoreTests.FakeStorageService(),
                ocr: new MeterImageStoreTests.FakeOcrService());

            // Room 1 hợp lệ; Room 2 mode Manual nhưng cố tình để trống lý do (hoặc null) -> Fail
            var request = new ApproveMeterPeriodsRequest
            {
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = room1.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 130,
                        ChiSoNuocMoiThuCong = 65,
                        LyDoDienThuCong = "Nhap tay hop le phong 1",
                        LyDoNuocThuCong = "Nhap tay hop le phong 1"
                    },
                    new()
                    {
                        PhongTroId = room2.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 140,
                        ChiSoNuocMoiThuCong = 70,
                        LyDoDienThuCong = "", // Thiếu lý do bắt buộc -> FAIL
                        LyDoNuocThuCong = "Ly do nuoc"
                    }
                }
            };

            var result = await workflowService.ApprovePeriodsAsync(request, admin.NguoiDungId);

            Assert.False(result.Success, "Batch phải thất bại khi một phòng vi phạm quy tắc");

            // Kiểm tra DB: Room 1 KHÔNG ĐƯỢC LƯU thay đổi, phải rollback toàn bộ!
            await using var verifyCtx = _fixture.CreateDbContext();
            var checkPeriod1 = await verifyCtx.DichVuDienNuocCuaPhongs.FindAsync(period1.DichVuDienNuocCuaPhongId);
            Assert.NotNull(checkPeriod1);
            Assert.Equal(TrangThaiGhiNhan.Nhap, checkPeriod1.TrangThaiGhiNhan);
            Assert.Equal(100, checkPeriod1.ChiSoDienMoi);
            Assert.Equal(50, checkPeriod1.ChiSoNuocMoi);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenPeriodDoesNotExist_CreatesNewPeriodAndApproves()
        {
            await using var context = _fixture.CreateDbContext();

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C_{suffix}", DiaChi = "123", SoDienThoai = "0900", MoTa = "Mo ta" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 15, MoTa = "P" };
            context.PhongTros.Add(room);
            await context.SaveChangesAsync();

            var admin = new NguoiDung { TenDangNhap = $"adm_{suffix}", MatKhauHash = "h", Role = Role.Admin, IsActive = true };
            context.NguoiDungs.Add(admin);
            await context.SaveChangesAsync();

            // Thêm dịch vụ điện & nước và gán giá cho chi nhánh
            var dvDien = new DichVu { TenDichVu = "Dịch vụ Điện", DonVi = "kWh", LoaiDichVu = LoaiDichVu.Dien };
            var dvNuoc = new DichVu { TenDichVu = "Dịch vụ Nước", DonVi = "m3", LoaiDichVu = LoaiDichVu.Nuoc };
            context.DichVus.AddRange(dvDien, dvNuoc);
            await context.SaveChangesAsync();

            var dvcnDien = new DichVuChiNhanh { ChiNhanhId = branch.ChiNhanhId, DichVuId = dvDien.DichVuId, GiaDichVu = 3500 };
            var dvcnNuoc = new DichVuChiNhanh { ChiNhanhId = branch.ChiNhanhId, DichVuId = dvNuoc.DichVuId, GiaDichVu = 18000 };
            context.DichVuChiNhanhs.AddRange(dvcnDien, dvcnNuoc);
            await context.SaveChangesAsync();

            // Tạo kỳ cũ tháng 8/2026 với số mới là 100 và 50
            var prevPeriod = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = 8,
                Nam = 2026,
                ChiSoDienCu = 0,
                ChiSoDienMoi = 100,
                DonGiaDien = 3500,
                ChiSoNuocCu = 0,
                ChiSoNuocMoi = 50,
                DonGiaNuoc = 18000,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            context.DichVuDienNuocCuaPhongs.Add(prevPeriod);
            await context.SaveChangesAsync();

            // Lưu ý: tháng 9/2026 CHƯA HỀ TỒN TẠI trong DB!
            var workflowService = MeterImageStoreTests.CreateWorkflowService(
                context,
                storage: new MeterImageStoreTests.FakeStorageService(),
                ocr: new MeterImageStoreTests.FakeOcrService());

            var request = new ApproveMeterPeriodsRequest
            {
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = room.PhongTroId,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 150,
                        ChiSoNuocMoiThuCong = 75,
                        LyDoDienThuCong = "Nhap tay ky moi thang 9",
                        LyDoNuocThuCong = "Nhap tay ky moi thang 9"
                    }
                }
            };

            var result = await workflowService.ApprovePeriodsAsync(request, admin.NguoiDungId);

            Assert.True(result.Success, $"Duyệt tạo kỳ mới phải thành công: {result.Message}");
            Assert.NotNull(result.Data);
            Assert.Single(result.Data.Items);

            var itemRes = result.Data.Items[0];
            Assert.Equal(100, itemRes.ChiSoDienCu);
            Assert.Equal(150, itemRes.ChiSoDienMoi);
            Assert.Equal(50, itemRes.ChiSoNuocCu);
            Assert.Equal(75, itemRes.ChiSoNuocMoi);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, itemRes.TrangThaiGhiNhan);

            // Kiểm tra DB
            await using var verifyCtx = _fixture.CreateDbContext();
            var createdPeriod = await verifyCtx.DichVuDienNuocCuaPhongs
                .FirstOrDefaultAsync(p => p.PhongTroId == room.PhongTroId && p.Thang == 9 && p.Nam == 2026);
            Assert.NotNull(createdPeriod);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, createdPeriod.TrangThaiGhiNhan);
            Assert.Equal(100, createdPeriod.ChiSoDienCu);
            Assert.Equal(150, createdPeriod.ChiSoDienMoi);
            Assert.Equal(50, createdPeriod.ChiSoNuocCu);
            Assert.Equal(75, createdPeriod.ChiSoNuocMoi);
            Assert.Equal(3500, createdPeriod.DonGiaDien);
            Assert.Equal(18000, createdPeriod.DonGiaNuoc);
        }
    }
}
