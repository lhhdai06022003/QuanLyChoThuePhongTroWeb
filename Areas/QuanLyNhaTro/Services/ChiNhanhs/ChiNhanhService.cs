using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ChiNhanhs
{
    public class ChiNhanhService : IChiNhanhService
    {
        private readonly ApplicationDbContext _context;

        public ChiNhanhService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ChiNhanh>> DanhSachChiNhanh()
        {
            return await _context.ChiNhanhs
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.NgayTao)
                .ToListAsync();
        }

        public async Task<ChiNhanh> GetChiNhanh(int id)
        {
            return await _context.ChiNhanhs
                .FirstOrDefaultAsync(c => c.ChiNhanhId == id && !c.IsDeleted);
        }
        public async Task<(bool IsSuccess, string Message)> ThemChiNhanhMoi(ChiNhanh model)
        {
            try
            {
                if (model.ChiNhanhId == 0) // LÀ THÊM MỚI
                {
                    model.NgayTao = DateTime.UtcNow;
                    _context.ChiNhanhs.Add(model);
                    await _context.SaveChangesAsync();
                    return (true, "Thêm chi nhánh thành công!");
                }
                else // LÀ CẬP NHẬT
                {
                    var existing = await _context.ChiNhanhs.FindAsync(model.ChiNhanhId);
                    if (existing == null || existing.IsDeleted)
                    {
                        return (false, "Lỗi: Không tìm thấy chi nhánh hoặc đã bị xóa!");
                    }

                    // Map dữ liệu
                    existing.TenChiNhanh = model.TenChiNhanh;
                    existing.DiaChi = model.DiaChi;
                    existing.SoDienThoai = model.SoDienThoai;
                    existing.MoTa = model.MoTa;
                    existing.NgayCapNhat = DateTime.UtcNow;

                    _context.ChiNhanhs.Update(existing);
                    await _context.SaveChangesAsync();
                    return (true, "Cập nhật chi nhánh thành công!");
                }
            }
            catch (Exception ex)
            {
                // Ở đây sau này có thể thêm thư viện ILogger để ghi log lỗi vào file
                return (false, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        // XỬ LÝ LOGIC XÓA MỀM
        public async Task<(bool IsSuccess, string Message)> XoaChiNhanh(int id)
        {
            try
            {
                var chiNhanh = await _context.ChiNhanhs.FindAsync(id);
                if (chiNhanh == null || chiNhanh.IsDeleted)
                {
                    return (false, "Không tìm thấy chi nhánh để xóa!");
                }

                chiNhanh.IsDeleted = true;
                chiNhanh.NgayCapNhat = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return (true, "Đã xóa chi nhánh thành công!");
            }
            catch (Exception ex)
            {
                return (false, $"Không thể xóa: {ex.Message}");
            }
        }
    }
}
