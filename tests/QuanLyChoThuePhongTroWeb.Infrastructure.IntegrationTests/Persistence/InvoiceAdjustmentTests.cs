using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Điều chỉnh hóa đơn theo trạng thái bằng service thật trên DB test: chờ duyệt (sửa tại chỗ), đã chốt (chỉ Admin),
    // đã gửi (hủy + bản thay thế chờ duyệt, báo khách qua cổng), rollback, quyền và trạng thái đổi giữa chừng.
    [Collection("PostgreSqlCollection")]
    public class InvoiceAdjustmentTests
    {
        private readonly PostgreSqlFixture _fixture;

        public InvoiceAdjustmentTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed record AdjustResultBox(bool Success, string Message, ServiceErrorKind ErrorKind, InvoiceAdjustmentResult? Data);

        // Mô phỏng màn điều chỉnh: trạng thái gửi lên là trạng thái đang lưu trong DB, trừ khi test chỉ định (màn hình cũ).
        private async Task<AdjustResultBox> AdjustAsync(
            DeadlineHost host, int actorId, int hoaDonId, decimal? dien, decimal? nuoc,
            IReadOnlyList<AdjustInvoiceLine>? lines = null, TrangThaiPhatHanhHoaDon? hienTai = null)
        {
            if (hienTai == null)
            {
                await using var db = _fixture.CreateDbContext();
                hienTai = (await db.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == hoaDonId)).TrangThaiPhatHanh;
            }

            await using var scope = host.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
            var result = await service.AdjustAsync(actorId, new AdjustInvoiceRequest
            {
                HoaDonId = hoaDonId, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc, ChiTiets = lines,
                TrangThaiPhatHanhHienTai = (int)hienTai.Value
            });
            return new AdjustResultBox(result.Success, result.Message, result.ErrorKind, result.Data);
        }

        private Task<AdjustResultBox> AdjustWithLinesAsync(
            DeadlineHost host, int actorId, int hoaDonId, IReadOnlyList<AdjustInvoiceLine> lines, decimal? dien = null, decimal? nuoc = null)
            => AdjustAsync(host, actorId, hoaDonId, dien, nuoc, lines);

        private Task<HoaDon> CreateAsync(DeadlineHost host, DeadlineSeed seed, TrangThaiPhatHanhHoaDon target)
            => DeadlineHarness.CreateInvoiceAsync(host, _fixture, seed, target);

        private async Task<List<ThongBao>> TenantNoticesAsync(DeadlineSeed seed)
        {
            await using var db = _fixture.CreateDbContext();
            return await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seed.TenantAccount.NguoiDungId).OrderBy(t => t.Id).ToListAsync();
        }

        [Fact]
        public async Task AdjustChoDuyet_ByStaff_RecalculatesInPlace_StaysChoDuyet_WithoutNotifyingCustomer()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);
            host.Clock.Now = host.Clock.Now.AddHours(1);

            var result = await AdjustAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);

            Assert.True(result.Success, result.Message);
            Assert.Equal("Đã điều chỉnh hóa đơn, hóa đơn vẫn chờ duyệt.", result.Message);
            Assert.False(result.Data!.DaTaoHoaDonThayThe);
            Assert.Equal(invoice.HoaDonId, result.Data.HoaDonIdMoi);
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, result.Data.TrangThaiPhatHanhMoi);

            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);
            Assert.Null(after.HanPhanHoi);
            // Điện tiêu thụ tăng từ 20 lên 30 kWh, đơn giá 3.500.
            Assert.Equal(invoice.TongTien + 10 * 3500m, after.TongTien);
            Assert.Equal(after.TongTien, after.ChiTietHoaDonDichVus.Where(c => !c.IsDeleted).Sum(c => c.TongTien));
            Assert.Equal(result.Data.TongTienMoi, after.TongTien);

            // Khởi tạo nháp, gửi duyệt, rồi hai bước điều chỉnh.
            var history = after.LichSuTrangThaiHoaDons.OrderBy(h => h.LichSuTrangThaiHoaDonId).ToList();
            Assert.Equal(4, history.Count);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, history[2].TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, history[2].TrangThaiPhatHanhMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, history[3].TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, history[3].TrangThaiPhatHanhMoi);
            Assert.All(history.Skip(2), h => Assert.StartsWith("[DIEU_CHINH]", h.LyDo));
            Assert.All(history.Skip(2), h => Assert.Equal(seed.Staff.NguoiDungId, h.NguoiThucHienId));

            await using var db = _fixture.CreateDbContext();
            var period = await db.DichVuDienNuocCuaPhongs.AsNoTracking()
                .SingleAsync(p => p.DichVuDienNuocCuaPhongId == invoice.DichVuDienNuocCuaPhongId);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
            Assert.Equal(130m, period.ChiSoDienMoi);
            var officialDien = await db.AnhChiSoDongHos.AsNoTracking()
                .SingleAsync(a => a.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId && a.LoaiDongHo == LoaiDongHo.Dien && a.DuocChonLamChiSoChinhThuc);
            Assert.Equal(130m, officialDien.GiaTriXacNhan);

            // Khách chưa thấy hóa đơn chưa công bố: không thông báo, không email.
            Assert.Empty(await TenantNoticesAsync(seed));
            Assert.Empty(host.Email.Invoice);
        }

        [Fact]
        public async Task AdjustChoDuyet_WhenCascadeFails_RollsBackEverything()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);
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
            Assert.Equal(before.TongTien, after.TongTien);
            Assert.Equal(before.LichSuTrangThaiHoaDons.Count, after.LichSuTrangThaiHoaDons.Count);

            await using var db = _fixture.CreateDbContext();
            var period = await db.DichVuDienNuocCuaPhongs.AsNoTracking()
                .SingleAsync(p => p.DichVuDienNuocCuaPhongId == invoice.DichVuDienNuocCuaPhongId);
            Assert.Equal(120m, period.ChiSoDienMoi);
            var officialDien = await db.AnhChiSoDongHos.AsNoTracking()
                .SingleAsync(a => a.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId && a.LoaiDongHo == LoaiDongHo.Dien && a.DuocChonLamChiSoChinhThuc);
            Assert.Equal(120m, officialDien.GiaTriXacNhan);
            Assert.Empty(await TenantNoticesAsync(seed));
        }

        [Fact]
        public async Task AdjustDaChot_ByAdmin_StaysDaChot_AndAdminBecomesFinalizer()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.DaChot);

            var result = await AdjustAsync(host, seed.Admin.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);

            Assert.True(result.Success, result.Message);
            Assert.Equal("Đã điều chỉnh hóa đơn đã chốt.", result.Message);
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.DaChot, result.Data!.TrangThaiPhatHanhMoi);
            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(invoice.HoaDonId, after.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, after.TrangThaiPhatHanh);
            Assert.Equal(seed.Admin.NguoiDungId, after.NguoiChotId);
            Assert.NotNull(after.NgayChot);
            Assert.Equal(invoice.TongTien + 10 * 3500m, after.TongTien);
            var adjustments = after.LichSuTrangThaiHoaDons.OrderBy(h => h.LichSuTrangThaiHoaDonId).Skip(3).ToList();
            Assert.Equal(
                new[]
                {
                    (TrangThaiPhatHanhHoaDon.DaChot, TrangThaiPhatHanhHoaDon.Nhap),
                    (TrangThaiPhatHanhHoaDon.Nhap, TrangThaiPhatHanhHoaDon.ChoDuyet),
                    (TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiPhatHanhHoaDon.DaChot)
                },
                adjustments.Select(h => (h.TrangThaiPhatHanhCu!.Value, h.TrangThaiPhatHanhMoi)).ToArray());
            Assert.All(adjustments, h => Assert.StartsWith("[DIEU_CHINH]", h.LyDo));
            Assert.Empty(await TenantNoticesAsync(seed));
        }

        [Fact]
        public async Task AdjustDaChot_ByStaff_IsForbiddenAndChangesNothing()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.DaChot);
            var before = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));

            var result = await AdjustAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Equal("Chỉ Quản trị viên mới điều chỉnh được hóa đơn đã chốt.", result.Message);
            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, after.TrangThaiPhatHanh);
            Assert.Equal(before.TongTien, after.TongTien);
            Assert.Equal(before.NguoiChotId, after.NguoiChotId);
            Assert.Equal(before.LichSuTrangThaiHoaDons.Count, after.LichSuTrangThaiHoaDons.Count);

            await using var scope = host.Provider.CreateAsyncScope();
            var info = await scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>().GetAdjustmentInfoAsync(seed.Staff.NguoiDungId, invoice.HoaDonId);
            Assert.True(info.Success, info.Message);
            Assert.False(info.Data!.CoTheDieuChinh);
            Assert.Equal("Chỉ Quản trị viên mới điều chỉnh được hóa đơn đã chốt.", info.Data.LyDoKhoa);
        }

        [Fact]
        public async Task Adjust_WhenInvoiceFinalizedAfterScreenOpened_IsRejectedAndChangesNothing()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            // Màn hình (của Admin) mở khi hóa đơn còn chờ duyệt; hóa đơn được chốt trước khi Admin bấm lưu.
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.DaChot);
            var before = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));

            var result = await AdjustAsync(host, seed.Admin.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m, hienTai: TrangThaiPhatHanhHoaDon.ChoDuyet);

            Assert.False(result.Success);
            Assert.Equal("Hóa đơn vừa đổi trạng thái, vui lòng tải lại.", result.Message);
            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, after.TrangThaiPhatHanh);
            Assert.Equal(before.TongTien, after.TongTien);
            Assert.Equal(before.LichSuTrangThaiHoaDons.Count, after.LichSuTrangThaiHoaDons.Count);
            await using var db = _fixture.CreateDbContext();
            var period = await db.DichVuDienNuocCuaPhongs.AsNoTracking().SingleAsync(p => p.DichVuDienNuocCuaPhongId == invoice.DichVuDienNuocCuaPhongId);
            Assert.Equal(120m, period.ChiSoDienMoi);
        }

        [Fact]
        public async Task AdjustDaGui_WithoutPayment_CancelsOld_CreatesReplacementChoDuyet_AndNotifiesPortalOnly()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var sent = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.DaGui);
            var noticesBefore = (await TenantNoticesAsync(seed)).Count;
            var invoiceEmailsBefore = host.Email.Invoice.Count;

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
            Assert.Equal("Đã hủy hóa đơn cũ và tạo hóa đơn thay thế chờ duyệt.", result.Message);
            Assert.True(result.Data!.DaTaoHoaDonThayThe);
            Assert.NotEqual(sent.HoaDonId, result.Data.HoaDonIdMoi);
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, result.Data.TrangThaiPhatHanhMoi);

            var all = await DeadlineHarness.InvoicesAsync(_fixture, seed, includeDeleted: true);
            Assert.Equal(2, all.Count);
            var old = all.Single(h => h.HoaDonId == sent.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, old.TrangThaiPhatHanh);
            Assert.True(old.IsDeleted);
            var replacement = all.Single(h => h.HoaDonId == result.Data.HoaDonIdMoi);
            Assert.Equal(sent.MaHoaDon + "-R1", replacement.MaHoaDon);
            Assert.False(replacement.IsDeleted);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, replacement.TrangThaiPhatHanh);
            Assert.Null(replacement.HanPhanHoi);
            Assert.Equal(sent.TongTien + 10 * 3500m, replacement.TongTien);
            Assert.Contains(replacement.LichSuTrangThaiHoaDons, h => h.LyDo != null && h.LyDo.StartsWith("[DIEU_CHINH]") && h.TrangThaiPhatHanhCu == null);
            Assert.Contains(replacement.LichSuTrangThaiHoaDons, h => h.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.ChoDuyet
                && h.LyDo != null && h.LyDo.Contains("Tạo hóa đơn thay thế sau điều chỉnh"));

            await using var db = _fixture.CreateDbContext();
            var cancelledRequest = await db.YeuCauThanhToanHoaDons.AsNoTracking().SingleAsync(y => y.YeuCauThanhToanHoaDonId == requestId);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, cancelledRequest.TrangThai);

            var notices = await TenantNoticesAsync(seed);
            Assert.Equal(noticesBefore + 1, notices.Count);
            var notice = notices.Last();
            Assert.Equal("Hóa đơn tháng 10/2026 đã được điều chỉnh", notice.TieuDe);
            Assert.Equal($"Hóa đơn {sent.MaHoaDon} đã được hủy để điều chỉnh, hóa đơn mới sẽ được gửi sau khi duyệt.", notice.NoiDung);
            Assert.Equal($"/KhachThue/HoaDon?hoaDonId={sent.HoaDonId}", notice.LinkDieuHuong);
            // Chỉ báo qua cổng: không gửi lại email hóa đơn.
            Assert.Equal(invoiceEmailsBefore, host.Email.Invoice.Count);
        }

        [Fact]
        public async Task Adjust_WhenScreenSaysDaChotButInvoiceAlreadySent_IsRejectedAndChangesNothing()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            // Màn hình (của Admin) mở khi hóa đơn đã chốt; hóa đơn được công bố trước khi Admin bấm lưu.
            var sent = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.DaGui);
            var before = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            var noticesBefore = (await TenantNoticesAsync(seed)).Count;

            var result = await AdjustAsync(host, seed.Admin.NguoiDungId, sent.HoaDonId, dien: 130m, nuoc: 60m, hienTai: TrangThaiPhatHanhHoaDon.DaChot);

            Assert.False(result.Success);
            Assert.Equal("Hóa đơn vừa đổi trạng thái, vui lòng tải lại.", result.Message);
            var all = await DeadlineHarness.InvoicesAsync(_fixture, seed, includeDeleted: true);
            var only = Assert.Single(all);
            Assert.Equal(sent.HoaDonId, only.HoaDonId);
            Assert.False(only.IsDeleted);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, only.TrangThaiPhatHanh);
            Assert.Equal(before.TongTien, only.TongTien);
            Assert.Equal(before.LichSuTrangThaiHoaDons.Count, only.LichSuTrangThaiHoaDons.Count);
            Assert.Equal(noticesBefore, (await TenantNoticesAsync(seed)).Count);
            await using var db = _fixture.CreateDbContext();
            var period = await db.DichVuDienNuocCuaPhongs.AsNoTracking().SingleAsync(p => p.DichVuDienNuocCuaPhongId == sent.DichVuDienNuocCuaPhongId);
            Assert.Equal(120m, period.ChiSoDienMoi);
        }

        [Fact]
        public async Task AdjustDaGui_ReadingsOnly_ReplacementKeepsManualLineOfOldInvoice()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var sent = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.DaGui);
            await using (var db = _fixture.CreateDbContext())
            {
                // Nhân viên đã thêm tay một khoản phí vào hóa đơn trước khi gửi.
                db.ChiTietHoaDons.Add(new ChiTietHoaDon { HoaDonId = sent.HoaDonId, TenDichVu = "Phí sửa khóa", DonGia = 50_000m, SoLuong = 1m, TongTien = 50_000m });
                var entity = await db.HoaDons.SingleAsync(h => h.HoaDonId == sent.HoaDonId);
                entity.TongTien += 50_000m;
                await db.SaveChangesAsync();
            }

            var info = await InfoAsync(host, seed.Staff.NguoiDungId, sent.HoaDonId);
            InvoiceMeterLinesPreview preview;
            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var p = await scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>()
                    .PreviewMeterLinesAsync(seed.Staff.NguoiDungId, sent.HoaDonId, 130m, 60m);
                Assert.True(p.Success, p.Message);
                preview = p.Data!;
            }

            // Như màn điều chỉnh: chỉ dòng điện được tính lại theo chỉ số mới, các dòng khác gửi nguyên.
            var dongs = info.ChiTiets.Select(l => l.Loai == "dien"
                    ? new AdjustInvoiceLine { ChiTietHoaDonId = l.ChiTietHoaDonId, TenDichVu = preview.Dien!.TenDichVu, DonGia = preview.Dien.DonGia, SoLuong = preview.Dien.SoLuong }
                    : Dong(l))
                .ToList();

            var result = await AdjustWithLinesAsync(host, seed.Staff.NguoiDungId, sent.HoaDonId, dongs, dien: 130m, nuoc: 60m);

            Assert.True(result.Success, result.Message);
            var replacement = (await DeadlineHarness.InvoicesAsync(_fixture, seed)).Single(h => h.HoaDonId == result.Data!.HoaDonIdMoi);
            var lines = replacement.ChiTietHoaDonDichVus.Where(l => !l.IsDeleted).ToList();
            Assert.Contains(lines, l => l.TenDichVu == "Phí sửa khóa" && l.TongTien == 50_000m);
            Assert.Equal(info.ChiTiets.Count, lines.Count);
            Assert.Contains(lines, l => l.TenDichVu == preview.Dien!.TenDichVu);
            Assert.Equal(lines.Sum(l => l.TongTien), replacement.TongTien);
        }

        [Fact]
        public async Task AdjustDaGui_WithCollectedMoney_IsBlockedAndChangesNothing()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.DaGui);
            await using (var seedDb = _fixture.CreateDbContext())
            {
                var entity = await seedDb.HoaDons.SingleAsync(h => h.HoaDonId == invoice.HoaDonId);
                entity.TrangThaiHoaDon = TrangThaiHoaDon.ThanhToanMotPhan;
                await seedDb.SaveChangesAsync();
            }

            var result = await AdjustAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);

            Assert.False(result.Success);
            Assert.Equal("Hóa đơn đã có khoản thu, không thể điều chỉnh.", result.Message);
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
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);

            var result = await AdjustAsync(host, other.Staff.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(invoice.TongTien, after.TongTien);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);

            await using var scope = host.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
            var info = await service.GetAdjustmentInfoAsync(other.Staff.NguoiDungId, invoice.HoaDonId);
            Assert.False(info.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, info.ErrorKind);
        }

        [Fact]
        public async Task GetAdjustmentInfo_ReportsCurrentReadingsStatus_AndLockReasons()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);

            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
                var info = await service.GetAdjustmentInfoAsync(seed.Staff.NguoiDungId, invoice.HoaDonId);
                Assert.True(info.Success);
                Assert.True(info.Data!.CoTheDieuChinh);
                Assert.Null(info.Data.LyDoKhoa);
                Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, info.Data.TrangThaiPhatHanhValue);
                Assert.Equal(100m, info.Data.ChiSoDienCu);
                Assert.Equal(120m, info.Data.ChiSoDienMoi);
                Assert.Equal(50m, info.Data.ChiSoNuocCu);
                Assert.Equal(60m, info.Data.ChiSoNuocMoi);
                Assert.Null(info.Data.HanPhanHoiUtc);
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

        // ---------- Sửa các khoản phí khi điều chỉnh (DB thật) ----------

        private async Task<InvoiceAdjustmentInfo> InfoAsync(DeadlineHost host, int actorId, int hoaDonId)
        {
            await using var scope = host.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
            var info = await service.GetAdjustmentInfoAsync(actorId, hoaDonId);
            Assert.True(info.Success, info.Message);
            return info.Data!;
        }

        private static AdjustInvoiceLine Dong(InvoiceAdjustmentLine l, decimal? donGia = null, decimal? soLuong = null, string? ten = null)
            => new() { ChiTietHoaDonId = l.ChiTietHoaDonId, TenDichVu = ten ?? l.TenDichVu, DonGia = donGia ?? l.DonGia, SoLuong = soLuong ?? l.SoLuong };

        [Fact]
        public async Task GetAdjustmentInfo_ReportsInvoiceLinesWithKindAndTotal()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);

            var info = await InfoAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId);

            Assert.Equal(invoice.TongTien, info.TongTien);
            Assert.Equal(info.TongTien, info.ChiTiets.Sum(l => l.TongTien));
            Assert.Contains(info.ChiTiets, l => l.Loai == "dien" && l.DonVi == "kWh");
            Assert.Contains(info.ChiTiets, l => l.Loai == "nuoc" && l.DonVi == "m³");
            Assert.Contains(info.ChiTiets, l => l.Loai == "khac" && l.TenDichVu.StartsWith("Tiền thuê phòng"));
            Assert.All(info.ChiTiets, l => Assert.True(l.ChiTietHoaDonId > 0));
            Assert.Equal(info.ChiTiets.Select(l => l.ChiTietHoaDonId).OrderBy(x => x), info.ChiTiets.Select(l => l.ChiTietHoaDonId));
        }

        [Fact]
        public async Task PreviewMeterLines_MatchesWhatAdjustingTheReadingsProduces()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);
            var before = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));

            InvoiceMeterLinesPreview preview;
            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
                var result = await service.PreviewMeterLinesAsync(seed.Staff.NguoiDungId, invoice.HoaDonId, 130m, 60m);
                Assert.True(result.Success, result.Message);
                preview = result.Data!;
            }

            // Xem trước không ghi gì.
            var untouched = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(before.TongTien, untouched.TongTien);
            Assert.Equal(before.LichSuTrangThaiHoaDons.Count, untouched.LichSuTrangThaiHoaDons.Count);
            Assert.Equal(
                before.ChiTietHoaDonDichVus.OrderBy(l => l.ChiTietHoaDonId).Select(l => (l.ChiTietHoaDonId, l.TenDichVu, l.TongTien)),
                untouched.ChiTietHoaDonDichVus.OrderBy(l => l.ChiTietHoaDonId).Select(l => (l.ChiTietHoaDonId, l.TenDichVu, l.TongTien)));

            var adjust = await AdjustAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, dien: 130m, nuoc: 60m);
            Assert.True(adjust.Success, adjust.Message);

            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            var dienLine = after.ChiTietHoaDonDichVus.Single(c => !c.IsDeleted && c.TenDichVu.StartsWith("Tiền điện"));
            Assert.Equal(preview.Dien!.TenDichVu, dienLine.TenDichVu);
            Assert.Equal(preview.Dien.DonGia, dienLine.DonGia);
            Assert.Equal(preview.Dien.SoLuong, dienLine.SoLuong);
            Assert.Equal(preview.Dien.TongTien, dienLine.TongTien);
        }

        [Fact]
        public async Task AdjustChoDuyet_WithEditedLines_ReplacesLinesInDatabase()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);
            var info = await InfoAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId);
            host.Clock.Now = host.Clock.Now.AddHours(1);

            var phong = info.ChiTiets.Single(l => l.TenDichVu.StartsWith("Tiền thuê phòng"));
            var dien = info.ChiTiets.Single(l => l.Loai == "dien");
            var nuoc = info.ChiTiets.Single(l => l.Loai == "nuoc");
            // Giữ nguyên tiền phòng, sửa đơn giá điện, xóa dòng nước, thêm khoản phí mới.
            var dongs = new[]
            {
                Dong(phong),
                Dong(dien, donGia: 4_000m),
                new AdjustInvoiceLine { ChiTietHoaDonId = null, TenDichVu = "Phí giữ xe", DonGia = 100_000m, SoLuong = 2m }
            };

            var result = await AdjustWithLinesAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, dongs);

            Assert.True(result.Success, result.Message);
            Assert.False(result.Data!.DaTaoHoaDonThayThe);
            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(invoice.HoaDonId, after.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);
            Assert.Null(after.HanPhanHoi);

            var lines = after.ChiTietHoaDonDichVus.ToList();
            // Dòng cũ bị thay hẳn (không còn dòng nước, không dòng mồ côi); dòng chưa xóa mềm chỉ còn 3 dòng.
            Assert.Equal(3, lines.Count);
            Assert.DoesNotContain(lines, l => l.ChiTietHoaDonId == nuoc.ChiTietHoaDonId);
            Assert.Equal(phong.TongTien, lines.Single(l => l.TenDichVu == phong.TenDichVu).TongTien);
            // Dòng được thay mới nên có id mới; nhận diện theo tên (không đổi).
            var dienMoi = lines.Single(l => l.TenDichVu == dien.TenDichVu);
            Assert.Equal(4_000m, dienMoi.DonGia);
            Assert.Equal(4_000m * dien.SoLuong, dienMoi.TongTien);
            Assert.Equal(200_000m, lines.Single(l => l.TenDichVu == "Phí giữ xe").TongTien);
            Assert.Equal(lines.Sum(l => l.TongTien), after.TongTien);
            Assert.Equal(result.Data.TongTienMoi, after.TongTien);

            var history = after.LichSuTrangThaiHoaDons.OrderBy(h => h.LichSuTrangThaiHoaDonId).ToList();
            Assert.Equal(4, history.Count);
            Assert.Equal("[DIEU_CHINH] Điều chỉnh hóa đơn", history[2].LyDo);
            Assert.Contains("(sửa 1 dòng, thêm 1 dòng, xóa 1 dòng)", history[3].LyDo);

            // Chỉ số của kỳ không đổi vì không gửi chỉ số mới.
            await using var db = _fixture.CreateDbContext();
            var period = await db.DichVuDienNuocCuaPhongs.AsNoTracking().SingleAsync(p => p.DichVuDienNuocCuaPhongId == invoice.DichVuDienNuocCuaPhongId);
            Assert.Equal((120m, 60m), (period.ChiSoDienMoi, period.ChiSoNuocMoi));
            Assert.Empty(host.Email.Invoice);
        }

        [Fact]
        public async Task AdjustChoDuyet_ReadingsAndLinesTogether_StoresSubmittedLinesAndNewReadings()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);
            var info = await InfoAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId);

            InvoiceMeterLinesPreview preview;
            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
                preview = (await service.PreviewMeterLinesAsync(seed.Staff.NguoiDungId, invoice.HoaDonId, 130m, 60m)).Data!;
            }

            var phong = info.ChiTiets.Single(l => l.TenDichVu.StartsWith("Tiền thuê phòng"));
            var dien = info.ChiTiets.Single(l => l.Loai == "dien");
            var nuoc = info.ChiTiets.Single(l => l.Loai == "nuoc");
            // Như màn điều chỉnh: dòng điện lấy từ xem trước, thêm một khoản phí.
            var dongs = new[]
            {
                Dong(phong),
                new AdjustInvoiceLine { ChiTietHoaDonId = dien.ChiTietHoaDonId, TenDichVu = preview.Dien!.TenDichVu, DonGia = preview.Dien.DonGia, SoLuong = preview.Dien.SoLuong },
                Dong(nuoc),
                new AdjustInvoiceLine { ChiTietHoaDonId = null, TenDichVu = "Phí vệ sinh", DonGia = 50_000m, SoLuong = 1m }
            };

            var result = await AdjustWithLinesAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId, dongs, dien: 130m, nuoc: 60m);

            Assert.True(result.Success, result.Message);
            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            var lines = after.ChiTietHoaDonDichVus.ToList();
            Assert.Equal(4, lines.Count);
            Assert.Equal(preview.Dien.TongTien, lines.Single(l => l.TenDichVu == preview.Dien.TenDichVu).TongTien);
            Assert.Equal(phong.TongTien + preview.Dien.TongTien + nuoc.TongTien + 50_000m, after.TongTien);

            await using var db = _fixture.CreateDbContext();
            var period = await db.DichVuDienNuocCuaPhongs.AsNoTracking().SingleAsync(p => p.DichVuDienNuocCuaPhongId == invoice.DichVuDienNuocCuaPhongId);
            Assert.Equal(130m, period.ChiSoDienMoi);
        }

        [Fact]
        public async Task AdjustDaGui_WithEditedLines_CreatesReplacementWithSubmittedLines()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var sent = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.DaGui);
            var info = await InfoAsync(host, seed.Staff.NguoiDungId, sent.HoaDonId);
            var invoiceEmailsBefore = host.Email.Invoice.Count;

            var phong = info.ChiTiets.Single(l => l.TenDichVu.StartsWith("Tiền thuê phòng"));
            var dien = info.ChiTiets.Single(l => l.Loai == "dien");
            var dongs = new[]
            {
                Dong(phong, donGia: 2_500_000m, ten: "Tiền thuê phòng (giảm giá theo thỏa thuận)"),
                Dong(dien)
            };

            var result = await AdjustWithLinesAsync(host, seed.Staff.NguoiDungId, sent.HoaDonId, dongs);
            // Chỉ báo qua cổng: không gửi email hóa đơn.
            Assert.Equal(invoiceEmailsBefore, host.Email.Invoice.Count);

            Assert.True(result.Success, result.Message);
            Assert.True(result.Data!.DaTaoHoaDonThayThe);
            var all = await DeadlineHarness.InvoicesAsync(_fixture, seed, includeDeleted: true);
            Assert.Equal(2, all.Count);
            var old = all.Single(h => h.HoaDonId == sent.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, old.TrangThaiPhatHanh);
            var replacement = all.Single(h => h.HoaDonId == result.Data.HoaDonIdMoi);
            Assert.EndsWith("-R1", replacement.MaHoaDon);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, replacement.TrangThaiPhatHanh);
            var lines = replacement.ChiTietHoaDonDichVus.Where(l => !l.IsDeleted).ToList();
            Assert.Equal(2, lines.Count);
            Assert.Equal(2_500_000m, lines.Single(l => l.TenDichVu.StartsWith("Tiền thuê phòng")).TongTien);
            Assert.Equal(dien.TongTien, lines.Single(l => l.TenDichVu == dien.TenDichVu).TongTien);
            Assert.Equal(2_500_000m + dien.TongTien, replacement.TongTien);
            // Dòng của hóa đơn cũ vẫn nguyên để đối chiếu.
            Assert.Equal(info.ChiTiets.Count, old.ChiTietHoaDonDichVus.Count(l => !l.IsDeleted));
        }

        [Fact]
        public async Task AdjustChoDuyet_WithInvalidOrEmptyResult_ChangesNothing()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await CreateAsync(host, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);
            var info = await InfoAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId);
            var before = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));

            var zero = await AdjustWithLinesAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId,
                info.ChiTiets.Select(l => Dong(l, donGia: 0m)).ToArray());
            var noChange = await AdjustWithLinesAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId,
                info.ChiTiets.Select(l => Dong(l)).ToArray());
            var badPrice = await AdjustWithLinesAsync(host, seed.Staff.NguoiDungId, invoice.HoaDonId,
                new[] { new AdjustInvoiceLine { TenDichVu = "Phí", DonGia = 10.5m, SoLuong = 1m } });

            Assert.Equal("Tổng tiền hóa đơn phải lớn hơn 0.", zero.Message);
            Assert.Equal("Không có thay đổi", noChange.Message);
            Assert.Equal("Đơn giá phải là số nguyên VND.", badPrice.Message);
            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);
            Assert.Equal(before.TongTien, after.TongTien);
            Assert.Equal(before.LichSuTrangThaiHoaDons.Count, after.LichSuTrangThaiHoaDons.Count);
            Assert.Equal(
                before.ChiTietHoaDonDichVus.OrderBy(l => l.ChiTietHoaDonId).Select(l => (l.ChiTietHoaDonId, l.TenDichVu, l.DonGia, l.SoLuong, l.TongTien)),
                after.ChiTietHoaDonDichVus.OrderBy(l => l.ChiTietHoaDonId).Select(l => (l.ChiTietHoaDonId, l.TenDichVu, l.DonGia, l.SoLuong, l.TongTien)));
        }
    }
}
