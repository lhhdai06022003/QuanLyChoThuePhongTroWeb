using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class InvoiceViewStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public InvoiceViewStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private class SeedData
        {
            public ChiNhanh Branch1 { get; set; } = null!;
            public ChiNhanh Branch2 { get; set; } = null!;
            public HoaDon InvoiceNhap { get; set; } = null!;
            public HoaDon InvoiceChoDuyet { get; set; } = null!;
            public HoaDon InvoiceDaChot { get; set; } = null!;
            public HoaDon InvoiceDaGui { get; set; } = null!;
            public HoaDon InvoiceCancelledA { get; set; } = null!;
            public HoaDon InvoiceCancelledB { get; set; } = null!;
            public HoaDon InvoiceLegacyDeleted { get; set; } = null!;
            public HoaDon InvoiceVnTime { get; set; } = null!;
            public NguoiDung UserStaff { get; set; } = null!;
        }

        private async Task<SeedData> SeedAsync(string prefix)
        {
            await using var context = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..6];

            var branch1 = new ChiNhanh
            {
                TenChiNhanh = $"CN1_{prefix}_{suffix}",
                MaChiNhanh = $"C1{suffix}",
                DiaChi = "123 Duong 1",
                SoDienThoai = "0911111111",
                MoTa = "Branch 1"
            };
            var branch2 = new ChiNhanh
            {
                TenChiNhanh = $"CN2_{prefix}_{suffix}",
                MaChiNhanh = $"C2{suffix}",
                DiaChi = "456 Duong 2",
                SoDienThoai = "0922222222",
                MoTa = "Branch 2"
            };
            context.ChiNhanhs.AddRange(branch1, branch2);
            await context.SaveChangesAsync();

            var tenant = new NguoiThue
            {
                HoVaTen = $"Tenant_{suffix}",
                SoDienThoai = "0933333333",
                Email = $"tenant_{suffix}@test.com",
                CCCD = $"079{suffix}11"
            };
            context.NguoiThues.Add(tenant);

            var staffUser = new NguoiDung
            {
                TenDangNhap = $"staff_{suffix}",
                MatKhauHash = "hash123",
                IsActive = true,
                Role = Role.NhanVien
            };
            context.NguoiDungs.Add(staffUser);
            await context.SaveChangesAsync();

            var contracts = new List<HopDong>();
            HopDong CreateContract(ChiNhanh branch, string code)
            {
                var room = new PhongTro
                {
                    ChiNhanhId = branch.ChiNhanhId,
                    SoPhong = $"P_{code}_{suffix}",
                    GiaThue = 2000000m,
                    DienTich = 20,
                    MoTa = "Room test"
                };
                context.PhongTros.Add(room);
                context.SaveChanges();

                var contract = new HopDong
                {
                    MaHopDong = $"HD_{code}_{suffix}",
                    PhongTroId = room.PhongTroId,
                    NguoiThueId = tenant.NguoiThueId,
                    TienThuePhong = 2000000m,
                    TienCocPhong = 2000000m,
                    ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-2),
                    TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
                };
                context.HopDongs.Add(contract);
                context.SaveChanges();
                return contract;
            }

            var contractNhap = CreateContract(branch1, "NHAP");
            var contractChoDuyet = CreateContract(branch1, "CHODUYET");
            var contractDaChot = CreateContract(branch1, "DACHOT");
            var contractDaGui = CreateContract(branch2, "DAGUI");
            var contractCancelledA = CreateContract(branch1, "HUY_A");
            var contractCancelledB = CreateContract(branch1, "HUY_B");
            var contractLegacy = CreateContract(branch1, "LEGACY");
            var contractVnTime = CreateContract(branch1, "VNTIME");

            // 1. Nhap (Branch 1)
            var invNhap = new HoaDon
            {
                MaHoaDon = $"HD_NHAP_{suffix}",
                HopDongId = contractNhap.HopDongId,
                Thang = 10,
                Nam = 2026,
                TongTien = 2000000m,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                IsDeleted = false,
                NgayTao = DateTime.UtcNow.AddDays(-10)
            };

            // 2. ChoDuyet (Branch 1)
            var invChoDuyet = new HoaDon
            {
                MaHoaDon = $"HD_CHODUYET_{suffix}",
                HopDongId = contractChoDuyet.HopDongId,
                Thang = 10,
                Nam = 2026,
                TongTien = 2100000m,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet,
                IsDeleted = false,
                NgayTao = DateTime.UtcNow.AddDays(-8)
            };

            // 3. DaChot (Branch 1)
            var invDaChot = new HoaDon
            {
                MaHoaDon = $"HD_DACHOT_{suffix}",
                HopDongId = contractDaChot.HopDongId,
                Thang = 10,
                Nam = 2026,
                TongTien = 2200000m,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false,
                NgayTao = DateTime.UtcNow.AddDays(-6)
            };

            // 4. DaGui (Branch 2) - DaThanhToan
            var invDaGui = new HoaDon
            {
                MaHoaDon = $"HD_DAGUI_{suffix}",
                HopDongId = contractDaGui.HopDongId,
                Thang = 10,
                Nam = 2026,
                TongTien = 2500000m,
                TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                IsDeleted = false,
                NgayGui = DateTime.UtcNow.AddDays(-4),
                NgayTao = DateTime.UtcNow.AddDays(-4)
            };

            // 5. Cancelled A (Branch 1) - has 2 status history entries (old & new)
            var invCancelledA = new HoaDon
            {
                MaHoaDon = $"HD_HUY_A_{suffix}",
                HopDongId = contractCancelledA.HopDongId,
                Thang = 10,
                Nam = 2026,
                TongTien = 2300000m,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true,
                NgayTao = DateTime.UtcNow.AddDays(-5)
            };

            // 6. Cancelled B (Branch 1) - no status history entries
            var invCancelledB = new HoaDon
            {
                MaHoaDon = $"HD_HUY_B_{suffix}",
                HopDongId = contractCancelledB.HopDongId,
                Thang = 10,
                Nam = 2026,
                TongTien = 2400000m,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true,
                NgayTao = DateTime.UtcNow.AddDays(-3)
            };

            // 7. Legacy Deleted (Branch 1) - Nhap but IsDeleted = true
            var invLegacyDeleted = new HoaDon
            {
                MaHoaDon = $"HD_LEGACY_{suffix}",
                HopDongId = contractLegacy.HopDongId,
                Thang = 10,
                Nam = 2026,
                TongTien = 1800000m,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                IsDeleted = true,
                NgayTao = DateTime.UtcNow.AddDays(-12)
            };

            // 8. VN Time test invoice (Branch 1) - NgayTao = 2026-09-30T17:30:00Z
            var invVnTime = new HoaDon
            {
                MaHoaDon = $"HD_VNTIME_{suffix}",
                HopDongId = contractVnTime.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 1900000m,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                IsDeleted = false,
                NgayTao = new DateTime(2026, 9, 30, 17, 30, 0, DateTimeKind.Utc)
            };

            context.HoaDons.AddRange(invNhap, invChoDuyet, invDaChot, invDaGui, invCancelledA, invCancelledB, invLegacyDeleted, invVnTime);
            await context.SaveChangesAsync();

            // Status histories for invCancelledA:
            // 1st history (older): by staffUser, reason "cũ"
            var h1 = new LichSuTrangThaiHoaDon
            {
                HoaDonId = invCancelledA.HoaDonId,
                TrangThaiPhatHanhCu = TrangThaiPhatHanhHoaDon.ChoDuyet,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.DaChot,
                TrangThaiThanhToanCu = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiThanhToanMoi = TrangThaiHoaDon.ChuaThanhToan,
                NguoiThucHienId = staffUser.NguoiDungId,
                NgayThucHien = DateTime.UtcNow.AddHours(-2),
                LyDo = "cũ"
            };

            // 2nd history (newer): system (NguoiThucHienId = null), reason "mới", TrangThaiPhatHanhMoi = DaHuy
            var h2 = new LichSuTrangThaiHoaDon
            {
                HoaDonId = invCancelledA.HoaDonId,
                TrangThaiPhatHanhCu = TrangThaiPhatHanhHoaDon.DaChot,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.DaHuy,
                TrangThaiThanhToanCu = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiThanhToanMoi = TrangThaiHoaDon.ChuaThanhToan,
                NguoiThucHienId = null,
                NgayThucHien = DateTime.UtcNow.AddHours(-1),
                LyDo = "mới"
            };
            context.LichSuTrangThaiHoaDons.AddRange(h1, h2);
            await context.SaveChangesAsync();

            return new SeedData
            {
                Branch1 = branch1,
                Branch2 = branch2,
                InvoiceNhap = invNhap,
                InvoiceChoDuyet = invChoDuyet,
                InvoiceDaChot = invDaChot,
                InvoiceDaGui = invDaGui,
                InvoiceCancelledA = invCancelledA,
                InvoiceCancelledB = invCancelledB,
                InvoiceLegacyDeleted = invLegacyDeleted,
                InvoiceVnTime = invVnTime,
                UserStaff = staffUser
            };
        }

        [Fact]
        public async Task List_DefaultFilter_ExcludesCancelledAndLegacyDeleted()
        {
            var data = await SeedAsync("DefFilter");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var req = new DataTableRequest { Start = 0, Length = 50 };
            var filter = new InvoiceListFilter { ChiNhanhId = data.Branch1.ChiNhanhId };

            var res = await store.GetEmployeeListAsync(req, filter, null);
            var returnedIds = res.data.Select(x => x.HoaDonId).ToList();

            Assert.DoesNotContain(data.InvoiceCancelledA.HoaDonId, returnedIds);
            Assert.DoesNotContain(data.InvoiceCancelledB.HoaDonId, returnedIds);
            Assert.DoesNotContain(data.InvoiceLegacyDeleted.HoaDonId, returnedIds);
            Assert.Contains(data.InvoiceNhap.HoaDonId, returnedIds);
            Assert.Contains(data.InvoiceChoDuyet.HoaDonId, returnedIds);
            Assert.Contains(data.InvoiceDaChot.HoaDonId, returnedIds);
        }

        [Fact]
        public async Task List_FilterCancelled_ReturnsOnlyDaHuy_NotLegacyDeleted()
        {
            var data = await SeedAsync("FilterCancelled");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var req = new DataTableRequest { Start = 0, Length = 50 };
            var filter = new InvoiceListFilter
            {
                ChiNhanhId = data.Branch1.ChiNhanhId,
                TrangThaiPhatHanh = (int)TrangThaiPhatHanhHoaDon.DaHuy
            };

            var res = await store.GetEmployeeListAsync(req, filter, null);
            var returnedIds = res.data.Select(x => x.HoaDonId).ToList();

            Assert.Contains(data.InvoiceCancelledA.HoaDonId, returnedIds);
            Assert.Contains(data.InvoiceCancelledB.HoaDonId, returnedIds);
            Assert.DoesNotContain(data.InvoiceLegacyDeleted.HoaDonId, returnedIds);
            Assert.DoesNotContain(data.InvoiceNhap.HoaDonId, returnedIds);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task List_FilterEachPublishState_ReturnsOnlyThatState(int publishState)
        {
            var data = await SeedAsync($"FilterState_{publishState}");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var req = new DataTableRequest { Start = 0, Length = 50 };
            var filter = new InvoiceListFilter
            {
                TrangThaiPhatHanh = publishState
            };

            var res = await store.GetEmployeeListAsync(req, filter, null);
            Assert.All(res.data, hd => Assert.Equal(publishState, hd.TrangThaiPhatHanhValue));
        }

        [Fact]
        public async Task List_PaymentAndPublishFilterCombined()
        {
            var data = await SeedAsync("PayAndPub");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var req = new DataTableRequest { Start = 0, Length = 50 };
            var filter = new InvoiceListFilter
            {
                TrangThaiPhatHanh = (int)TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiThanhToan = (int)TrangThaiHoaDon.DaThanhToan
            };

            var res = await store.GetEmployeeListAsync(req, filter, null);
            var returnedIds = res.data.Select(x => x.HoaDonId).ToList();

            Assert.Contains(data.InvoiceDaGui.HoaDonId, returnedIds);
            Assert.DoesNotContain(data.InvoiceNhap.HoaDonId, returnedIds);
        }

        [Fact]
        public async Task List_AllowedBranches_ScopedToThoseBranches()
        {
            var data = await SeedAsync("AllowedBranches");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var req = new DataTableRequest { Start = 0, Length = 50 };
            var filter = new InvoiceListFilter { ChiNhanhId = 0 }; // All

            var res = await store.GetEmployeeListAsync(req, filter, new[] { data.Branch2.ChiNhanhId });
            var returnedIds = res.data.Select(x => x.HoaDonId).ToList();

            Assert.Contains(data.InvoiceDaGui.HoaDonId, returnedIds); // Branch 2
            Assert.DoesNotContain(data.InvoiceNhap.HoaDonId, returnedIds); // Branch 1
        }

        [Fact]
        public async Task List_NgayTao_IsVietnamTime()
        {
            var data = await SeedAsync("VnTime");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var req = new DataTableRequest { Start = 0, Length = 50 };
            var filter = new InvoiceListFilter { ChiNhanhId = data.Branch1.ChiNhanhId, Thang = 9, Nam = 2026 };

            var res = await store.GetEmployeeListAsync(req, filter, null);
            var target = res.data.FirstOrDefault(x => x.HoaDonId == data.InvoiceVnTime.HoaDonId);

            Assert.NotNull(target);
            Assert.Equal("01/10/2026 00:30", target.NgayTao);
        }

        [Fact]
        public async Task BranchId_Cancelled_Returned_LegacyDeleted_Null()
        {
            var data = await SeedAsync("BranchIdTest");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var branchCancelled = await store.GetViewableBranchIdAsync(data.InvoiceCancelledA.HoaDonId);
            Assert.Equal(data.Branch1.ChiNhanhId, branchCancelled);

            var branchLegacy = await store.GetViewableBranchIdAsync(data.InvoiceLegacyDeleted.HoaDonId);
            Assert.Null(branchLegacy);
        }

        [Fact]
        public async Task Detail_Cancelled_DaHuyTrue_LatestReason()
        {
            var data = await SeedAsync("DetailCancelledA");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var detail = await store.GetDetailAsync(data.InvoiceCancelledA.HoaDonId, includeHistory: true);

            Assert.NotNull(detail);
            Assert.True(detail.DaHuy);
            Assert.Equal("mới", detail.LyDoHuy);
            Assert.Equal("Đã hủy", detail.TrangThaiPhatHanh);
        }

        [Fact]
        public async Task Detail_CancelledWithoutHistory_EmptyReason()
        {
            var data = await SeedAsync("DetailCancelledB");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var detail = await store.GetDetailAsync(data.InvoiceCancelledB.HoaDonId, includeHistory: true);

            Assert.NotNull(detail);
            Assert.True(detail.DaHuy);
            Assert.Equal(string.Empty, detail.LyDoHuy);
        }

        [Fact]
        public async Task Detail_WithHistory_OldestFirst_LabelsAndSystemUser()
        {
            var data = await SeedAsync("DetailHistory");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var detail = await store.GetDetailAsync(data.InvoiceCancelledA.HoaDonId, includeHistory: true);

            Assert.NotNull(detail);
            Assert.Equal(2, detail.LichSuTrangThais.Count);

            // First entry (older)
            var first = detail.LichSuTrangThais[0];
            Assert.Equal(data.UserStaff.TenDangNhap, first.NguoiThucHien);
            Assert.Equal("Chờ duyệt", first.PhatHanhTu);
            Assert.Equal("Đã chốt", first.PhatHanhDen);
            Assert.Equal("cũ", first.LyDo);

            // Last entry (newer)
            var last = detail.LichSuTrangThais[1];
            Assert.Equal("Hệ thống", last.NguoiThucHien);
            Assert.Equal("Đã chốt", last.PhatHanhTu);
            Assert.Equal("Đã hủy", last.PhatHanhDen);
            Assert.Equal("mới", last.LyDo);
        }

        [Fact]
        public async Task Detail_WithoutHistory_EmptyList()
        {
            var data = await SeedAsync("DetailNoHistory");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var detail = await store.GetDetailAsync(data.InvoiceCancelledA.HoaDonId, includeHistory: false);

            Assert.NotNull(detail);
            Assert.Empty(detail.LichSuTrangThais);
            Assert.Equal("mới", detail.LyDoHuy);
            Assert.True(detail.DaHuy);
        }

        [Fact]
        public async Task Detail_LegacyDeleted_Null()
        {
            var data = await SeedAsync("DetailLegacy");
            await using var context = _fixture.CreateDbContext();
            var store = new InvoiceViewStore(context);

            var detail = await store.GetDetailAsync(data.InvoiceLegacyDeleted.HoaDonId, includeHistory: true);
            Assert.Null(detail);
        }
    }
}
