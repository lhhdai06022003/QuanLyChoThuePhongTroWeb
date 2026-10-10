using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // "Điều chỉnh và gửi lại" bằng service thật trên DB test: nhánh hóa đơn tạm tính (6.2), nhánh hóa đơn đã gửi (6.3), rollback và quyền.
    [Collection("PostgreSqlCollection")]
    public class InvoiceAdjustmentTests
    {
        private readonly PostgreSqlFixture _fixture;

        public InvoiceAdjustmentTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<AdjustResultBox> AdjustAsync(DeadlineHost host, int actorId, int hoaDonId, decimal dien, decimal nuoc)
        {
            await using var scope = host.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
            var result = await service.AdjustAsync(actorId, new AdjustInvoiceRequest { HoaDonId = hoaDonId, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc });
            return new AdjustResultBox(result.Success, result.Message, result.ErrorKind, result.Data);
        }

        private sealed record AdjustResultBox(bool Success, string Message, QuanLyChoThuePhongTroWeb.Application.Common.Models.ServiceErrorKind ErrorKind, InvoiceAdjustmentResult? Data);

        [Fact]
        public async Task AdjustProvisional_RecalculatesInPlace_ResetsDeadline_AndNotifiesCustomer()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
            Assert.Equal(host.Clock.Now.AddHours(DeadlineHarness.SoGio), invoice.HanPhanHoi);
            host.Clock.Now = host.Clock.Now.AddHours(1);

            var result = await AdjustAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);

            Assert.True(result.Success, result.Message);
            Assert.False(result.Data!.DaTaoHoaDonThayThe);
            Assert.Equal(invoice.HoaDonId, result.Data.HoaDonIdMoi);
            Assert.Equal(host.Clock.Now.AddHours(DeadlineHarness.SoGio), result.Data.HanPhanHoiUtc);

            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);
            Assert.Equal(host.Clock.Now.AddHours(DeadlineHarness.SoGio), after.HanPhanHoi);
            // Điện tiêu thụ tăng từ 20 lên 30 kWh, đơn giá 3.500.
            Assert.Equal(invoice.TongTien + 10 * 3500m, after.TongTien);
            Assert.Equal(after.TongTien, after.ChiTietHoaDonDichVus.Where(c => !c.IsDeleted).Sum(c => c.TongTien));
            Assert.Equal(result.Data.TongTienMoi, after.TongTien);

            var history = after.LichSuTrangThaiHoaDons.OrderBy(h => h.LichSuTrangThaiHoaDonId).ToList();
            Assert.Equal(4, history.Count);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, history[2].TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, history[2].TrangThaiPhatHanhMoi);
            Assert.StartsWith("[DIEU_CHINH]", history[2].LyDo);
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, history[3].TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, history[3].TrangThaiPhatHanhMoi);
            Assert.StartsWith("[DIEU_CHINH]", history[3].LyDo);
            Assert.All(history.Skip(2), h => Assert.Equal(seed.Staff.NguoiDungId, h.NguoiThucHienId));

            await using var db = _fixture.CreateDbContext();
            var period = await db.DichVuDienNuocCuaPhongs.AsNoTracking()
                .SingleAsync(p => p.DichVuDienNuocCuaPhongId == invoice.DichVuDienNuocCuaPhongId);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
            Assert.Equal(130m, period.ChiSoDienMoi);
            var officialDien = await db.AnhChiSoDongHos.AsNoTracking()
                .SingleAsync(a => a.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId && a.LoaiDongHo == LoaiDongHo.Dien && a.DuocChonLamChiSoChinhThuc);
            Assert.Equal(130m, officialDien.GiaTriXacNhan);

            var notices = await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seed.TenantAccount.NguoiDungId).OrderBy(t => t.Id).ToListAsync();
            Assert.Equal(2, notices.Count);
            Assert.Contains("đã được điều chỉnh", notices[1].TieuDe);
            Assert.Equal($"/KhachThue/HoaDon?hoaDonId={invoice.HoaDonId}", notices[1].LinkDieuHuong);

            var mails = host.Email.Provisional.ToList();
            Assert.Equal(2, mails.Count);
            Assert.True(mails[1].Data.LaDieuChinh);
            Assert.False(mails[0].Data.LaDieuChinh);
            Assert.Equal(130m, mails[1].Data.ChiSoDienMoi);
            Assert.Equal(after.TongTien, mails[1].Data.TongTien);
        }

        [Fact]
        public async Task AdjustProvisional_WhenCascadeFails_RollsBackEverything()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
            var before = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            await using (var seedDb = _fixture.CreateDbContext())
            {
                // Kỳ 11/2026 là bản nháp có chỉ số điện mới (125) thấp hơn số sẽ điều chỉnh (130): cập nhật dây chuyền báo lỗi.
                seedDb.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
                {
                    PhongTroId = seed.Room.PhongTroId, Thang = 11, Nam = 2026,
                    ChiSoDienCu = 120, ChiSoDienMoi = 125, ChiSoNuocCu = 60, ChiSoNuocMoi = 60,
                    DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
                });
                await seedDb.SaveChangesAsync();
            }

            host.Clock.Now = host.Clock.Now.AddHours(1);
            var result = await AdjustAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);

            Assert.False(result.Success);
            Assert.Contains("11/2026", result.Message);
            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);
            Assert.Equal(before.HanPhanHoi, after.HanPhanHoi);
            Assert.Equal(before.TongTien, after.TongTien);
            Assert.Equal(before.LichSuTrangThaiHoaDons.Count, after.LichSuTrangThaiHoaDons.Count);

            await using var db = _fixture.CreateDbContext();
            var period = await db.DichVuDienNuocCuaPhongs.AsNoTracking()
                .SingleAsync(p => p.DichVuDienNuocCuaPhongId == invoice.DichVuDienNuocCuaPhongId);
            Assert.Equal(120m, period.ChiSoDienMoi);
            var officialDien = await db.AnhChiSoDongHos.AsNoTracking()
                .SingleAsync(a => a.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId && a.LoaiDongHo == LoaiDongHo.Dien && a.DuocChonLamChiSoChinhThuc);
            Assert.Equal(120m, officialDien.GiaTriXacNhan);
            Assert.Single(await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seed.TenantAccount.NguoiDungId).ToListAsync());
            Assert.Single(host.Email.Provisional);
        }

        [Fact]
        public async Task AdjustSent_WithoutPayment_CancelsOld_CreatesReplacementAndResends()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
            host.Clock.Now = invoice.HanPhanHoi!.Value.AddMinutes(1);
            await FinalizeDueAsync(host, seed);
            var sent = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, sent.TrangThaiPhatHanh);

            int requestId;
            await using (var seedDb = _fixture.CreateDbContext())
            {
                var request = YeuCauThanhToanHoaDon.Tao(sent.HoaDonId, $"YC{Guid.NewGuid():N}"[..14], sent.TongTien, host.Clock.Now.AddDays(1), seed.Staff.NguoiDungId, host.Clock.Now);
                seedDb.YeuCauThanhToanHoaDons.Add(request);
                await seedDb.SaveChangesAsync();
                requestId = request.YeuCauThanhToanHoaDonId;
            }

            var result = await AdjustAsync(host, seed.Staff.NguoiDungId, sent.HoaDonId, dien: 130m, nuoc: 60m);

            Assert.True(result.Success, result.Message);
            Assert.True(result.Data!.DaTaoHoaDonThayThe);
            Assert.NotEqual(sent.HoaDonId, result.Data.HoaDonIdMoi);

            var all = await DeadlineHarness.InvoicesAsync(_fixture, seed, includeDeleted: true);
            Assert.Equal(2, all.Count);
            var old = all.Single(h => h.HoaDonId == sent.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, old.TrangThaiPhatHanh);
            Assert.True(old.IsDeleted);
            var replacement = all.Single(h => h.HoaDonId == result.Data.HoaDonIdMoi);
            Assert.EndsWith("-R1", replacement.MaHoaDon);
            Assert.False(replacement.IsDeleted);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, replacement.TrangThaiPhatHanh);
            Assert.Equal(host.Clock.Now.AddHours(DeadlineHarness.SoGio), replacement.HanPhanHoi);
            Assert.Equal(sent.TongTien + 10 * 3500m, replacement.TongTien);
            Assert.Contains(replacement.LichSuTrangThaiHoaDons, h => h.LyDo != null && h.LyDo.StartsWith("[DIEU_CHINH]") && h.TrangThaiPhatHanhCu == null);

            await using var db = _fixture.CreateDbContext();
            var cancelledRequest = await db.YeuCauThanhToanHoaDons.AsNoTracking().SingleAsync(y => y.YeuCauThanhToanHoaDonId == requestId);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, cancelledRequest.TrangThai);
            Assert.Equal(2, host.Email.Provisional.Count);
            Assert.True(host.Email.Provisional.Last().Data.LaDieuChinh);
        }

        [Fact]
        public async Task AdjustSent_WithCollectedMoney_IsBlockedAndChangesNothing()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
            host.Clock.Now = invoice.HanPhanHoi!.Value.AddMinutes(1);
            await FinalizeDueAsync(host, seed);
            await using (var seedDb = _fixture.CreateDbContext())
            {
                var entity = await seedDb.HoaDons.SingleAsync(h => h.HoaDonId == invoice.HoaDonId);
                entity.TrangThaiHoaDon = TrangThaiHoaDon.ThanhToanMotPhan;
                await seedDb.SaveChangesAsync();
            }

            var result = await AdjustAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);

            Assert.False(result.Success);
            Assert.Contains("Hóa đơn đã có khoản thu", result.Message);
            var all = await DeadlineHarness.InvoicesAsync(_fixture, seed, includeDeleted: true);
            var only = Assert.Single(all);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, only.TrangThaiPhatHanh);
            Assert.False(only.IsDeleted);
        }

        [Fact]
        public async Task Adjust_ByStaffOfAnotherBranch_IsForbiddenAndChangesNothing()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var other = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);

            var result = await AdjustAsync(host, other.Staff.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);

            Assert.False(result.Success);
            Assert.Equal(QuanLyChoThuePhongTroWeb.Application.Common.Models.ServiceErrorKind.Forbidden, result.ErrorKind);
            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(invoice.TongTien, after.TongTien);
            Assert.Equal(invoice.HanPhanHoi, after.HanPhanHoi);

            await using var scope = host.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
            var info = await service.GetAdjustmentInfoAsync(other.Staff.NguoiDungId, invoice.HoaDonId);
            Assert.False(info.Success);
            Assert.Equal(QuanLyChoThuePhongTroWeb.Application.Common.Models.ServiceErrorKind.Forbidden, info.ErrorKind);
        }

        [Fact]
        public async Task GetAdjustmentInfo_ReportsCurrentReadings_AndLockReasons()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);

            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
                var info = await service.GetAdjustmentInfoAsync(seed.Staff.NguoiDungId, invoice.HoaDonId);
                Assert.True(info.Success);
                Assert.True(info.Data!.CoTheDieuChinh);
                Assert.Null(info.Data.LyDoKhoa);
                Assert.Equal(100m, info.Data.ChiSoDienCu);
                Assert.Equal(120m, info.Data.ChiSoDienMoi);
                Assert.Equal(50m, info.Data.ChiSoNuocCu);
                Assert.Equal(60m, info.Data.ChiSoNuocMoi);
                Assert.Equal(invoice.HanPhanHoi, info.Data.HanPhanHoiUtc);
            }

            await using (var seedDb = _fixture.CreateDbContext())
            {
                // Kỳ 11/2026 đã duyệt khóa kỳ 10/2026.
                seedDb.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
                {
                    PhongTroId = seed.Room.PhongTroId, Thang = 11, Nam = 2026,
                    ChiSoDienCu = 120, ChiSoDienMoi = 140, ChiSoNuocCu = 60, ChiSoNuocMoi = 70,
                    DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
                });
                await seedDb.SaveChangesAsync();
            }

            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
                var info = await service.GetAdjustmentInfoAsync(seed.Staff.NguoiDungId, invoice.HoaDonId);
                Assert.True(info.Success);
                Assert.False(info.Data!.CoTheDieuChinh);
                Assert.Equal("Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể điều chỉnh.", info.Data.LyDoKhoa);
            }
        }

        [Fact]
        public async Task JobAndAdjustment_RunningTogether_LeaveExactlyOneValidActiveInvoice()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
            host.Clock.Now = invoice.HanPhanHoi!.Value.AddMinutes(1);

            await DeadlineHarness.IsolateJobAsync(_fixture, seed);
            var job = FinalizeDueAsync(host, seed);
            var adjust = AdjustAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);
            await Task.WhenAll(job, adjust);

            var active = await DeadlineHarness.InvoicesAsync(_fixture, seed);
            var only = Assert.Single(active);
            Assert.True(
                only.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui
                || (only.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.ChoDuyet && only.HanPhanHoi > host.Clock.Now),
                $"Trạng thái cuối không hợp lệ: {only.TrangThaiPhatHanh}, hạn {only.HanPhanHoi}");
            Assert.Equal(only.TongTien, only.ChiTietHoaDonDichVus.Where(c => !c.IsDeleted).Sum(c => c.TongTien));
        }

        private async Task FinalizeDueAsync(DeadlineHost host, DeadlineSeed seed)
        {
            await DeadlineHarness.IsolateJobAsync(_fixture, seed);
            await using var scope = host.Provider.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IAutoInvoiceFinalizeUseCase>();
            await useCase.ExecuteAsync();
        }
    }
}
