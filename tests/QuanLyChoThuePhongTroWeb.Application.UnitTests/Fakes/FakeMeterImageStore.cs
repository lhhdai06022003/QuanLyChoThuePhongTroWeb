using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeMeterImageStore : IMeterImageStore
    {
        public Dictionary<int, int> RoomBranches { get; } = new();
        public HashSet<(int RoomId, int TenantUserId, int Thang, int Nam)> ActiveTenantContracts { get; } = new();
        public HashSet<(int RoomId, int Thang, int Nam)> ContractMonths { get; } = new();
        public Dictionary<int, DichVuDienNuocCuaPhong> Periods { get; } = new();
        public Dictionary<int, AnhChiSoDongHo> Images { get; } = new();
        public HashSet<(int RoomId, int Thang, int Nam)> SubsequentPeriods { get; } = new();
        public HashSet<int> NonDraftInvoicePeriodIds { get; } = new();
        private int _periodIdSeq = 1;
        private int _imageIdSeq = 1;

        public Task<int?> GetBranchIdByRoomAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RoomBranches.TryGetValue(phongTroId, out var bId) ? (int?)bId : null);
        }

        public Task<bool> HasActiveContractForTenantInPeriodAsync(int phongTroId, int tenantUserId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ActiveTenantContracts.Contains((phongTroId, tenantUserId, thang, nam)));
        }

        public Task<bool> HasContractInMonthAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ContractMonths.Contains((phongTroId, thang, nam)));
        }

        public Task<DichVuDienNuocCuaPhong?> GetPeriodRecordAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            var period = Periods.Values.FirstOrDefault(p => p.PhongTroId == phongTroId && p.Thang == thang && p.Nam == nam && !p.IsDeleted);
            return Task.FromResult(period);
        }

        public Task<DichVuDienNuocCuaPhong?> GetPeriodRecordWithLockAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return GetPeriodRecordAsync(phongTroId, thang, nam, cancellationToken);
        }

        public Task<DichVuDienNuocCuaPhong?> GetPeriodRecordByIdAsync(int periodRecordId, CancellationToken cancellationToken = default)
        {
            Periods.TryGetValue(periodRecordId, out var period);
            return Task.FromResult(period != null && !period.IsDeleted ? period : null);
        }

        public Task<DichVuDienNuocCuaPhong?> GetPeriodRecordByIdWithLockAsync(int periodRecordId, CancellationToken cancellationToken = default)
        {
            return GetPeriodRecordByIdAsync(periodRecordId, cancellationToken);
        }

        public int GetImageByIdCallCount { get; set; }
        public bool ReturnNullOnSecondGetImageById { get; set; }

        public Task<AnhChiSoDongHo?> GetImageByIdAsync(int imageId, CancellationToken cancellationToken = default)
        {
            GetImageByIdCallCount++;
            if (ReturnNullOnSecondGetImageById && GetImageByIdCallCount >= 2) return Task.FromResult<AnhChiSoDongHo?>(null);
            Images.TryGetValue(imageId, out var img);
            return Task.FromResult(img != null && !img.IsDeleted ? img : null);
        }

        public Task<AnhChiSoDongHo?> GetImageByIdWithLockAsync(int imageId, CancellationToken cancellationToken = default)
        {
            return GetImageByIdAsync(imageId, cancellationToken);
        }

        public Task<IReadOnlyList<AnhChiSoDongHo>> GetImagesByPeriodAndTypeAsync(int periodRecordId, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
        {
            var list = Images.Values
                .Where(i => i.DichVuDienNuocCuaPhongId == periodRecordId && i.LoaiDongHo == loaiDongHo && !i.IsDeleted)
                .ToList();
            return Task.FromResult<IReadOnlyList<AnhChiSoDongHo>>(list);
        }

        public Task<AnhChiSoDongHo?> GetOfficialImageAsync(int periodRecordId, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
        {
            var img = Images.Values
                .FirstOrDefault(i => i.DichVuDienNuocCuaPhongId == periodRecordId && i.LoaiDongHo == loaiDongHo && i.DuocChonLamChiSoChinhThuc && !i.IsDeleted);
            return Task.FromResult(img);
        }

        public Task<bool> HasSubsequentPeriodAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SubsequentPeriods.Contains((phongTroId, thang, nam)));
        }

        public Task<bool> HasNonDraftInvoiceAsync(int periodRecordId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NonDraftInvoicePeriodIds.Contains(periodRecordId));
        }

        public Task<bool> HasLockedInvoiceAsync(int periodRecordId, CancellationToken cancellationToken = default)
            => HasNonDraftInvoiceAsync(periodRecordId, cancellationToken);

        public Task<MeterImageAccessContext?> GetImageAccessContextAsync(int imageId, CancellationToken cancellationToken = default)
        {
            if (!Images.TryGetValue(imageId, out var img)) return Task.FromResult<MeterImageAccessContext?>(null);
            Periods.TryGetValue(img.DichVuDienNuocCuaPhongId, out var period);
            var branchId = period != null && RoomBranches.TryGetValue(period.PhongTroId, out var bId) ? bId : 1;
            return Task.FromResult<MeterImageAccessContext?>(new MeterImageAccessContext
            {
                AnhChiSoDongHoId = img.AnhChiSoDongHoId,
                DichVuDienNuocCuaPhongId = img.DichVuDienNuocCuaPhongId,
                PhongTroId = period?.PhongTroId ?? 0,
                ChiNhanhId = branchId,
                TrangThaiXuLy = img.TrangThaiXuLy,
                LoaiDongHo = img.LoaiDongHo,
                DuocChonLamChiSoChinhThuc = img.DuocChonLamChiSoChinhThuc,
                IsDeleted = img.IsDeleted
            });
        }

        public DichVuDienNuocCuaPhong? PeriodForUpdateOverride { get; set; }

        public Task LockRoomsAsync(IEnumerable<int> roomIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<DichVuDienNuocCuaPhong?> GetPeriodForUpdateAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
            => Task.FromResult(PeriodForUpdateOverride ?? Periods.Values.FirstOrDefault(p => p.PhongTroId == phongTroId && p.Thang == thang && p.Nam == nam && !p.IsDeleted));

        public Task<DichVuDienNuocCuaPhong?> GetPeriodByIdForUpdateAsync(int periodRecordId, CancellationToken cancellationToken = default)
            => GetPeriodRecordByIdAsync(periodRecordId, cancellationToken);

        public Task<AnhChiSoDongHo?> GetImageForUpdateAsync(int imageId, CancellationToken cancellationToken = default)
            => GetImageByIdAsync(imageId, cancellationToken);

        public Task<IReadOnlyList<AnhChiSoDongHo>> GetImagesForUpdateAsync(int periodRecordId, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
            => GetImagesByPeriodAndTypeAsync(periodRecordId, loaiDongHo, cancellationToken);

        public decimal DefaultChiSoDienMoi { get; set; } = 100m;
        public decimal DefaultChiSoNuocMoi { get; set; } = 50m;

        public Task<(decimal ChiSoDienMoi, decimal ChiSoNuocMoi)> GetNearestPreviousReadingAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((DefaultChiSoDienMoi, DefaultChiSoNuocMoi));
        }

        public Task<decimal> GetServicePriceAsync(string serviceKeyword, int chiNhanhId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(serviceKeyword.Contains("điện") ? 3500m : 20000m);
        }

        public Task AddPeriodRecordAsync(DichVuDienNuocCuaPhong record, CancellationToken cancellationToken = default)
        {
            if (record.DichVuDienNuocCuaPhongId <= 0) record.DichVuDienNuocCuaPhongId = _periodIdSeq++;
            Periods[record.DichVuDienNuocCuaPhongId] = record;
            return Task.CompletedTask;
        }

        public int UpdatePeriodRecordCallCount { get; private set; }
        public int UpdateImageCallCount { get; private set; }

        public void UpdatePeriodRecord(DichVuDienNuocCuaPhong record)
        {
            UpdatePeriodRecordCallCount++;
            Periods[record.DichVuDienNuocCuaPhongId] = record;
        }

        public Task AddImageAsync(AnhChiSoDongHo image, CancellationToken cancellationToken = default)
        {
            if (image.AnhChiSoDongHoId <= 0) image.AnhChiSoDongHoId = _imageIdSeq++;
            Images[image.AnhChiSoDongHoId] = image;
            return Task.CompletedTask;
        }

        public void UpdateImage(AnhChiSoDongHo image)
        {
            UpdateImageCallCount++;
            Images[image.AnhChiSoDongHoId] = image;
        }
    }
}
