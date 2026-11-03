using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Cascade kỳ sau khi sửa chỉ số (điều chỉnh hóa đơn, duyệt nhiều phòng, đổi mốc) trên DB test:
    // kết quả được lưu đúng, lỗi thì transaction rollback hết (kể cả kỳ nháp tạo mới và việc đổi ảnh chính thức).
    [Collection("PostgreSqlCollection")]
    public class MeterBaselineCascadeTests
    {
        private readonly PostgreSqlFixture _fixture;

        public MeterBaselineCascadeTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<DichVuDienNuocCuaPhong> AddPeriodAsync(int phongTroId, int thang, TrangThaiGhiNhan status,
            decimal dienCu, decimal dienMoi, decimal nuocCu, decimal nuocMoi)
        {
            await using var db = _fixture.CreateDbContext();
            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = phongTroId, Thang = thang, Nam = 2026,
                ChiSoDienCu = dienCu, ChiSoDienMoi = dienMoi, ChiSoNuocCu = nuocCu, ChiSoNuocMoi = nuocMoi,
                DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = status
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();
            return period;
        }

        private async Task<DichVuDienNuocCuaPhong> ReloadAsync(int periodId)
        {
            await using var db = _fixture.CreateDbContext();
            return await db.DichVuDienNuocCuaPhongs.AsNoTracking().SingleAsync(p => p.DichVuDienNuocCuaPhongId == periodId);
        }

        private async Task<DichVuDienNuocCuaPhong?> FindAsync(int phongTroId, int thang)
        {
            await using var db = _fixture.CreateDbContext();
            return await db.DichVuDienNuocCuaPhongs.AsNoTracking()
                .SingleOrDefaultAsync(p => p.PhongTroId == phongTroId && p.Thang == thang && p.Nam == 2026 && !p.IsDeleted);
        }

        // ---------- AdjustReadingsInCurrentTransactionAsync ----------

        private async Task<ServiceResult<MeterAdjustmentResult>> AdjustAsync(DeadlineHost host, int actorId, int periodId, decimal dien, decimal nuoc)
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
                LyDo = "Khách yêu cầu"
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

        [Fact]
        public async Task Adjust_ShiftsLaterDraftChain_AndPersistsIt()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var period = await AddPeriodAsync(seed.Room.PhongTroId, 10, TrangThaiGhiNhan.DaDuyet, 100, 120, 50, 60);
            var next1 = await AddPeriodAsync(seed.Room.PhongTroId, 11, TrangThaiGhiNhan.Nhap, 120, 120, 60, 60);
            var next2 = await AddPeriodAsync(seed.Room.PhongTroId, 12, TrangThaiGhiNhan.Nhap, 120, 140, 60, 60);

            var result = await AdjustAsync(host, seed.Staff.NguoiDungId, period.DichVuDienNuocCuaPhongId, 130m, 60m);

            Assert.True(result.Success, result.Message);
            var p1 = await ReloadAsync(next1.DichVuDienNuocCuaPhongId);
            var p2 = await ReloadAsync(next2.DichVuDienNuocCuaPhongId);
            Assert.Equal((130m, 130m), (p1.ChiSoDienCu, p1.ChiSoDienMoi));
            Assert.Equal((130m, 140m), (p2.ChiSoDienCu, p2.ChiSoDienMoi));
            Assert.Equal((60m, 60m), (p1.ChiSoNuocCu, p1.ChiSoNuocMoi));
        }

        [Fact]
        public async Task Adjust_WhenLaterPeriodChangedElectricBaseline_FailsAndLeavesEverythingUntouched()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var period = await AddPeriodAsync(seed.Room.PhongTroId, 10, TrangThaiGhiNhan.DaDuyet, 100, 120, 50, 60);
            var next = await AddPeriodAsync(seed.Room.PhongTroId, 11, TrangThaiGhiNhan.Nhap, 0, 30, 60, 60);

            var result = await AdjustAsync(host, seed.Staff.NguoiDungId, period.DichVuDienNuocCuaPhongId, 130m, 60m);

            Assert.False(result.Success);
            Assert.Contains("11/2026", result.Message);
            Assert.Contains("đã đổi đồng hồ hoặc quay vòng", result.Message);
            var after = await ReloadAsync(period.DichVuDienNuocCuaPhongId);
            Assert.Equal(120m, after.ChiSoDienMoi);
            var afterNext = await ReloadAsync(next.DichVuDienNuocCuaPhongId);
            Assert.Equal((0m, 30m), (afterNext.ChiSoDienCu, afterNext.ChiSoDienMoi));
        }

        [Fact]
        public async Task Adjust_WaterOnly_IgnoresLaterElectricBaselineChange()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var period = await AddPeriodAsync(seed.Room.PhongTroId, 10, TrangThaiGhiNhan.DaDuyet, 100, 120, 50, 60);
            var next = await AddPeriodAsync(seed.Room.PhongTroId, 11, TrangThaiGhiNhan.Nhap, 0, 30, 60, 60);

            var result = await AdjustAsync(host, seed.Staff.NguoiDungId, period.DichVuDienNuocCuaPhongId, 120m, 65m);

            Assert.True(result.Success, result.Message);
            var afterNext = await ReloadAsync(next.DichVuDienNuocCuaPhongId);
            Assert.Equal((65m, 65m), (afterNext.ChiSoNuocCu, afterNext.ChiSoNuocMoi));
            Assert.Equal((0m, 30m), (afterNext.ChiSoDienCu, afterNext.ChiSoDienMoi));
        }

        // ---------- ApprovePeriodsAsync nhiều phòng ----------

        private static ApproveMeterPeriodItem Manual(int phongTroId, decimal dien, decimal nuoc) => new()
        {
            PhongTroId = phongTroId,
            DienMode = MeterReadingSubmissionMode.Manual,
            ChiSoDienMoiThuCong = dien,
            LyDoDienThuCong = "Đồng hồ điện bị mờ số",
            NuocMode = MeterReadingSubmissionMode.Manual,
            ChiSoNuocMoiThuCong = nuoc,
            LyDoNuocThuCong = "Khách không có nhà, ghi nhận trực tiếp"
        };

        // Phòng thứ hai cùng chi nhánh, kỳ 09 đã có (điện 100, nước 50). Kỳ 10 của cả hai phòng chưa tồn tại, kỳ 11 nháp có sẵn.
        private async Task<(DeadlineSeed Seed, PhongTro Room2)> SeedTwoRoomsAsync(DeadlineHost host, bool room2LaterChangedBaseline)
        {
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            PhongTro room2;
            await using (var db = _fixture.CreateDbContext())
            {
                room2 = new PhongTro
                {
                    ChiNhanhId = seed.Branch.ChiNhanhId,
                    SoPhong = $"K{Guid.NewGuid():N}"[..6],
                    GiaThue = 3_000_000m,
                    DienTich = 20,
                    MoTa = "x"
                };
                db.PhongTros.Add(room2);
                await db.SaveChangesAsync();
            }

            await AddPeriodAsync(room2.PhongTroId, 9, TrangThaiGhiNhan.DaDuyet, 90, 100, 45, 50);
            await AddPeriodAsync(seed.Room.PhongTroId, 11, TrangThaiGhiNhan.Nhap, 100, 100, 50, 50);
            if (room2LaterChangedBaseline)
            {
                await AddPeriodAsync(room2.PhongTroId, 11, TrangThaiGhiNhan.Nhap, 0, 30, 50, 50);
            }
            else
            {
                await AddPeriodAsync(room2.PhongTroId, 11, TrangThaiGhiNhan.Nhap, 100, 100, 50, 50);
            }

            return (seed, room2);
        }

        private async Task<ServiceResult<MeterPeriodsApprovalResult>> ApproveAsync(DeadlineHost host, DeadlineSeed seed, int room2Id, decimal dien2, decimal nuoc2)
        {
            await using var scope = host.Provider.CreateAsyncScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IMeterReadingWorkflowService>();
            return await workflow.ApprovePeriodsAsync(new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem> { Manual(seed.Room.PhongTroId, 160m, 65m), Manual(room2Id, dien2, nuoc2) }
            }, seed.Staff.NguoiDungId);
        }

        [Fact]
        public async Task ApprovePeriods_TwoRooms_ShiftsLaterDraftOfEachRoom_AndPersistsAll()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var (seed, room2) = await SeedTwoRoomsAsync(host, room2LaterChangedBaseline: false);

            var result = await ApproveAsync(host, seed, room2.PhongTroId, 150m, 55m);

            Assert.True(result.Success, result.Message);
            var r1p10 = await FindAsync(seed.Room.PhongTroId, 10);
            var r1p11 = await FindAsync(seed.Room.PhongTroId, 11);
            var r2p10 = await FindAsync(room2.PhongTroId, 10);
            var r2p11 = await FindAsync(room2.PhongTroId, 11);
            Assert.Equal((TrangThaiGhiNhan.DaDuyet, 100m, 160m), (r1p10!.TrangThaiGhiNhan, r1p10.ChiSoDienCu, r1p10.ChiSoDienMoi));
            Assert.Equal((160m, 160m, 65m, 65m), (r1p11!.ChiSoDienCu, r1p11.ChiSoDienMoi, r1p11.ChiSoNuocCu, r1p11.ChiSoNuocMoi));
            Assert.Equal((100m, 150m), (r2p10!.ChiSoDienCu, r2p10.ChiSoDienMoi));
            Assert.Equal((150m, 150m, 55m, 55m), (r2p11!.ChiSoDienCu, r2p11.ChiSoDienMoi, r2p11.ChiSoNuocCu, r2p11.ChiSoNuocMoi));
        }

        [Fact]
        public async Task ApprovePeriods_SecondRoomLaterPeriodChangedBaseline_RollsBackWholeBatchIncludingCreatedPeriods()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var (seed, room2) = await SeedTwoRoomsAsync(host, room2LaterChangedBaseline: true);

            var result = await ApproveAsync(host, seed, room2.PhongTroId, 150m, 55m);

            Assert.False(result.Success);
            Assert.Contains("Phòng", result.Message);
            Assert.Contains("11/2026", result.Message);
            Assert.Contains("đã đổi đồng hồ hoặc quay vòng", result.Message);

            // Không có kỳ 10 nào được tạo (kỳ nháp tạo giữa chừng cũng rollback) và kỳ 11 của phòng thứ nhất không bị dịch.
            Assert.Null(await FindAsync(seed.Room.PhongTroId, 10));
            Assert.Null(await FindAsync(room2.PhongTroId, 10));
            var r1p11 = await FindAsync(seed.Room.PhongTroId, 11);
            Assert.Equal((100m, 100m), (r1p11!.ChiSoDienCu, r1p11.ChiSoDienMoi));
            var r2p11 = await FindAsync(room2.PhongTroId, 11);
            Assert.Equal((0m, 30m), (r2p11!.ChiSoDienCu, r2p11.ChiSoDienMoi));
        }

        // ---------- ResetMeterBaseline: đổi ảnh chính thức + cascade trong một transaction ----------

        private static UploadFile Jpeg() => new(new MemoryStream(new byte[] { 1, 2, 3, 4 }), "meter.jpg", "image/jpeg", 4);

        private async Task<int> UploadDienAsync(DeadlineHost host, DeadlineSeed seed)
        {
            await using var scope = host.Provider.CreateAsyncScope();
            var portal = scope.ServiceProvider.GetRequiredService<IMeterImagePortalService>();
            var result = await portal.UploadAsync(new MeterImageUploadCommand
            {
                PhongTroId = seed.Room.PhongTroId,
                Thang = DeadlineHarness.Thang,
                Nam = DeadlineHarness.Nam,
                LoaiDongHo = AppLoaiDongHo.Dien,
                File = Jpeg()
            }, seed.Staff.NguoiDungId);
            Assert.True(result.Success, result.Message);
            return result.Data!.AnhChiSoDongHoId;
        }

        // Ảnh A đã xác nhận 120 (chính thức), ảnh B mới tải (DocDuoc, chưa chính thức), hai kỳ sau nháp theo Mới 120.
        private async Task<(DeadlineHost Host, DeadlineSeed Seed, int ImageA, int ImageB, int PeriodId, int Next1Id)> SeedResetAsync(bool secondLaterChangedBaseline)
        {
            var host = new DeadlineHost(_fixture.ConnectionString, autoConfirmAiImages: false);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var imageA = await UploadDienAsync(host, seed);
            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var portal = scope.ServiceProvider.GetRequiredService<IMeterImagePortalService>();
                var confirm = await portal.ConfirmAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = imageA, GiaTriXacNhan = 120m }, seed.Staff.NguoiDungId);
                Assert.True(confirm.Success, confirm.Message);
            }

            var imageB = await UploadDienAsync(host, seed);
            var period = await FindAsync(seed.Room.PhongTroId, 10);
            var next1 = await AddPeriodAsync(seed.Room.PhongTroId, 11, TrangThaiGhiNhan.Nhap, 120, 120, 50, 50);
            if (secondLaterChangedBaseline)
            {
                await AddPeriodAsync(seed.Room.PhongTroId, 12, TrangThaiGhiNhan.Nhap, 0, 30, 50, 50);
            }
            else
            {
                await AddPeriodAsync(seed.Room.PhongTroId, 12, TrangThaiGhiNhan.Nhap, 120, 120, 50, 50);
            }

            return (host, seed, imageA, imageB, period!.DichVuDienNuocCuaPhongId, next1.DichVuDienNuocCuaPhongId);
        }

        [Fact]
        public async Task ResetMeterBaseline_ShiftsLaterChain_SwapsOfficialImage_AndWritesTrueOldNumber()
        {
            var (host, seed, imageA, imageB, periodId, next1Id) = await SeedResetAsync(secondLaterChangedBaseline: false);
            using var hostScope = host;

            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var portal = scope.ServiceProvider.GetRequiredService<IMeterImagePortalService>();
                var reset = await portal.DoiMocAsync(
                    new ResetMeterBaselineRequest(imageB, MeterBaselineResetType.DoiDongHo, 85.5m, 0m, 145.25m, "Thay đồng hồ"), seed.Staff.NguoiDungId);
                Assert.True(reset.Success, reset.Message);
                Assert.StartsWith("[DOI_DONG_HO] Chỉ số cũ 100; số đầu 0; số cuối đồng hồ cũ 145,25; phần đồng hồ cũ 45,25 kWh;", reset.Data!.GhiChuXacNhan);
            }

            var period = await ReloadAsync(periodId);
            Assert.Equal((0m, 85.5m), (period.ChiSoDienCu, period.ChiSoDienMoi));
            var next1 = await ReloadAsync(next1Id);
            Assert.Equal((85.5m, 85.5m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            var next2 = await FindAsync(seed.Room.PhongTroId, 12);
            Assert.Equal((85.5m, 85.5m), (next2!.ChiSoDienCu, next2.ChiSoDienMoi));

            await using var db = _fixture.CreateDbContext();
            var a = await db.AnhChiSoDongHos.AsNoTracking().SingleAsync(x => x.AnhChiSoDongHoId == imageA);
            var b = await db.AnhChiSoDongHos.AsNoTracking().SingleAsync(x => x.AnhChiSoDongHoId == imageB);
            Assert.False(a.DuocChonLamChiSoChinhThuc);
            Assert.True(b.DuocChonLamChiSoChinhThuc);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, b.TrangThaiXuLy);
            Assert.Equal(85.5m, b.GiaTriXacNhan);
        }

        [Fact]
        public async Task ResetMeterBaseline_WhenSecondLinkChangedBaseline_RollsBackOfficialImageSwapAndPeriod()
        {
            var (host, seed, imageA, imageB, periodId, next1Id) = await SeedResetAsync(secondLaterChangedBaseline: true);
            using var hostScope = host;

            await using (var scope = host.Provider.CreateAsyncScope())
            {
                var portal = scope.ServiceProvider.GetRequiredService<IMeterImagePortalService>();
                var reset = await portal.DoiMocAsync(
                    new ResetMeterBaselineRequest(imageB, MeterBaselineResetType.DoiDongHo, 85.5m, 0m, 145.25m, "Thay đồng hồ"), seed.Staff.NguoiDungId);
                Assert.False(reset.Success);
                Assert.Contains("12/2026", reset.Message);
                Assert.Contains("đã đổi đồng hồ hoặc quay vòng", reset.Message);
            }

            // Việc bỏ ảnh chính thức cũ cần SaveChanges giữa chừng; sau rollback ảnh A phải vẫn là ảnh chính thức.
            await using var db = _fixture.CreateDbContext();
            var a = await db.AnhChiSoDongHos.AsNoTracking().SingleAsync(x => x.AnhChiSoDongHoId == imageA);
            var b = await db.AnhChiSoDongHos.AsNoTracking().SingleAsync(x => x.AnhChiSoDongHoId == imageB);
            Assert.True(a.DuocChonLamChiSoChinhThuc);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, a.TrangThaiXuLy);
            Assert.False(b.DuocChonLamChiSoChinhThuc);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DocDuoc, b.TrangThaiXuLy);

            var period = await ReloadAsync(periodId);
            Assert.Equal((100m, 120m), (period.ChiSoDienCu, period.ChiSoDienMoi));
            var next1 = await ReloadAsync(next1Id);
            Assert.Equal((120m, 120m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            var next2 = await FindAsync(seed.Room.PhongTroId, 12);
            Assert.Equal((0m, 30m), (next2!.ChiSoDienCu, next2.ChiSoDienMoi));
        }
    }
}
