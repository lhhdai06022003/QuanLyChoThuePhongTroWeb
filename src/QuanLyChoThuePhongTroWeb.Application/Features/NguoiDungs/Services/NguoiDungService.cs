using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
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
        private readonly AdminSeedOptions _adminSeedOptions;
        private readonly ILoginAttemptThrottle _loginThrottle;

        // Hash giả để tên đăng nhập không tồn tại vẫn tốn một phép băm, thời gian phản hồi gần bằng tên có thật.
        private static string? _dummyHash;

        public NguoiDungService(
            INguoiDungStore store,
            IUnitOfWork unitOfWork,
            IPasswordService passwordService,
            AdminSeedOptions adminSeedOptions,
            ILoginAttemptThrottle loginThrottle)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _passwordService = passwordService;
            _adminSeedOptions = adminSeedOptions;
            _loginThrottle = loginThrottle;
        }

        // --- CHỨC NĂNG ĐĂNG NHẬP & SEED DATA ---

        // Chỉ tạo admin khi người vận hành cấp AdminSeed:Password; không có mật khẩu mặc định (spec bảo mật xác thực §2.1).
        public async Task<SeedAdminResult> SeedAdminAccountAsync()
        {
            var admin = await _store.GetActiveByUsernameAsync("admin");
            if (admin != null)
            {
                // Hash SHA256 cũ được nâng cấp khi chính người đó đăng nhập (needsRehash), không đặt lại ở đây.
                return SeedAdminResult.DaCo;
            }

            if (string.IsNullOrEmpty(_adminSeedOptions.Password))
            {
                return SeedAdminResult.ThieuMatKhau;
            }

            admin = new NguoiDung
            {
                TenDangNhap = "admin",
                Role = Role.Admin,
                IsActive = true,
                NgayTao = DateTime.UtcNow
            };
            admin.MatKhauHash = _passwordService.HashPassword(_adminSeedOptions.Password);
            await _store.AddAsync(admin);
            await _unitOfWork.SaveChangesAsync();
            return SeedAdminResult.DaTao;
        }

        public async Task<UserAuthDto?> ValidateUserAsync(string username, string password)
        {
            var result = await LoginAsync(username, password, null);
            return result.Status == LoginStatus.ThanhCong ? result.User : null;
        }

        public async Task<LoginResult> LoginAsync(string tenDangNhap, string matKhau, string? clientKey)
        {
            tenDangNhap ??= string.Empty;
            matKhau ??= string.Empty;

            // Đang khóa: từ chối ngay, không kiểm mật khẩu (kể cả mật khẩu đúng).
            var lockRemaining = _loginThrottle.GetLockRemaining(tenDangNhap, clientKey);
            if (lockRemaining.HasValue)
            {
                return LoginResult.TamKhoa(Math.Max(1, (int)Math.Ceiling(lockRemaining.Value.TotalMinutes)));
            }

            var user = await _store.GetActiveByUsernameAsync(tenDangNhap);
            if (user == null || !user.IsActive)
            {
                _passwordService.VerifyPassword(GetDummyHash(), matKhau, out _);
                _loginThrottle.RecordFailure(tenDangNhap, clientKey);
                return LoginResult.Sai();
            }

            if (!_passwordService.VerifyPassword(user.MatKhauHash, matKhau, out bool needsRehash))
            {
                _loginThrottle.RecordFailure(tenDangNhap, clientKey);
                return LoginResult.Sai();
            }

            _loginThrottle.RecordSuccess(tenDangNhap);
            user.LanDangNhapCuoi = DateTime.UtcNow;

            if (needsRehash)
            {
                user.MatKhauHash = _passwordService.HashPassword(matKhau);
            }

            _store.Update(user);
            await _unitOfWork.SaveChangesAsync();

            // Không áp cho khách vãng lai: họ chưa có màn đổi mật khẩu (spec §2.2).
            var batBuocDoiMatKhau = user.Role != Role.KhachVangLai &&
                PasswordPolicy.LaMatKhauMacDinh(
                    matKhau,
                    user.TenDangNhap,
                    user.Role == Role.KhachThue ? user.NguoiThue?.SoDienThoai : null);

            return LoginResult.ThanhCong(new UserAuthDto
            {
                NguoiDungId = user.NguoiDungId,
                TenDangNhap = user.TenDangNhap,
                Role = (AppRole)(int)user.Role,
                NguoiThueId = user.NguoiThueId,
                HoVaTen = user.NguoiThue?.HoVaTen ?? user.KhachVangLai?.HoTen,
                Email = user.NguoiThue?.Email ?? user.KhachVangLai?.Email,
                IsActive = user.IsActive,
                BatBuocDoiMatKhau = batBuocDoiMatKhau
            });
        }

        private string GetDummyHash()
        {
            return _dummyHash ??= _passwordService.HashPassword("TroSync-dummy-password-for-timing");
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

            // Tài khoản khách vãng lai cần hồ sơ KhachVangLai, chỉ tạo qua trang khách tự đăng ký.
            if (domainRole == Role.KhachVangLai)
            {
                return ServiceResult.Fail("Tài khoản khách vãng lai do khách tự đăng ký, không tạo ở màn quản lý tài khoản.");
            }

            // Áp chính sách cho cả nhánh tạo mới và nhánh khôi phục xóa mềm. Chưa nạp người thuê nên không so số điện thoại;
            // Admin đặt trùng số điện thoại thì khách bị ép đổi ở lần đăng nhập đầu.
            if (!string.IsNullOrEmpty(req.MatKhau))
            {
                var loiMatKhau = PasswordPolicy.Validate(req.MatKhau, req.TenDangNhap, null);
                if (loiMatKhau != null)
                {
                    return ServiceResult.Fail(loiMatKhau);
                }
            }

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

            if (domainRole == Role.KhachVangLai && existingUser.Role != Role.KhachVangLai)
            {
                return ServiceResult.Fail("Không thể chuyển tài khoản sang khách vãng lai; quyền này chỉ có khi khách tự đăng ký.");
            }

            if (domainRole == Role.KhachThue && nguoiThueId.HasValue)
            {
                bool tenantUsedByOther = await _store.ExistsActiveTenantAccountAsync(nguoiThueId.Value, id);
                if (tenantUsedByOther)
                {
                    return ServiceResult.Fail("Khách thuê này đã được liên kết với một tài khoản khác đang hoạt động.");
                }
            }

            if (!string.IsNullOrEmpty(newPassword))
            {
                var loiMatKhau = PasswordPolicy.Validate(newPassword, existingUser.TenDangNhap, existingUser.NguoiThue?.SoDienThoai);
                if (loiMatKhau != null)
                {
                    return ServiceResult.Fail(loiMatKhau);
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

            if (newPassword == oldPassword)
            {
                return ServiceResult.Fail("Mật khẩu mới phải khác mật khẩu hiện tại.");
            }

            var loiMatKhau = PasswordPolicy.Validate(newPassword, user.TenDangNhap, user.NguoiThue?.SoDienThoai);
            if (loiMatKhau != null)
            {
                return ServiceResult.Fail(loiMatKhau);
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
