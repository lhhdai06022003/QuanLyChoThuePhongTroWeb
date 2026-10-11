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

            // Email điều chỉnh gửi nền sau khi request trả về, nên chờ có hạn.
            var mails = await host.Email.WaitForProvisionalAsync(2);
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
            var mails = await host.Email.WaitForProvisionalAsync(2);
            Assert.Equal(2, mails.Count);
            Assert.True(mails.Last().Data.LaDieuChinh);
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

        // ---------- Sửa các khoản phí khi điều chỉnh (DB thật) ----------

        private async Task<AdjustResultBox> AdjustWithLinesAsync(
            DeadlineHost host, int actorId, int hoaDonId, IReadOnlyList<AdjustInvoiceLine> lines, decimal? dien = null, decimal? nuoc = null)
        {
            await using var scope = host.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
            var result = await service.AdjustAsync(actorId, new AdjustInvoiceRequest
            {
                HoaDonId = hoaDonId, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc, ChiTiets = lines
            });
            return new AdjustResultBox(result.Success, result.Message, result.ErrorKind, result.Data);
        }

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
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);

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
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
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
        public async Task AdjustProvisional_WithEditedLines_ReplacesLinesInDatabase_AndResends()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
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
            Assert.Equal(host.Clock.Now.AddHours(DeadlineHarness.SoGio), after.HanPhanHoi);

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
            Assert.Contains("Điều chỉnh hóa đơn theo yêu cầu khách thuê", history[2].LyDo);
            Assert.Contains("(sửa 1 dòng, thêm 1 dòng, xóa 1 dòng)", history[3].LyDo);

            // Chỉ số của kỳ không đổi vì không gửi chỉ số mới.
            await using var db = _fixture.CreateDbContext();
            var period = await db.DichVuDienNuocCuaPhongs.AsNoTracking().SingleAsync(p => p.DichVuDienNuocCuaPhongId == invoice.DichVuDienNuocCuaPhongId);
            Assert.Equal((120m, 60m), (period.ChiSoDienMoi, period.ChiSoNuocMoi));

            var mail = (await host.Email.WaitForProvisionalAsync(2)).Last();
            Assert.True(mail.Data.LaDieuChinh);
            Assert.Equal(after.TongTien, mail.Data.TongTien);
            Assert.Equal((100m, 120m, 50m, 60m), (mail.Data.ChiSoDienCu, mail.Data.ChiSoDienMoi, mail.Data.ChiSoNuocCu, mail.Data.ChiSoNuocMoi));
            Assert.Equal(3, mail.Data.ChiTietHoaDons.Count);
        }

        [Fact]
        public async Task AdjustProvisional_ReadingsAndLinesTogether_StoresSubmittedLinesAndNewReadings()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
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
            var mail = (await host.Email.WaitForProvisionalAsync(2)).Last();
            Assert.Equal((100m, 130m), (mail.Data.ChiSoDienCu, mail.Data.ChiSoDienMoi));
        }

        [Fact]
        public async Task AdjustSent_WithEditedLines_CreatesReplacementWithSubmittedLines()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
            host.Clock.Now = invoice.HanPhanHoi!.Value.AddMinutes(1);
            await FinalizeDueAsync(host, seed);
            var sent = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, sent.TrangThaiPhatHanh);
            var info = await InfoAsync(host, seed.Staff.NguoiDungId, sent.HoaDonId);

            var phong = info.ChiTiets.Single(l => l.TenDichVu.StartsWith("Tiền thuê phòng"));
            var dien = info.ChiTiets.Single(l => l.Loai == "dien");
            var dongs = new[]
            {
                Dong(phong, donGia: 2_500_000m, ten: "Tiền thuê phòng (giảm giá theo thỏa thuận)"),
                Dong(dien)
            };

            var result = await AdjustWithLinesAsync(host, seed.Staff.NguoiDungId, sent.HoaDonId, dongs);

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
            Assert.True((await host.Email.WaitForProvisionalAsync(2)).Last().Data.LaDieuChinh);
        }

        [Fact]
        public async Task AdjustProvisional_WithInvalidOrEmptyResult_ChangesNothing()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
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
            Assert.Equal(before.HanPhanHoi, after.HanPhanHoi);
            Assert.Equal(before.TongTien, after.TongTien);
            Assert.Equal(before.LichSuTrangThaiHoaDons.Count, after.LichSuTrangThaiHoaDons.Count);
            Assert.Equal(
                before.ChiTietHoaDonDichVus.OrderBy(l => l.ChiTietHoaDonId).Select(l => (l.ChiTietHoaDonId, l.TenDichVu, l.DonGia, l.SoLuong, l.TongTien)),
                after.ChiTietHoaDonDichVus.OrderBy(l => l.ChiTietHoaDonId).Select(l => (l.ChiTietHoaDonId, l.TenDichVu, l.DonGia, l.SoLuong, l.TongTien)));
            Assert.Single(host.Email.Provisional);
        }
    }
}
