using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs
{
    public class NguoiDungService : INguoiDungService
    {
        private readonly ApplicationDbContext _context;

        public NguoiDungService(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- CHỨC NĂNG ĐĂNG NHẬP & SEED DATA (GIỮ NGUYÊN CỦA BẠN) ---

        public async Task SeedAdminAccountAsync()
        {
            if (!await _context.NguoiDungs.AnyAsync(x => !x.IsDeleted))
            {
                var admin = new NguoiDung
                {
                    TenDangNhap = "admin",
                    MatKhauHash = HashPassword("admin"),
                    Role = Role.Admin,
                    IsActive = true
                };
                _context.NguoiDungs.Add(admin);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<NguoiDung> ValidateUserAsync(string username, string password)
        {
            var hashPassword = HashPassword(password);

            var user = await _context.NguoiDungs
                .FirstOrDefaultAsync(x => x.TenDangNhap == username && x.MatKhauHash == hashPassword && x.IsActive && !x.IsDeleted);

            if (user != null)
            {
                // Cập nhật lần đăng nhập cuối
                user.LanDangNhapCuoi = DateTime.UtcNow;
                _context.NguoiDungs.Update(user);
                await _context.SaveChangesAsync();
            }

            return user;
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }


        // --- BỔ SUNG CÁC CHỨC NĂNG CRUD ---

        // 1. Lấy danh sách tất cả người dùng chưa bị xóa
        public async Task<IEnumerable<NguoiDung>> GetAllAsync()
        {
            return await _context.NguoiDungs
                .Where(x => !x.IsDeleted)
                .Include(x => x.NguoiThue) // Kèm theo thông tin người thuê nếu có
                .ToListAsync();
        }

        // 2. Lấy chi tiết một người dùng theo Id
        public async Task<NguoiDung> GetByIdAsync(int id)
        {
            return await _context.NguoiDungs
                .Include(x => x.NguoiThue)
                .FirstOrDefaultAsync(x => x.NguoiDungId == id && !x.IsDeleted);
        }

        // 3. Thêm mới người dùng (Tự động băm mật khẩu)
        public async Task AddAsync(NguoiDung nguoiDung)
        {
            if (!string.IsNullOrEmpty(nguoiDung.MatKhauHash))
            {
                // Sử dụng chính hàm HashPassword SHA256 của bạn để bảo mật
                nguoiDung.MatKhauHash = HashPassword(nguoiDung.MatKhauHash);
            }

            _context.NguoiDungs.Add(nguoiDung);
            await _context.SaveChangesAsync();
        }

        // 4. Cập nhật thông tin người dùng
        public async Task UpdateAsync(NguoiDung nguoiDung)
        {
            // Lưu ý: Nếu ở giao diện Edit bạn cho phép đổi mật khẩu, 
            // bạn nên xử lý băm mật khẩu trước khi truyền model vào hàm này.
            _context.NguoiDungs.Update(nguoiDung);
            await _context.SaveChangesAsync();
        }

        // 5. Xóa mềm người dùng (Soft Delete bằng cờ IsDeleted)
        public async Task DeleteAsync(int id)
        {
            var user = await _context.NguoiDungs.FindAsync(id);
            if (user != null)
            {
                user.IsDeleted = true;
                user.IsActive = false; // Hủy kích hoạt tài khoản luôn khi xóa

                _context.NguoiDungs.Update(user);
                await _context.SaveChangesAsync();
            }
        }
    }
}