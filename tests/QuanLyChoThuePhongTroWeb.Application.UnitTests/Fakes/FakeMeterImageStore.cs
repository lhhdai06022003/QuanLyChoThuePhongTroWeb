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

        public int GetBranchIdByRoomCalls { get; private set; }
        public int GetBranchIdsByRoomsCalls { get; private set; }

        // Đếm lời gọi đọc từng phòng/từng kỳ, để test kiểm duyệt nhiều phòng không đọc lặp theo phòng.
        public int HasContractInMonthCalls { get; private set; }
        public int GetPeriodRecordCalls { get; private set; }
        public int GetPeriodForUpdateCalls { get; private set; }
        public int GetImagesForUpdateCalls { get; private set; }
        public int HasSubsequentPeriodCalls { get; private set; }
        public int HasLockedInvoiceCalls { get; private set; }
        public int GetServicePriceCalls { get; private set; }

        public Task<int?> GetBranchIdByRoomAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            GetBranchIdByRoomCalls++;
            return Task.FromResult(RoomBranches.TryGetValue(phongTroId, out var bId) ? (int?)bId : null);
        }

        public Task<IReadOnlyDictionary<int, int>> GetBranchIdsByRoomsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
        {
            GetBranchIdsByRoomsCalls++;
            return Task.FromResult<IReadOnlyDictionary<int, int>>(roomIds.Where(RoomBranches.ContainsKey).Distinct().ToDictionary(id => id, id => RoomBranches[id]));
        }

        public Task<bool> HasActiveContractForTenantInPeriodAsync(int phongTroId, int tenantUserId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ActiveTenantContracts.Contains((phongTroId, tenantUserId, thang, nam)));
        }

        public Task<bool> HasContractInMonthAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            HasContractInMonthCalls++;
            return Task.FromResult(ContractMonths.Contains((phongTroId, thang, nam)));
        }

        public Task<DichVuDienNuocCuaPhong?> GetPeriodRecordAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            GetPeriodRecordCalls++;
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
            HasSubsequentPeriodCalls++;
            return Task.FromResult(SubsequentPeriods.Contains((phongTroId, thang, nam)));
        }

        public Task<IReadOnlyList<DichVuDienNuocCuaPhong>> GetSubsequentPeriodsForUpdateAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            var list = Periods.Values
                .Where(p => p.PhongTroId == phongTroId && !p.IsDeleted && (p.Nam > nam || (p.Nam == nam && p.Thang > thang)))
                .OrderBy(p => p.Nam).ThenBy(p => p.Thang)
                .ToList();
            return Task.FromResult<IReadOnlyList<DichVuDienNuocCuaPhong>>(list);
        }

        public Task<bool> HasNonDraftInvoiceAsync(int periodRecordId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NonDraftInvoicePeriodIds.Contains(periodRecordId));
        }

        public Task<bool> HasLockedInvoiceAsync(int periodRecordId, CancellationToken cancellationToken = default)
        {
            HasLockedInvoiceCalls++;
            return HasNonDraftInvoiceAsync(periodRecordId, cancellationToken);
        }

        // Bản theo lô: cùng dữ liệu với bản từng phòng nhưng không tăng bộ đếm từng phòng.
        public int GetPeriodsForUpdateBatchCalls { get; private set; }
        public int GetImagesForUpdateBatchCalls { get; private set; }
        public int SubsequentBatchCalls { get; private set; }
        public int LockedInvoiceBatchCalls { get; private set; }
        public int ContractInMonthBatchCalls { get; private set; }
        public int GetPeriodRecordsBatchCalls { get; private set; }

        public Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetPeriodsForUpdateAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
        {
            GetPeriodsForUpdateBatchCalls++;
            var result = new Dictionary<int, DichVuDienNuocCuaPhong>();
            foreach (var id in roomIds.Distinct())
            {
                var period = FindPeriodForUpdate(id, thang, nam);
                if (period != null) result[id] = period;
            }
            return Task.FromResult<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>>(result);
        }

        public Task<IReadOnlyList<AnhChiSoDongHo>> GetImagesForUpdateByPeriodsAsync(IReadOnlyList<int> periodRecordIds, CancellationToken cancellationToken = default)
        {
            GetImagesForUpdateBatchCalls++;
            var ids = periodRecordIds.ToHashSet();
            var list = Images.Values
                .Where(i => ids.Contains(i.DichVuDienNuocCuaPhongId) && !i.IsDeleted)
                .OrderBy(i => i.AnhChiSoDongHoId)
                .ToList();
            return Task.FromResult<IReadOnlyList<AnhChiSoDongHo>>(list);
        }

        public Task<IReadOnlySet<int>> GetRoomsWithLockingSubsequentPeriodAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
        {
            SubsequentBatchCalls++;
            return Task.FromResult<IReadOnlySet<int>>(roomIds.Where(id => SubsequentPeriods.Contains((id, thang, nam))).ToHashSet());
        }

        public Task<IReadOnlySet<int>> GetPeriodIdsWithLockedInvoiceAsync(IReadOnlyList<int> periodRecordIds, CancellationToken cancellationToken = default)
        {
            LockedInvoiceBatchCalls++;
            return Task.FromResult<IReadOnlySet<int>>(periodRecordIds.Where(NonDraftInvoicePeriodIds.Contains).ToHashSet());
        }

        public Task<IReadOnlySet<int>> GetRoomsWithContractInMonthAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
        {
            ContractInMonthBatchCalls++;
            return Task.FromResult<IReadOnlySet<int>>(roomIds.Where(id => ContractMonths.Contains((id, thang, nam))).ToHashSet());
        }

        public Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetPeriodRecordsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
        {
            GetPeriodRecordsBatchCalls++;
            var result = Periods.Values
                .Where(p => roomIds.Contains(p.PhongTroId) && p.Thang == thang && p.Nam == nam && !p.IsDeleted)
                .GroupBy(p => p.PhongTroId)
                .ToDictionary(g => g.Key, g => g.First());
            return Task.FromResult<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>>(result);
        }

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

        public Dictionary<int, string> RoomNumbers { get; } = new();

        public Task<IReadOnlyDictionary<int, string>> GetRoomNumbersAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, string>>(roomIds.Where(RoomNumbers.ContainsKey).Distinct().ToDictionary(id => id, id => RoomNumbers[id]));

        public Task LockRoomsAsync(IEnumerable<int> roomIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<DichVuDienNuocCuaPhong?> GetPeriodForUpdateAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            GetPeriodForUpdateCalls++;
            return Task.FromResult(FindPeriodForUpdate(phongTroId, thang, nam));
        }

        private DichVuDienNuocCuaPhong? FindPeriodForUpdate(int phongTroId, int thang, int nam)
            => PeriodForUpdateOverride ?? Periods.Values.FirstOrDefault(p => p.PhongTroId == phongTroId && p.Thang == thang && p.Nam == nam && !p.IsDeleted);

        public Task<DichVuDienNuocCuaPhong?> GetPeriodByIdForUpdateAsync(int periodRecordId, CancellationToken cancellationToken = default)
            => GetPeriodRecordByIdAsync(periodRecordId, cancellationToken);

        public Task<AnhChiSoDongHo?> GetImageForUpdateAsync(int imageId, CancellationToken cancellationToken = default)
            => GetImageByIdAsync(imageId, cancellationToken);

        public Task<IReadOnlyList<AnhChiSoDongHo>> GetImagesForUpdateAsync(int periodRecordId, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
        {
            GetImagesForUpdateCalls++;
            return GetImagesByPeriodAndTypeAsync(periodRecordId, loaiDongHo, cancellationToken);
        }

        public decimal DefaultChiSoDienMoi { get; set; } = 100m;
        public decimal DefaultChiSoNuocMoi { get; set; } = 50m;

        public Task<(decimal ChiSoDienMoi, decimal ChiSoNuocMoi)> GetNearestPreviousReadingAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((DefaultChiSoDienMoi, DefaultChiSoNuocMoi));
        }

        public Task<DichVuDienNuocCuaPhong?> GetNearestPreviousPeriodAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            var previous = Periods.Values
                .Where(p => p.PhongTroId == phongTroId && !p.IsDeleted && (p.Nam < nam || (p.Nam == nam && p.Thang < thang)))
                .OrderByDescending(p => p.Nam)
                .ThenByDescending(p => p.Thang)
                .FirstOrDefault();
            return Task.FromResult(previous);
        }

        public Task<decimal> GetServicePriceAsync(string serviceKeyword, int chiNhanhId, CancellationToken cancellationToken = default)
        {
            GetServicePriceCalls++;
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
