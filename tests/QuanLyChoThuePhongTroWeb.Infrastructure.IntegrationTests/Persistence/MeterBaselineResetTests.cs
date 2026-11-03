using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Đổi đồng hồ, quay vòng trên DB test: kỳ lưu số của đồng hồ đang lắp, hóa đơn tính theo số đó,
    // dòng truy thu do nhân viên thêm còn nguyên khi tính lại nháp và khi tạo bản thay thế -R{n}.
    [Collection("PostgreSqlCollection")]
    public class MeterBaselineResetTests
    {
        private const string TruyThuDien = "Truy thu điện đồng hồ cũ";
        private readonly PostgreSqlFixture _fixture;

        public MeterBaselineResetTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static UploadFile Jpeg() => new(new MemoryStream(new byte[] { 1, 2, 3, 4 }), "meter.jpg", "image/jpeg", 4);

        private async Task<int> UploadAsync(DeadlineHost host, DeadlineSeed seed, AppLoaiDongHo loai)
        {
            await using var scope = host.Provider.CreateAsyncScope();
            var portal = scope.ServiceProvider.GetRequiredService<IMeterImagePortalService>();
            var result = await portal.UploadAsync(new MeterImageUploadCommand
            {
                PhongTroId = seed.Room.PhongTroId,
                Thang = DeadlineHarness.Thang,
                Nam = DeadlineHarness.Nam,
                LoaiDongHo = loai,
                File = Jpeg()
            }, seed.Staff.NguoiDungId);
            Assert.True(result.Success, result.Message);
            return result.Data!.AnhChiSoDongHoId;
        }

        private async Task<DichVuDienNuocCuaPhong> PeriodAsync(DeadlineSeed seed)
        {
            await using var db = _fixture.CreateDbContext();
            return await db.DichVuDienNuocCuaPhongs.AsNoTracking()
                .SingleAsync(p => p.PhongTroId == seed.Room.PhongTroId && p.Thang == DeadlineHarness.Thang && p.Nam == DeadlineHarness.Nam);
        }

        private static ChiTietHoaDon DienLine(HoaDon invoice)
            => invoice.ChiTietHoaDonDichVus.Single(l => !l.IsDeleted && InvoiceMeterLine.Classify(null, l.TenDichVu) == InvoiceLineKind.Dien);

        [Fact]
        public async Task SpecExample_ResetToZero_InvoiceBillsNewMeter_AndTruyThuLineSurvivesDraftRecalculation()
        {
            // Cờ tự xác nhận tắt: nhân viên xác nhận, rồi hóa đơn nháp tự tạo.
            using var host = new DeadlineHost(_fixture.ConnectionString, autoConfirmAiImages: false);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);

            var dienImageId = await UploadAsync(host, seed, AppLoaiDongHo.Dien);
            var nuocImageId = await UploadAsync(host, seed, AppLoaiDongHo.Nuoc);
            Assert.Empty(await DeadlineHarness.InvoicesAsync(_fixture, seed));

            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var portal = scope.ServiceProvider.GetRequiredService<IMeterImagePortalService>();
                var confirmNuoc = await portal.ConfirmAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = nuocImageId, GiaTriXacNhan = 60m }, seed.Staff.NguoiDungId);
                Assert.True(confirmNuoc.Success, confirmNuoc.Message);
            }

            Assert.Empty(await DeadlineHarness.InvoicesAsync(_fixture, seed));

            // Kỳ 09 kết thúc ở 100. Đồng hồ cũ chạy tới 145,25, đồng hồ mới đang ở 85,5.
            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var portal = scope.ServiceProvider.GetRequiredService<IMeterImagePortalService>();
                var reset = await portal.DoiMocAsync(
                    new ResetMeterBaselineRequest(dienImageId, MeterBaselineResetType.DoiDongHo, 85.5m, 0m, 145.25m, "Thay đồng hồ"),
                    seed.Staff.NguoiDungId);
                Assert.True(reset.Success, reset.Message);
                Assert.Equal("[DOI_DONG_HO] Chỉ số cũ 100; số đầu 0; số cuối đồng hồ cũ 145,25; phần đồng hồ cũ 45,25 kWh; lý do: Thay đồng hồ", reset.Data!.GhiChuXacNhan);
            }

            var period = await PeriodAsync(seed);
            Assert.Equal((0m, 85.5m), (period.ChiSoDienCu, period.ChiSoDienMoi));
            Assert.Equal((50m, 60m), (period.ChiSoNuocCu, period.ChiSoNuocMoi));

            var invoice = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, invoice.TrangThaiPhatHanh);
            Assert.Equal(85.5m, DienLine(invoice).SoLuong);

            // Nhân viên thêm dòng truy thu 45,25 kWh: tổng điện tính tiền 130,75 kWh.
            await using (var db = _fixture.CreateDbContext())
            {
                db.ChiTietHoaDons.Add(new ChiTietHoaDon { HoaDonId = invoice.HoaDonId, TenDichVu = TruyThuDien, DonGia = 3500m, SoLuong = 45.25m, TongTien = 158_375m });
                var entity = await db.HoaDons.SingleAsync(h => h.HoaDonId == invoice.HoaDonId);
                entity.TongTien += 158_375m;
                await db.SaveChangesAsync();
            }

            // Sửa số điện của đồng hồ mới: hóa đơn nháp tính lại, dòng truy thu còn nguyên.
            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var portal = scope.ServiceProvider.GetRequiredService<IMeterImagePortalService>();
                var correct = await portal.CorrectAsync(new CorrectConfirmedMeterImageRequest(dienImageId, 90m, "Đọc lại số"), seed.Staff.NguoiDungId);
                Assert.True(correct.Success, correct.Message);
            }

            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(90m, DienLine(after).SoLuong);
            var truyThu = Assert.Single(after.ChiTietHoaDonDichVus, l => !l.IsDeleted && l.TenDichVu == TruyThuDien);
            Assert.Equal(45.25m, truyThu.SoLuong);
            Assert.Equal(158_375m, truyThu.TongTien);
            Assert.Equal(after.ChiTietHoaDonDichVus.Where(l => !l.IsDeleted).Sum(l => l.TongTien), after.TongTien);

            // Phía quản lý nhận biết kỳ đã đổi mốc; kỳ trước kết thúc ở 100 còn Cũ của kỳ là 0.
            await using var viewScope = host.Provider.CreateAsyncScope();
            var facts = await viewScope.ServiceProvider.GetRequiredService<IInvoiceViewStore>().GetMeterBaselineFactsAsync(after.HoaDonId);
            Assert.NotNull(facts);
            Assert.Equal(0m, facts!.ChiSoDienCu);
            Assert.Equal(100m, facts.MoiDienKyTruoc);
            Assert.True(MeterBaselineChangePolicy.DaDoiMoc(facts.ChiSoDienCu, facts.MoiDienKyTruoc));
            // Sửa số sau đổi mốc ghi đè ghi chú xác nhận (lựa chọn B2): nhắc vẫn hiện, bằng câu chung.
            Assert.DoesNotContain("[DOI_DONG_HO]", facts.GhiChuDien ?? string.Empty);
            Assert.Equal(
                "Kỳ này có đổi đồng hồ hoặc quay vòng điện (không còn ghi chú xác nhận). Kiểm tra đã thêm dòng truy thu nếu cần thu.",
                MeterBaselineChangePolicy.BuildReminder(LoaiDongHo.Dien, facts.ChiSoDienCu, facts.MoiDienKyTruoc, facts.GhiChuDien));
            Assert.False(MeterBaselineChangePolicy.DaDoiMoc(facts.ChiSoNuocCu, facts.MoiNuocKyTruoc));
            Assert.Equal(3500m, facts.DonGiaDien);
        }

        [Fact]
        public async Task AdjustDaGui_TruyThuLineSentInTheList_IsKeptInReplacementInvoice()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var sent = await DeadlineHarness.CreateInvoiceAsync(host, _fixture, seed, TrangThaiPhatHanhHoaDon.DaGui);

            await using var scope = host.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceAdjustmentService>();
            var info = await service.GetAdjustmentInfoAsync(seed.Staff.NguoiDungId, sent.HoaDonId);
            Assert.True(info.Success, info.Message);

            var lines = info.Data!.ChiTiets
                .Select(l => new AdjustInvoiceLine { ChiTietHoaDonId = l.ChiTietHoaDonId, TenDichVu = l.TenDichVu, DonGia = l.DonGia, SoLuong = l.SoLuong })
                .Append(new AdjustInvoiceLine { TenDichVu = TruyThuDien, DonGia = 3500m, SoLuong = 45.25m })
                .ToList();

            var result = await service.AdjustAsync(seed.Staff.NguoiDungId, new AdjustInvoiceRequest
            {
                HoaDonId = sent.HoaDonId,
                TrangThaiPhatHanhHienTai = (int)TrangThaiPhatHanhHoaDon.DaGui,
                ChiSoDienMoi = null,
                ChiSoNuocMoi = null,
                ChiTiets = lines
            });

            Assert.True(result.Success, result.Message);
            Assert.True(result.Data!.DaTaoHoaDonThayThe);
            var replacement = (await DeadlineHarness.InvoicesAsync(_fixture, seed)).Single(h => h.HoaDonId == result.Data.HoaDonIdMoi);
            var truyThu = Assert.Single(replacement.ChiTietHoaDonDichVus, l => !l.IsDeleted && l.TenDichVu == TruyThuDien);
            Assert.Equal(45.25m, truyThu.SoLuong);
            Assert.Equal(158_375m, truyThu.TongTien);
            Assert.Equal(replacement.ChiTietHoaDonDichVus.Where(l => !l.IsDeleted).Sum(l => l.TongTien), replacement.TongTien);
        }

        [Fact]
        public async Task InvoiceViewStore_DetectsBaselineChange_OnlyWhenOldReadingDiffersFromPreviousNew()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateInvoiceAsync(host, _fixture, seed, TrangThaiPhatHanhHoaDon.ChoDuyet);

            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IInvoiceViewStore>();
                var facts = await store.GetMeterBaselineFactsAsync(invoice.HoaDonId);

                Assert.NotNull(facts);
                Assert.Equal(100m, facts!.ChiSoDienCu);
                Assert.Equal(100m, facts.MoiDienKyTruoc);
                Assert.False(MeterBaselineChangePolicy.DaDoiMoc(facts.ChiSoDienCu, facts.MoiDienKyTruoc));
                Assert.False(MeterBaselineChangePolicy.DaDoiMoc(facts.ChiSoNuocCu, facts.MoiNuocKyTruoc));
            }

            // Kỳ đã xóa mềm không tính là kỳ trước: bỏ kỳ 09 thì kỳ 10 thành kỳ đầu tiên.
            await using (var db = _fixture.CreateDbContext())
            {
                var previous = await db.DichVuDienNuocCuaPhongs.SingleAsync(p => p.PhongTroId == seed.Room.PhongTroId && p.Thang == 9 && p.Nam == 2026);
                previous.IsDeleted = true;
                await db.SaveChangesAsync();
            }

            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IInvoiceViewStore>();
                var facts = await store.GetMeterBaselineFactsAsync(invoice.HoaDonId);

                Assert.NotNull(facts);
                Assert.Null(facts!.MoiDienKyTruoc);
                Assert.False(MeterBaselineChangePolicy.DaDoiMoc(facts.ChiSoDienCu, facts.MoiDienKyTruoc));
            }
        }
    }
}
