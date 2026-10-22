using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services
{
    public class EmployeeBranchAssignmentService : IEmployeeBranchAssignmentService
    {
        private const string InvalidDataMessage = "Dữ liệu không hợp lệ.";
        private const string ActorNotAllowedMessage = "Chỉ Quản trị viên đang hoạt động mới được quản lý phân công chi nhánh.";
        private const string InvalidTargetMessage = "Tài khoản không tồn tại, đã bị xóa hoặc không phải nhân viên.";
        private const string InvalidBranchMessage = "Chi nhánh không tồn tại hoặc đã bị xóa.";
        private const string AssignReasonTooLongMessage = "Lý do tối đa 500 ký tự.";
        private const string RevokeReasonInvalidMessage = "Vui lòng nhập lý do thu hồi (1–500 ký tự).";
        private const string AssignSuccessMessage = "Đã phân công nhân viên phụ trách chi nhánh.";
        private const string AlreadyAssignedMessage = "Nhân viên đã phụ trách chi nhánh này";
        private const string RevokeSuccessMessage = "Đã thu hồi phân công chi nhánh.";
        private const string AlreadyRevokedMessage = "Nhân viên không còn phụ trách chi nhánh này";
        private const string UnexpectedErrorMessage = "Không thể lưu phân công chi nhánh. Vui lòng thử lại.";

        private readonly IEmployeeBranchStore _employeeBranchStore;
        private readonly IEmployeeBranchAssignmentStore _assignmentStore;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EmployeeBranchAssignmentService> _logger;

        public EmployeeBranchAssignmentService(
            IEmployeeBranchStore employeeBranchStore,
            IEmployeeBranchAssignmentStore assignmentStore,
            IUnitOfWork unitOfWork,
            ILogger<EmployeeBranchAssignmentService> logger)
        {
            _employeeBranchStore = employeeBranchStore;
            _assignmentStore = assignmentStore;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ServiceResult<IReadOnlyList<EmployeeBranchSummaryDto>>> GetEmployeesAsync(int? chiNhanhId, int actorId, CancellationToken ct = default)
        {
            if (!await IsActiveAdminAsync(actorId, ct))
            {
                return ServiceResult<IReadOnlyList<EmployeeBranchSummaryDto>>.Fail(ActorNotAllowedMessage);
            }

            var list = await _assignmentStore.GetEmployeeSummariesAsync(chiNhanhId is > 0 ? chiNhanhId : null, ct);
            return ServiceResult<IReadOnlyList<EmployeeBranchSummaryDto>>.Ok(list);
        }

        public async Task<ServiceResult<EmployeeBranchDetailDto>> GetEmployeeAsync(int nguoiDungId, int actorId, CancellationToken ct = default)
        {
            if (!await IsActiveAdminAsync(actorId, ct))
            {
                return ServiceResult<EmployeeBranchDetailDto>.Fail(ActorNotAllowedMessage);
            }

            var detail = await _assignmentStore.GetEmployeeDetailAsync(nguoiDungId, ct);
            if (detail == null)
            {
                return ServiceResult<EmployeeBranchDetailDto>.Fail(InvalidTargetMessage);
            }

            return ServiceResult<EmployeeBranchDetailDto>.Ok(detail);
        }

        public async Task<ServiceResult<IReadOnlyList<BranchOptionDto>>> GetBranchOptionsAsync(int actorId, CancellationToken ct = default)
        {
            if (!await IsActiveAdminAsync(actorId, ct))
            {
                return ServiceResult<IReadOnlyList<BranchOptionDto>>.Fail(ActorNotAllowedMessage);
            }

            var options = await _assignmentStore.GetBranchOptionsAsync(ct);
            return ServiceResult<IReadOnlyList<BranchOptionDto>>.Ok(options);
        }

        public async Task<ServiceResult> AssignAsync(AssignEmployeeBranchRequest request, int actorId, CancellationToken ct = default)
        {
            if (request == null || request.NguoiDungId <= 0 || request.ChiNhanhId <= 0)
            {
                return ServiceResult.Fail(InvalidDataMessage);
            }

            var normalized = string.IsNullOrWhiteSpace(request.LyDo) ? null : request.LyDo.Trim();
            if (normalized?.Length > 500)
            {
                return ServiceResult.Fail(AssignReasonTooLongMessage);
            }

            if (!await IsActiveAdminAsync(actorId, ct))
            {
                return ServiceResult.Fail(ActorNotAllowedMessage);
            }

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                var target = await _assignmentStore.LockEmployeeAsync(request.NguoiDungId, ct);
                if (target == null || target.IsDeleted || target.Role != Role.NhanVien)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult.Fail(InvalidTargetMessage);
                }

                var branchAssignable = await _assignmentStore.IsBranchAssignableAsync(request.ChiNhanhId, ct);
                if (!branchAssignable)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult.Fail(InvalidBranchMessage);
                }

                var now = DateTime.UtcNow;
                var assignment = await _assignmentStore.GetAssignmentAsync(request.NguoiDungId, request.ChiNhanhId, ct);
                if (assignment == null)
                {
                    await _assignmentStore.AddAssignmentAsync(new NhanVienChiNhanh
                    {
                        NguoiDungId = request.NguoiDungId,
                        ChiNhanhId = request.ChiNhanhId,
                        IsActive = true,
                        NgayPhanCong = now,
                        NguoiPhanCongId = actorId,
                        LyDo = normalized
                    }, ct);
                }
                else if (assignment.IsActive && assignment.NgayThuHoi == null)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult.Ok(AlreadyAssignedMessage);
                }
                else
                {
                    assignment.PhanCongLai(actorId, now, normalized);
                }

                await _unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return ServiceResult.Ok(AssignSuccessMessage);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi khi phân công chi nhánh cho nhân viên {NguoiDungId}, chi nhánh {ChiNhanhId}", request.NguoiDungId, request.ChiNhanhId);
                return ServiceResult.Fail(UnexpectedErrorMessage);
            }
        }

        public async Task<ServiceResult> RevokeAsync(RevokeEmployeeBranchRequest request, int actorId, CancellationToken ct = default)
        {
            if (request == null || request.NguoiDungId <= 0 || request.ChiNhanhId <= 0)
            {
                return ServiceResult.Fail(InvalidDataMessage);
            }

            var trimmedReason = request.LyDo?.Trim() ?? string.Empty;
            if (trimmedReason.Length < 1 || trimmedReason.Length > 500)
            {
                return ServiceResult.Fail(RevokeReasonInvalidMessage);
            }

            if (!await IsActiveAdminAsync(actorId, ct))
            {
                return ServiceResult.Fail(ActorNotAllowedMessage);
            }

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                var target = await _assignmentStore.LockEmployeeAsync(request.NguoiDungId, ct);
                if (target == null || target.IsDeleted || target.Role != Role.NhanVien)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult.Fail(InvalidTargetMessage);
                }

                var assignment = await _assignmentStore.GetAssignmentAsync(request.NguoiDungId, request.ChiNhanhId, ct);
                if (assignment == null || !assignment.IsActive || assignment.NgayThuHoi != null)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult.Ok(AlreadyRevokedMessage);
                }

                assignment.ThuHoi(actorId, DateTime.UtcNow, trimmedReason);
                await _unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return ServiceResult.Ok(RevokeSuccessMessage);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi khi thu hồi phân công chi nhánh cho nhân viên {NguoiDungId}, chi nhánh {ChiNhanhId}", request.NguoiDungId, request.ChiNhanhId);
                return ServiceResult.Fail(UnexpectedErrorMessage);
            }
        }

        private async Task<bool> IsActiveAdminAsync(int actorId, CancellationToken ct)
        {
            if (actorId <= 0)
            {
                return false;
            }

            var actor = await _employeeBranchStore.GetActorWithActiveBranchesAsync(actorId, ct);
            return actor != null && actor.IsActive && actor.Role == Role.Admin;
        }
    }
}
