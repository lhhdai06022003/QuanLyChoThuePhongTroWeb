using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Lõi đổi chỉ số kỳ đã duyệt dùng cho "Điều chỉnh và gửi lại" (T16), chạy trong transaction do test mở.
    [Collection("PostgreSqlCollection")]
    public class MeterAdjustmentCoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public MeterAdjustmentCoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<DichVuDienNuocCuaPhong> AddPeriodAsync(DeadlineSeed seed, int thang, TrangThaiGhiNhan status,
            decimal dienCu = 100, decimal dienMoi = 120, decimal nuocCu = 50, decimal nuocMoi = 60, string? note = null)
        {
            await using var db = _fixture.CreateDbContext();
            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = seed.Room.PhongTroId, Thang = thang, Nam = 2026,
                ChiSoDienCu = dienCu, ChiSoDienMoi = dienMoi, ChiSoNuocCu = nuocCu, ChiSoNuocMoi = nuocMoi,
                DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = status, GhiChuDuyet = note
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();
            return period;
        }

        private async Task<ServiceResult<MeterAdjustmentResult>> RunAsync(DeadlineHost host, int actorId, int periodId, decimal dien, decimal nuoc, string lyDo = "Khách yêu cầu")
        {
            await using var scope = host.Provider.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var workflow = scope.ServiceProvider.GetRequiredService<IMeterReadingWorkflowService>();
            await using var tx = await uow.BeginTransactionAsync();
            var result = await workflow.AdjustReadingsInCurrentTransactionAsync(new MeterAdjustmentRequest
            {
                DichVuDienNuocCuaPhongId = periodId,
                ChiSoDienMoi = dien,
                ChiSoNuocMoi = nuoc,
                LyDo = lyDo
            }, actorId);
            if (result.Success)
            {
                await tx.CommitAsync();
            }
            else
            {
                await tx.RollbackAsync();
            }

            return result;
        }

        private async Task<DichVuDienNuocCuaPhong> ReloadAsync(int periodId)
        {
            await using var db = _fixture.CreateDbContext();
            return await db.DichVuDienNuocCuaPhongs.AsNoTracking().SingleAsync(p => p.DichVuDienNuocCuaPhongId == periodId);
        }

        [Fact]
        public async Task WithoutOfficialImage_RecordsManualNoteOnlyForChangedMeter_AndNeverDuplicatesIt()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var period = await AddPeriodAsync(seed, 10, TrangThaiGhiNhan.DaDuyet);

            var first = await RunAsync(host, seed.Staff.NguoiDungId, period.DichVuDienNuocCuaPhongId, dien: 130m, nuoc: 60m);

            Assert.True(first.Success, first.Message);
            Assert.Equal((120m, 130m, 60m, 60m), (first.Data!.ChiSoDienCu, first.Data.ChiSoDienMoi, first.Data.ChiSoNuocCu, first.Data.ChiSoNuocMoi));
            var after = await ReloadAsync(period.DichVuDienNuocCuaPhongId);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, after.TrangThaiGhiNhan);
            Assert.Equal(130m, after.ChiSoDienMoi);
            Assert.Equal(60m, after.ChiSoNuocMoi);
            Assert.Equal("[Điện thủ công: Khách yêu cầu]", after.GhiChuDuyet);
            Assert.Equal(seed.Staff.NguoiDungId, after.NguoiDuyetId);

            var second = await RunAsync(host, seed.Staff.NguoiDungId, period.DichVuDienNuocCuaPhongId, dien: 140m, nuoc: 60m);

            Assert.True(second.Success, second.Message);
            Assert.Equal("[Điện thủ công: Khách yêu cầu]", (await ReloadAsync(period.DichVuDienNuocCuaPhongId)).GhiChuDuyet);
        }

        [Fact]
        public async Task DoesNotBlockAbnormalConsumption()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var period = await AddPeriodAsync(seed, 10, TrangThaiGhiNhan.DaDuyet);

            var result = await RunAsync(host, seed.Staff.NguoiDungId, period.DichVuDienNuocCuaPhongId, dien: 100_000m, nuoc: 60m);

            Assert.True(result.Success, result.Message);
            Assert.Equal(100_000m, (await ReloadAsync(period.DichVuDienNuocCuaPhongId)).ChiSoDienMoi);
        }

        [Fact]
        public async Task Fails_WhenPeriodNotDaDuyet_OrReadingBelowOld_OrReasonMissing()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var draft = await AddPeriodAsync(seed, 12, TrangThaiGhiNhan.Nhap);
            var approved = await AddPeriodAsync(seed, 10, TrangThaiGhiNhan.DaDuyet);

            var notApproved = await RunAsync(host, seed.Staff.NguoiDungId, draft.DichVuDienNuocCuaPhongId, 130m, 60m);
            var belowOld = await RunAsync(host, seed.Staff.NguoiDungId, approved.DichVuDienNuocCuaPhongId, 99m, 60m);
            var belowOldWater = await RunAsync(host, seed.Staff.NguoiDungId, approved.DichVuDienNuocCuaPhongId, 130m, 49m);
            var noReason = await RunAsync(host, seed.Staff.NguoiDungId, approved.DichVuDienNuocCuaPhongId, 130m, 60m, lyDo: "  ");

            Assert.False(notApproved.Success);
            Assert.Equal("Kỳ chỉ số điện nước liên kết chưa được chốt.", notApproved.Message);
            Assert.Equal("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.", belowOld.Message);
            Assert.Equal("Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.", belowOldWater.Message);
            Assert.Equal("Lý do sửa số không được để trống.", noReason.Message);
            Assert.Equal(120m, (await ReloadAsync(approved.DichVuDienNuocCuaPhongId)).ChiSoDienMoi);
        }

        [Fact]
        public async Task Fails_WhenLaterPeriodAlreadyClosed_OrPeriodHasLockedInvoice()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var period = await AddPeriodAsync(seed, 10, TrangThaiGhiNhan.DaDuyet);
            var otherSeed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var locked = await AddPeriodAsync(otherSeed, 10, TrangThaiGhiNhan.DaDuyet);
            await using (var db = _fixture.CreateDbContext())
            {
                db.HoaDons.Add(new HoaDon
                {
                    MaHoaDon = $"HLK{Guid.NewGuid():N}"[..16],
                    HopDongId = otherSeed.Contract.HopDongId,
                    Thang = 10,
                    Nam = 2026,
                    TongTien = 1_000_000m,
                    TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                    DichVuDienNuocCuaPhongId = locked.DichVuDienNuocCuaPhongId
                });
                await db.SaveChangesAsync();
            }

            var lockedResult = await RunAsync(host, otherSeed.Staff.NguoiDungId, locked.DichVuDienNuocCuaPhongId, 130m, 60m);
            Assert.False(lockedResult.Success);
            Assert.Contains("không còn ở trạng thái nháp", lockedResult.Message);

            await AddPeriodAsync(seed, 11, TrangThaiGhiNhan.DaDuyet, 120, 140, 60, 70);
            var laterResult = await RunAsync(host, seed.Staff.NguoiDungId, period.DichVuDienNuocCuaPhongId, 130m, 60m);
            Assert.False(laterResult.Success);
            Assert.Equal("Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể điều chỉnh.", laterResult.Message);
        }

        [Fact]
        public async Task StaffOfAnotherBranch_IsForbidden()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var other = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var period = await AddPeriodAsync(seed, 10, TrangThaiGhiNhan.DaDuyet);

            var result = await RunAsync(host, other.Staff.NguoiDungId, period.DichVuDienNuocCuaPhongId, 130m, 60m);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Equal(120m, (await ReloadAsync(period.DichVuDienNuocCuaPhongId)).ChiSoDienMoi);
        }
    }
}
