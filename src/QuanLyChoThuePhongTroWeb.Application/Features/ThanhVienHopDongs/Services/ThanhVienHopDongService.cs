using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.Services
{
    public class ThanhVienHopDongService : IThanhVienHopDongService
    {
        private const string KhongCoQuyen = "Bạn không có quyền thao tác hợp đồng tại chi nhánh này.";
        private const string KhongTimThayThanhVien = "Không tìm thấy thông tin thành viên.";

        private readonly IThanhVienHopDongStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _access;
        private readonly ITenantVisibilityStore _tenantVisibility;

        public ThanhVienHopDongService(IThanhVienHopDongStore store, IUnitOfWork unitOfWork, IEmployeeAccessService access, ITenantVisibilityStore tenantVisibility)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _access = access;
            _tenantVisibility = tenantVisibility;
        }

        private async Task<bool> CanAccessBranchAsync(int actorId, int chiNhanhId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            return scope != null && scope.CanAccessBranch(chiNhanhId);
        }

        public async Task<List<ThanhVienHopDongRes>> GetThanhVienByHopDongIdAsync(int actorId, int hopDongId)
        {
            var hopDong = await _store.GetHopDongWithPhongTroAsync(hopDongId);
            if (hopDong == null || !await CanAccessBranchAsync(actorId, hopDong.PhongTro.ChiNhanhId))
                return new List<ThanhVienHopDongRes>();

            return await _store.GetThanhVienByHopDongIdAsync(hopDongId);
        }

        public async Task<ServiceResult> AddThanhVienVaoHopDongAsync(int actorId, ThanhVienHopDongReq request)
        {
            var hopDong = await _store.GetHopDongWithPhongTroAsync(request.HopDongId);
            if (hopDong == null) return ServiceResult.NotFound("Không tìm thấy hợp đồng.");
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || !scope.CanAccessBranch(hopDong.PhongTro.ChiNhanhId))
                return ServiceResult.Forbidden(KhongCoQuyen);

            // Người được thêm (chọn sẵn hoặc trùng CCCD với người đã có) phải nhìn thấy được với nhân viên.
            int? nguoiThueId = request.NguoiThueId > 0
                ? request.NguoiThueId
                : string.IsNullOrWhiteSpace(request.CCCD) ? null : (await _store.GetNguoiThueByCccdAsync(request.CCCD))?.NguoiThueId;
            if (nguoiThueId.HasValue && !scope.IsAdmin
                && !await _tenantVisibility.AreAllVisibleAsync(new[] { nguoiThueId.Value }, scope.AllowedBranchIds!))
                return ServiceResult.Forbidden("Bạn không có quyền dùng người thuê thuộc chi nhánh khác.");

            return ServiceResult.FromTuple(await AddCoreAsync(hopDong, request));
        }

        private async Task<(bool IsSuccess, string? ErrorMessage)> AddCoreAsync(HopDong hopDong, ThanhVienHopDongReq request)
        {
            if (hopDong.TrangThaiHopDong != TrangThaiHopDong.DangHoatDong)
                return (false, "Chỉ có thể thêm thành viên vào hợp đồng đang hoạt động.");

            int currentActiveMembers = await _store.CountActiveMembersAsync(request.HopDongId);
            if (currentActiveMembers >= hopDong.PhongTro.SoNguoiToiDa)
                return (false, $"Phòng đã đạt số người ở tối đa ({hopDong.PhongTro.SoNguoiToiDa} người).");

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                int nguoiThueIdToUse = 0;

                if (request.NguoiThueId.HasValue && request.NguoiThueId.Value > 0)
                {
                    nguoiThueIdToUse = request.NguoiThueId.Value;
                    var nguoiThueExists = await _store.ExistsNguoiThueAsync(nguoiThueIdToUse);
                    if (!nguoiThueExists) return (false, "Người thuê không tồn tại.");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(request.HoVaTen)) return (false, "Họ và tên không được để trống.");
                    if (string.IsNullOrWhiteSpace(request.SoDienThoai)) return (false, "Số điện thoại không được để trống.");
                    if (string.IsNullOrWhiteSpace(request.CCCD)) return (false, "CCCD không được để trống.");

                    var existingNguoiThue = await _store.GetNguoiThueByCccdAsync(request.CCCD);
                    if (existingNguoiThue != null)
                    {
                        nguoiThueIdToUse = existingNguoiThue.NguoiThueId;
                    }
                    else
                    {
                        var newNguoiThue = new NguoiThue
                        {
                            HoVaTen = request.HoVaTen,
                            SoDienThoai = request.SoDienThoai,
                            CCCD = request.CCCD,
                            NgayTao = DateTime.UtcNow,
                            IsDeleted = false
                        };
                        _store.AddNguoiThue(newNguoiThue);
                        await _unitOfWork.SaveChangesAsync();
                        nguoiThueIdToUse = newNguoiThue.NguoiThueId;
                    }
                }

                bool isOverlapping = await _store.IsNguoiThueOverlappingAsync(nguoiThueIdToUse);
                if (isOverlapping)
                {
                    await transaction.RollbackAsync();
                    return (false, "Người này hiện đang ở một phòng khác (Hợp đồng khác đang hoạt động).");
                }

                var chiTiet = new ChiTietThanhVienHopDong
                {
                    HopDongId = request.HopDongId,
                    NguoiThueId = nguoiThueIdToUse,
                    NgayVao = request.NgayVao.ToUniversalTime(),
                    IsDeleted = false
                };

                _store.AddChiTiet(chiTiet);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, string.Empty);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                string errorDetails = e.InnerException != null ? e.InnerException.Message : e.Message;
                return (false, $"Lỗi hệ thống khi thêm thành viên: {errorDetails}");
            }
        }

        public async Task<ServiceResult> BaoRoiPhongAsync(int actorId, int chiTietId)
        {
            var chiTiet = await _store.GetChiTietWithHopDongAsync(chiTietId);
            if (chiTiet == null) return ServiceResult.NotFound(KhongTimThayThanhVien);
            if (!await CanAccessBranchAsync(actorId, chiTiet.HopDong.PhongTro.ChiNhanhId))
                return ServiceResult.Forbidden(KhongCoQuyen);

            return ServiceResult.FromTuple(await BaoRoiPhongCoreAsync(chiTiet));
        }

        private async Task<(bool IsSuccess, string? ErrorMessage)> BaoRoiPhongCoreAsync(ChiTietThanhVienHopDong chiTiet)
        {

            if (chiTiet.NgayChuyenDi.HasValue) return (false, "Thành viên này đã rời phòng trước đó.");

            if (chiTiet.HopDong != null 
                && chiTiet.NguoiThueId == chiTiet.HopDong.NguoiThueId 
                && chiTiet.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
            {
                return (false, "Không thể báo rời phòng cho người đại diện hợp đồng khi hợp đồng còn hiệu lực. Vui lòng kết thúc hợp đồng trước.");
            }

            chiTiet.NgayChuyenDi = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            return (true, string.Empty);
        }

        public async Task<ServiceResult> XoaThanhVienNhamAsync(int actorId, int chiTietId)
        {
            var chiTiet = await _store.GetChiTietWithHopDongAsync(chiTietId);
            if (chiTiet == null) return ServiceResult.NotFound(KhongTimThayThanhVien);
            if (!await CanAccessBranchAsync(actorId, chiTiet.HopDong.PhongTro.ChiNhanhId))
                return ServiceResult.Forbidden(KhongCoQuyen);

            return ServiceResult.FromTuple(await XoaThanhVienCoreAsync(chiTiet));
        }

        private async Task<(bool IsSuccess, string? ErrorMessage)> XoaThanhVienCoreAsync(ChiTietThanhVienHopDong chiTiet)
        {

            if (chiTiet.HopDong != null 
                && chiTiet.NguoiThueId == chiTiet.HopDong.NguoiThueId 
                && chiTiet.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
            {
                return (false, "Không thể xoá người đại diện hợp đồng khỏi danh sách thành viên khi hợp đồng còn hiệu lực.");
            }

            chiTiet.IsDeleted = true;
            await _unitOfWork.SaveChangesAsync();

            return (true, string.Empty);
        }
    }
}
