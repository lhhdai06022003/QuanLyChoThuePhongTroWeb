using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs
{
    public class NguoiDungService : INguoiDungService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<NguoiDung> _passwordHasher;

        public NguoiDungService(ApplicationDbContext context, IPasswordHasher<NguoiDung> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // --- CHỨC NĂNG ĐĂNG NHẬP & SEED DATA ---

        public async Task SeedAdminAccountAsync()
        {
            var admin = await _context.NguoiDungs.FirstOrDefaultAsync(x => x.TenDangNhap == "admin" && !x.IsDeleted);
            if (admin == null)
            {
                admin = new NguoiDung
                {
                    TenDangNhap = "admin",
                    Role = Role.Admin,
                    IsActive = true,
                    NgayTao = DateTime.UtcNow
                };
                admin.MatKhauHash = _passwordHasher.HashPassword(admin, "admin");
                _context.NguoiDungs.Add(admin);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Tự động nâng cấp mật khẩu của admin cũ từ SHA256 sang PBKDF2
                // SHA256 của "admin" = 8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918
                if (admin.MatKhauHash == "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918")
                {
                    admin.MatKhauHash = _passwordHasher.HashPassword(admin, "admin");
                    _context.NguoiDungs.Update(admin);
                    await _context.SaveChangesAsync();
                }
            }
        }

        public async Task<NguoiDung?> ValidateUserAsync(string username, string password)
        {
            var user = await _context.NguoiDungs
                .FirstOrDefaultAsync(x => x.TenDangNhap == username && x.IsActive && !x.IsDeleted);

            if (user != null)
            {
                bool isPasswordValid = false;
                bool needsRehash = false;

                // 1. Kiểm tra bằng IPasswordHasher (PBKDF2)
                var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.MatKhauHash, password);
                if (verificationResult == PasswordVerificationResult.Success)
                {
                    isPasswordValid = true;
                }
                else if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    isPasswordValid = true;
                    needsRehash = true;
                }
                else
                {
                    // 2. Thử so khớp bằng SHA256 cũ (đối với tài khoản chưa nâng cấp)
                    var oldSha256Hash = HashPasswordSha256(password);
                    if (user.MatKhauHash == oldSha256Hash)
                    {
                        isPasswordValid = true;
                        needsRehash = true; // Yêu cầu băm lại sang PBKDF2
                    }
                }

                if (isPasswordValid)
                {
                    // Cập nhật lần đăng nhập cuối
                    user.LanDangNhapCuoi = DateTime.UtcNow;

                    if (needsRehash)
                    {
                        user.MatKhauHash = _passwordHasher.HashPassword(user, password);
                    }

                    _context.NguoiDungs.Update(user);
                    await _context.SaveChangesAsync();
                    return user;
                }
            }

            return null;
        }

        private string HashPasswordSha256(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }


        // --- CÁC CHỨC NĂNG CRUD ---

        public async Task<IEnumerable<NguoiDung>> GetAllAsync()
        {
            return await _context.NguoiDungs
                .Where(x => !x.IsDeleted)
                .Include(x => x.NguoiThue)
                .ToListAsync();
        }

        public async Task<NguoiDung?> GetByIdAsync(int id)
        {
            return await _context.NguoiDungs
                .Include(x => x.NguoiThue)
                .FirstOrDefaultAsync(x => x.NguoiDungId == id && !x.IsDeleted);
        }

        public async Task<ServiceResult> AddAsync(NguoiDung nguoiDung)
        {
            // 1. Kiểm tra tài khoản đang hoạt động (chưa bị xóa) xem có bị trùng TenDangNhap hoặc NguoiThueId không
            var activeUsernameMatch = await _context.NguoiDungs.AnyAsync(x => !x.IsDeleted && x.TenDangNhap == nguoiDung.TenDangNhap);
            if (activeUsernameMatch)
            {
                return ServiceResult.Fail("Tên đăng nhập đã tồn tại trên hệ thống. Vui lòng chọn tên đăng nhập khác.");
            }

            if (nguoiDung.Role == Role.KhachThue && nguoiDung.NguoiThueId.HasValue)
            {
                var activeTenantMatch = await _context.NguoiDungs.AnyAsync(x => !x.IsDeleted && x.NguoiThueId == nguoiDung.NguoiThueId);
                if (activeTenantMatch)
                {
                    return ServiceResult.Fail("Khách thuê này đã có tài khoản đăng nhập đang hoạt động trên hệ thống.");
                }
            }

            // 2. Kiểm tra tài khoản bị xóa mềm (IsDeleted == true) theo NguoiThueId hoặc TenDangNhap
            NguoiDung? softDeletedUser = null;
            if (nguoiDung.Role == Role.KhachThue && nguoiDung.NguoiThueId.HasValue)
            {
                softDeletedUser = await _context.NguoiDungs.FirstOrDefaultAsync(x => x.IsDeleted && x.NguoiThueId == nguoiDung.NguoiThueId);
            }

            if (softDeletedUser == null)
            {
                softDeletedUser = await _context.NguoiDungs.FirstOrDefaultAsync(x => x.IsDeleted && x.TenDangNhap == nguoiDung.TenDangNhap);
            }

            // 3. Nếu tìm thấy tài khoản bị xóa mềm -> Khôi phục và cập nhật thông tin mới
            if (softDeletedUser != null)
            {
                softDeletedUser.TenDangNhap = nguoiDung.TenDangNhap;
                softDeletedUser.Role = nguoiDung.Role;
                softDeletedUser.IsActive = nguoiDung.IsActive;
                softDeletedUser.NguoiThueId = nguoiDung.NguoiThueId;
                softDeletedUser.IsDeleted = false;

                if (!string.IsNullOrEmpty(nguoiDung.MatKhauHash))
                {
                    softDeletedUser.MatKhauHash = _passwordHasher.HashPassword(softDeletedUser, nguoiDung.MatKhauHash);
                }

                _context.NguoiDungs.Update(softDeletedUser);
                await _context.SaveChangesAsync();
                return ServiceResult.Ok("Tài khoản đã được khôi phục và tạo lại thành công!");
            }

            // 4. Nếu không có bản ghi cũ -> Thêm mới hoàn toàn
            if (!string.IsNullOrEmpty(nguoiDung.MatKhauHash))
            {
                nguoiDung.MatKhauHash = _passwordHasher.HashPassword(nguoiDung, nguoiDung.MatKhauHash);
            }

            _context.NguoiDungs.Add(nguoiDung);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok("Thêm mới tài khoản thành công!");
        }

        public async Task<ServiceResult> UpdateAsync(NguoiDung nguoiDung)
        {
            _context.NguoiDungs.Update(nguoiDung);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok("Cập nhật thông tin tài khoản thành công!");
        }

        public async Task<ServiceResult> UpdateUserAsync(int id, Role role, bool isActive, string? newPassword, int? nguoiThueId)
        {
            var existingUser = await _context.NguoiDungs.FirstOrDefaultAsync(x => x.NguoiDungId == id && !x.IsDeleted);
            if (existingUser == null)
            {
                return ServiceResult.Fail("Tài khoản không tồn tại trên hệ thống.");
            }

            if (role == Role.KhachThue && nguoiThueId.HasValue)
            {
                bool tenantUsedByOther = await _context.NguoiDungs.AnyAsync(x => x.NguoiDungId != id && !x.IsDeleted && x.NguoiThueId == nguoiThueId);
                if (tenantUsedByOther)
                {
                    return ServiceResult.Fail("Khách thuê này đã được liên kết với một tài khoản khác đang hoạt động.");
                }
            }

            existingUser.Role = role;
            existingUser.IsActive = isActive;
            existingUser.NguoiThueId = nguoiThueId;

            if (!string.IsNullOrEmpty(newPassword))
            {
                existingUser.MatKhauHash = _passwordHasher.HashPassword(existingUser, newPassword);
            }

            _context.NguoiDungs.Update(existingUser);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok("Cập nhật thông tin tài khoản thành công!");
        }

        public async Task<ServiceResult> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.NguoiDungId == userId && !u.IsDeleted);
            if (user == null)
            {
                return ServiceResult.Fail("Tài khoản không tồn tại.");
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.MatKhauHash, oldPassword);
            bool isOldPasswordValid = false;

            if (verificationResult == PasswordVerificationResult.Success || verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                isOldPasswordValid = true;
            }
            else
            {
                var oldSha256Hash = HashPasswordSha256(oldPassword);
                if (user.MatKhauHash == oldSha256Hash)
                {
                    isOldPasswordValid = true;
                }
            }

            if (!isOldPasswordValid)
            {
                return ServiceResult.Fail("Mật khẩu hiện tại không chính xác.");
            }

            user.MatKhauHash = _passwordHasher.HashPassword(user, newPassword);
            _context.NguoiDungs.Update(user);
            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Đổi mật khẩu thành công!");
        }

        public async Task<ServiceResult> ResetPasswordToPhoneAsync(int id)
        {
            var user = await _context.NguoiDungs
                .Include(x => x.NguoiThue)
                .FirstOrDefaultAsync(x => x.NguoiDungId == id && !x.IsDeleted);

            if (user == null || user.Role != Role.KhachThue || user.NguoiThue == null || string.IsNullOrEmpty(user.NguoiThue.SoDienThoai))
            {
                return ServiceResult.Fail("Không thể đặt lại mật khẩu. Tài khoản không phải là Khách Thuê hoặc không có số điện thoại hợp lệ.");
            }

            user.MatKhauHash = _passwordHasher.HashPassword(user, user.NguoiThue.SoDienThoai);
            _context.NguoiDungs.Update(user);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok("Đặt lại mật khẩu thành số điện thoại thành công!");
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            var user = await _context.NguoiDungs.FindAsync(id);
            if (user != null)
            {
                user.IsDeleted = true;
                user.IsActive = false;

                _context.NguoiDungs.Update(user);
                await _context.SaveChangesAsync();
                return ServiceResult.Ok("Đã xóa tài khoản ra khỏi hệ thống quản lý.");
            }
            return ServiceResult.Fail("Không tìm thấy tài khoản để xóa.");
        }
    }
}