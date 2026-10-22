using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public class DienNuocService : IDienNuocService
    {
        private readonly IDienNuocStore _store;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IMeterReadingWorkflowService _workflowService;

        public DienNuocService(
            IDienNuocStore store,
            IEmployeeAccessService employeeAccessService,
            IMeterReadingWorkflowService workflowService)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _employeeAccessService = employeeAccessService ?? throw new ArgumentNullException(nameof(employeeAccessService));
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
        }

        public async Task<List<DienNuocPhongRes>> GetDanhSachDienNuocAsync(int chiNhanhId, int thang, int nam, int actorId)
        {
            if (actorId <= 0 ||
                !await _employeeAccessService.CanPerformAsync(actorId, chiNhanhId, EmployeeActionCodes.MeterRead))
            {
                return new List<DienNuocPhongRes>();
            }

            // Tháng tính theo giờ Việt Nam (UTC+7), đổi sang UTC để so với mốc hợp đồng lưu UTC.
            var (startOfMonth, endExclusiveUtc) = MeterPeriodPolicy.MonthRangeUtc(new MeterPeriod(thang, nam));
            var endOfMonth = endExclusiveUtc.AddSeconds(-1);

            var hopDongsActive = await _store.GetActiveContractsInBranchAsync(chiNhanhId, startOfMonth, endOfMonth);

            if (!hopDongsActive.Any()) return new List<DienNuocPhongRes>();

            var phongĐangThue = hopDongsActive
                .GroupBy(h => h.PhongTroId)
                .Select(g => g.OrderByDescending(h => h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
                              .ThenByDescending(h => h.ThoiDiemBatDau)
                              .First())
                .ToList();

            var phongTroIds = phongĐangThue.Select(h => h.PhongTroId).ToList();

            var currentRecords = await _store.GetCurrentMonthRecordsAsync(phongTroIds, thang, nam);

            int prevThang = thang == 1 ? 12 : thang - 1;
            int prevNam = thang == 1 ? nam - 1 : nam;

            var prevRecords = await _store.GetPreviousMonthRecordsAsync(phongTroIds, prevThang, prevNam);
            var lockedPhongIds = await _store.GetLockedRoomIdsAsync(phongTroIds, thang, nam);
            var phongCoHopDongThangTruoc = await _store.GetRoomIdsWithContractInMonthAsync(phongTroIds, prevThang, prevNam);

            var result = new List<DienNuocPhongRes>();

            foreach (var hd in phongĐangThue)
            {
                var phongTroId = hd.PhongTroId;

                // Đánh dấu khóa sẵn mọi phòng mà ApprovePeriodsAsync sẽ từ chối, để màn chốt
                // không gửi chúng lên và làm rollback cả lô.
                string? lyDoKhoa = null;
                if (lockedPhongIds.Contains(phongTroId))
                {
                    lyDoKhoa = "Kỳ sau đã chốt hoặc kỳ này đã có hóa đơn phát hành.";
                }
                else if (phongCoHopDongThangTruoc.Contains(phongTroId) &&
                         (!prevRecords.TryGetValue(phongTroId, out var kyTruoc) || kyTruoc.TrangThaiGhiNhan != TrangThaiGhiNhan.DaDuyet))
                {
                    lyDoKhoa = $"Kỳ {prevThang:00}/{prevNam} chưa được chốt, cần chốt kỳ đó trước.";
                }
                bool isLocked = lyDoKhoa != null;

                if (currentRecords.TryGetValue(phongTroId, out var currentRecord))
                {
                    var nonDeletedImages = currentRecord.AnhChiSoDongHos?.Where(a => !a.IsDeleted).ToList() ?? new List<AnhChiSoDongHo>();
                    var dienImages = nonDeletedImages.Where(a => a.LoaiDongHo == LoaiDongHo.Dien).ToList();
                    var nuocImages = nonDeletedImages.Where(a => a.LoaiDongHo == LoaiDongHo.Nuoc).ToList();

                    var officialDien = dienImages.FirstOrDefault(a => a.DuocChonLamChiSoChinhThuc);
                    var newestDien = dienImages.OrderByDescending(a => a.NgayGui).ThenByDescending(a => a.AnhChiSoDongHoId).FirstOrDefault();
                    var representativeDien = officialDien ?? newestDien;

                    var officialNuoc = nuocImages.FirstOrDefault(a => a.DuocChonLamChiSoChinhThuc);
                    var newestNuoc = nuocImages.OrderByDescending(a => a.NgayGui).ThenByDescending(a => a.AnhChiSoDongHoId).FirstOrDefault();
                    var representativeNuoc = officialNuoc ?? newestNuoc;

                    result.Add(new DienNuocPhongRes
                    {
                        DichVuDienNuocCuaPhongId = currentRecord.DichVuDienNuocCuaPhongId,
                        PhongTroId = phongTroId,
                        TenPhong = hd.PhongTro.SoPhong,
                        TenNguoiDaiDien = hd.NguoiThue.HoVaTen,
                        ChiSoDienCu = currentRecord.ChiSoDienCu,
                        ChiSoDienMoi = currentRecord.ChiSoDienMoi,
                        ChiSoNuocCu = currentRecord.ChiSoNuocCu,
                        ChiSoNuocMoi = currentRecord.ChiSoNuocMoi,
                        IsDaChot = currentRecord.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet,
                        CoAnhDienChinhThuc = officialDien != null,
                        CoAnhNuocChinhThuc = officialNuoc != null,
                        GiaTriDienXacNhanTuAnh = officialDien?.GiaTriXacNhan,
                        GiaTriNuocXacNhanTuAnh = officialNuoc?.GiaTriXacNhan,
                        TrangThaiGhiNhan = (QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiGhiNhan)currentRecord.TrangThaiGhiNhan,
                        IsLocked = isLocked,
                        LyDoKhoa = lyDoKhoa,
                        SoAnhDien = dienImages.Count,
                        SoAnhNuoc = nuocImages.Count,
                        TrangThaiAnhDien = representativeDien != null ? (QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiAnhChiSo)representativeDien.TrangThaiXuLy : null,
                        TrangThaiAnhNuoc = representativeNuoc != null ? (QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiAnhChiSo)representativeNuoc.TrangThaiXuLy : null
                    });
                }
                else
                {
                    decimal dienCu = 0m;
                    decimal nuocCu = 0m;
                    if (prevRecords.TryGetValue(phongTroId, out var prevRecord))
                    {
                        dienCu = prevRecord.ChiSoDienMoi;
                        nuocCu = prevRecord.ChiSoNuocMoi;
                    }
                    else
                    {
                        var ganNhat = await _store.GetNearestPreviousReadingAsync(phongTroId, thang, nam);
                        dienCu = ganNhat.ChiSoDienMoi;
                        nuocCu = ganNhat.ChiSoNuocMoi;
                    }

                    result.Add(new DienNuocPhongRes
                    {
                        DichVuDienNuocCuaPhongId = 0,
                        PhongTroId = phongTroId,
                        TenPhong = hd.PhongTro.SoPhong,
                        TenNguoiDaiDien = hd.NguoiThue.HoVaTen,
                        ChiSoDienCu = dienCu,
                        ChiSoDienMoi = 0m,
                        ChiSoNuocCu = nuocCu,
                        ChiSoNuocMoi = 0m,
                        IsDaChot = false,
                        IsLocked = isLocked,
                        LyDoKhoa = lyDoKhoa,
                        SoAnhDien = 0,
                        SoAnhNuoc = 0,
                        TrangThaiAnhDien = null,
                        TrangThaiAnhNuoc = null
                    });
                }
            }

            return result.OrderBy(x => x.TenPhong).ToList();
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> SaveChotDienNuocAsync(ChotDienNuocReq input, int actorId = 0)
        {
            if (input == null || input.DanhSachPhong == null || !input.DanhSachPhong.Any())
            {
                return (false, "Không có dữ liệu để lưu.");
            }

            if (input.Thang < 1 || input.Thang > 12 || input.Nam < 2000)
            {
                return (false, "Thời gian chốt chỉ số điện nước không hợp lệ.");
            }

            if (actorId <= 0)
            {
                return (false, "Người thực hiện không hợp lệ.");
            }

            var phongTroIds = input.DanhSachPhong.Select(x => x.PhongTroId).Distinct().ToList();
            var roomBranches = await _store.GetRoomBranchIdsAsync(phongTroIds);
            if (roomBranches.Count != phongTroIds.Count)
            {
                return (false, "Một hoặc nhiều phòng trọ không tồn tại hoặc đã bị xóa.");
            }

            var distinctBranchIds = roomBranches.Values.Distinct().ToList();
            if (distinctBranchIds.Count > 1)
            {
                return (false, "Dữ liệu chốt điện nước không hợp lệ: các phòng trọ thuộc nhiều chi nhánh khác nhau.");
            }

            var actualBranchId = distinctBranchIds.First();
            if (input.ChiNhanhId != actualBranchId)
            {
                return (false, "Chi nhánh yêu cầu không khớp với chi nhánh thực tế của các phòng trọ.");
            }

            var canPerform = await _employeeAccessService.CanPerformAsync(actorId, actualBranchId, EmployeeActionCodes.MeterReview);
            if (!canPerform)
            {
                return (false, "Bạn không có quyền chốt chỉ số điện nước tại chi nhánh này.");
            }

            var workflowReq = new ApproveMeterPeriodsRequest
            {
                Thang = input.Thang,
                Nam = input.Nam,
                DanhSachPhong = input.DanhSachPhong.Select(req => new ApproveMeterPeriodItem
                {
                    PhongTroId = req.PhongTroId,
                    DienMode = req.DienMode,
                    NuocMode = req.NuocMode,
                    ChiSoDienMoiThuCong = req.DienMode == MeterReadingSubmissionMode.Manual ? req.ChiSoDienMoi : null,
                    ChiSoNuocMoiThuCong = req.NuocMode == MeterReadingSubmissionMode.Manual ? req.ChiSoNuocMoi : null,
                    LyDoDienThuCong = req.DienMode == MeterReadingSubmissionMode.Manual ? req.LyDoNhapThuCongDien : null,
                    LyDoNuocThuCong = req.NuocMode == MeterReadingSubmissionMode.Manual ? req.LyDoNhapThuCongNuoc : null
                }).ToList()
            };

            var batchResult = await _workflowService.ApprovePeriodsAsync(workflowReq, actorId);
            if (!batchResult.Success)
            {
                return (false, batchResult.Message);
            }

            return (true, string.Empty);
        }
    }
}
