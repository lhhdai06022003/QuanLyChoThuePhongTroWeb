using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services
{
    public class NguoiDungService : INguoiDungService
    {
        private readonly INguoiDungStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordService _passwordService;

        public NguoiDungService(
            INguoiDungStore store,
            IUnitOfWork unitOfWork,
            IPasswordService passwordService)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _passwordService = passwordService;
        }

        // --- CHỨC NĂNG ĐĂNG NHẬP & SEED DATA ---

        public async Task SeedAdminAccountAsync()
        {
            var admin = await _store.GetActiveByUsernameAsync("admin");
            if (admin == null)
            {
                admin = new NguoiDung
                {
                    TenDangNhap = "admin",
                    Role = Role.Admin,
                    IsActive = true,
                    NgayTao = DateTime.UtcNow
                };
                admin.MatKhauHash = _passwordService.HashPassword("admin");
                await _store.AddAsync(admin);
                await _unitOfWork.SaveChangesAsync();
            }
            else
            {
                // Tự động nâng cấp mật khẩu của admin cũ từ SHA256 sang PBKDF2
                if (admin.MatKhauHash == "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918")
                {
                    admin.MatKhauHash = _passwordService.HashPassword("admin");
                    _store.Update(admin);
                    await _unitOfWork.SaveChangesAsync();
                }
            }
        }

        public async Task<UserAuthDto?> ValidateUserAsync(string username, string password)
        {
            var user = await _store.GetActiveByUsernameAsync(username);

            if (user != null && user.IsActive)
            {
                if (_passwordService.VerifyPassword(user.MatKhauHash, password, out bool needsRehash))
                {
                    user.LanDangNhapCuoi = DateTime.UtcNow;

                    if (needsRehash)
                    {
                        user.MatKhauHash = _passwordService.HashPassword(password);
                    }

                    _store.Update(user);
                    await _unitOfWork.SaveChangesAsync();

                    return new UserAuthDto
                    {
                        NguoiDungId = user.NguoiDungId,
                        TenDangNhap = user.TenDangNhap,
                        Role = (AppRole)(int)user.Role,
                        NguoiThueId = user.NguoiThueId,
                        HoVaTen = user.NguoiThue?.HoVaTen,
                        Email = user.NguoiThue?.Email,
                        IsActive = user.IsActive
                    };
                }
            }

            return null;
        }

        // --- CÁC CHỨC NĂNG CRUD ---

        public async Task<IEnumerable<NguoiDungRes>> GetAllAsync()
        {
            var users = await _store.GetAllActiveWithTenantAsync();
            return users.Select(u => new NguoiDungRes
            {
                NguoiDungId = u.NguoiDungId,
                TenDangNhap = u.TenDangNhap,
                Role = (AppRole)(int)u.Role,
                IsActive = u.IsActive,
                NgayTao = u.NgayTao,
                LanDangNhapCuoi = u.LanDangNhapCuoi,
                NguoiThueId = u.NguoiThueId,
                TenNguoiThue = u.NguoiThue?.HoVaTen
            });
        }

        public async Task<NguoiDungRes?> GetByIdAsync(int id)
        {
            var u = await _store.GetActiveWithTenantByIdAsync(id);
            if (u == null) return null;

            return new NguoiDungRes
            {
                NguoiDungId = u.NguoiDungId,
                TenDangNhap = u.TenDangNhap,
                Role = (AppRole)(int)u.Role,
                IsActive = u.IsActive,
                NgayTao = u.NgayTao,
                LanDangNhapCuoi = u.LanDangNhapCuoi,
                NguoiThueId = u.NguoiThueId,
                TenNguoiThue = u.NguoiThue?.HoVaTen
            };
        }

        public async Task<ServiceResult> AddAsync(CreateNguoiDungReq req)
        {
            // 1. Kiểm tra tài khoản đang hoạt động (chưa bị xóa) xem có bị trùng TenDangNhap hoặc NguoiThueId không
            var activeUsernameMatch = await _store.ExistsActiveUsernameAsync(req.TenDangNhap);
            if (activeUsernameMatch)
            {
                return ServiceResult.Fail("Tên đăng nhập đã tồn tại trên hệ thống. Vui lòng chọn tên đăng nhập khác.");
            }

            var domainRole = (Role)(int)req.Role;

            if (domainRole == Role.KhachThue && req.NguoiThueId.HasValue)
            {
                var activeTenantMatch = await _store.ExistsActiveTenantAccountAsync(req.NguoiThueId.Value);
                if (activeTenantMatch)
                {
                    return ServiceResult.Fail("Khách thuê này đã có tài khoản đăng nhập đang hoạt động trên hệ thống.");
                }
            }

            // 2. Kiểm tra tài khoản bị xóa mềm (IsDeleted == true) theo NguoiThueId hoặc TenDangNhap
            NguoiDung? softDeletedUser = null;
            if (domainRole == Role.KhachThue && req.NguoiThueId.HasValue)
            {
                softDeletedUser = await _store.GetSoftDeletedByTenantIdAsync(req.NguoiThueId.Value);
            }

            if (softDeletedUser == null)
            {
                softDeletedUser = await _store.GetSoftDeletedByUsernameAsync(req.TenDangNhap);
            }

            // 3. Nếu tìm thấy tài khoản bị xóa mềm -> Khôi phục và cập nhật thông tin mới
            if (softDeletedUser != null)
            {
                softDeletedUser.TenDangNhap = req.TenDangNhap;
                softDeletedUser.Role = domainRole;
                softDeletedUser.IsActive = true;
                softDeletedUser.NguoiThueId = req.NguoiThueId;
                softDeletedUser.IsDeleted = false;

                if (!string.IsNullOrEmpty(req.MatKhau))
                {
                    softDeletedUser.MatKhauHash = _passwordService.HashPassword(req.MatKhau);
                }

                _store.Update(softDeletedUser);
                await _unitOfWork.SaveChangesAsync();
                return ServiceResult.Ok("Tài khoản đã được khôi phục và tạo lại thành công!");
            }

            // 4. Nếu không có bản ghi cũ -> Thêm mới hoàn toàn
            var nguoiDung = new NguoiDung
            {
                TenDangNhap = req.TenDangNhap,
                Role = domainRole,
                IsActive = true,
                NguoiThueId = req.NguoiThueId,
                NgayTao = DateTime.UtcNow
            };

            if (!string.IsNullOrEmpty(req.MatKhau))
            {
                nguoiDung.MatKhauHash = _passwordService.HashPassword(req.MatKhau);
            }

            await _store.AddAsync(nguoiDung);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult.Ok("Thêm mới tài khoản thành công!");
        }

        public async Task<ServiceResult> UpdateUserAsync(int id, AppRole role, bool isActive, string? newPassword, int? nguoiThueId)
        {
            var existingUser = await _store.GetActiveWithTenantByIdAsync(id);
            if (existingUser == null)
            {
                return ServiceResult.Fail("Tài khoản không tồn tại trên hệ thống.");
            }

            var domainRole = (Role)(int)role;

            if (domainRole == Role.KhachThue && nguoiThueId.HasValue)
            {
                bool tenantUsedByOther = await _store.ExistsActiveTenantAccountAsync(nguoiThueId.Value, id);
                if (tenantUsedByOther)
                {
                    return ServiceResult.Fail("Khách thuê này đã được liên kết với một tài khoản khác đang hoạt động.");
                }
            }

            existingUser.Role = domainRole;
            existingUser.IsActive = isActive;
            existingUser.NguoiThueId = nguoiThueId;

            if (!string.IsNullOrEmpty(newPassword))
            {
                existingUser.MatKhauHash = _passwordService.HashPassword(newPassword);
            }

            _store.Update(existingUser);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult.Ok("Cập nhật thông tin tài khoản thành công!");
        }

        public async Task<ServiceResult> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            var user = await _store.GetActiveWithTenantByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult.Fail("Tài khoản không tồn tại.");
            }

            if (!_passwordService.VerifyPassword(user.MatKhauHash, oldPassword))
            {
                return ServiceResult.Fail("Mật khẩu hiện tại không chính xác.");
            }

            user.MatKhauHash = _passwordService.HashPassword(newPassword);
            _store.Update(user);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult.Ok("Đổi mật khẩu thành công!");
        }

        public async Task<ServiceResult> ResetPasswordToPhoneAsync(int id)
        {
            var user = await _store.GetActiveWithTenantByIdAsync(id);

            if (user == null || user.Role != Role.KhachThue || user.NguoiThue == null || string.IsNullOrEmpty(user.NguoiThue.SoDienThoai))
            {
                return ServiceResult.Fail("Không thể đặt lại mật khẩu. Tài khoản không phải là Khách Thuê hoặc không có số điện thoại hợp lệ.");
            }

            user.MatKhauHash = _passwordService.HashPassword(user.NguoiThue.SoDienThoai);
            _store.Update(user);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult.Ok("Đặt lại mật khẩu thành số điện thoại thành công!");
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            var user = await _store.GetByIdAsync(id);
            if (user != null)
            {
                user.IsDeleted = true;
                user.IsActive = false;

                _store.Update(user);
                await _unitOfWork.SaveChangesAsync();
                return ServiceResult.Ok("Đã xóa tài khoản ra khỏi hệ thống quản lý.");
            }
            return ServiceResult.Fail("Không tìm thấy tài khoản để xóa.");
        }
    }
}