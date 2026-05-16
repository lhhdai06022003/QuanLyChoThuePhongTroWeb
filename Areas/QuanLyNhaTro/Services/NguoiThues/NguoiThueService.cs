using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiThues
{
    public class NguoiThueService : INguoiThueService
    {
        private readonly ApplicationDbContext _context;

        public NguoiThueService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<NguoiThue>> GetAllAsync()
        {
            // Chỉ lấy những người chưa bị xóa mềm
            return await _context.NguoiThues
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.NgayTao)
                .ToListAsync();
        }

        public async Task<NguoiThue> GetByIdAsync(int id)
        {
            return await _context.NguoiThues
                .FirstOrDefaultAsync(x => x.NguoiThueId == id && !x.IsDeleted);
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> CreateAsync(NguoiThueReq input)
        {
            // 1. Kiểm tra trùng lặp CCCD hoặc SĐT (với những người chưa bị xóa)
            bool isDuplicate = await _context.NguoiThues.AnyAsync(x =>
                !x.IsDeleted && (x.CCCD == input.CCCD || x.SoDienThoai == input.SoDienThoai));

            if (isDuplicate) return (false, "CCCD hoặc Số điện thoại đã tồn tại trong hệ thống.");

            // 2. Ép kiểu UTC cho PostgreSQL để tránh lỗi Múi giờ
            var entity = new NguoiThue
            {
                HoVaTen = input.HoVaTen,
                SoDienThoai = input.SoDienThoai,
                CCCD = input.CCCD,
                NoiCapCCCD = input.NoiCapCCCD,
                QueQUan = input.QueQuan,
                GhiChu = input.GhiChu,
                NgaySinh = input.NgaySinh?.ToUniversalTime(), // Bắt buộc cho PostgreSQL
                NgayCapCCCD = input.NgayCapCCCD?.ToUniversalTime(),
                NgayTao = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.NguoiThues.Add(entity);
            await _context.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> UpdateAsync(int id, NguoiThueUpdateDto input)
        {
            var entity = await _context.NguoiThues.FirstOrDefaultAsync(x => x.NguoiThueId == id && !x.IsDeleted);
            if (entity == null) return (false, "Không tìm thấy người thuê.");

            // Kiểm tra trùng lặp dữ liệu với người KHÁC
            bool isDuplicate = await _context.NguoiThues.AnyAsync(x =>
                x.NguoiThueId != id && !x.IsDeleted &&
                (x.CCCD == input.CCCD || x.SoDienThoai == input.SoDienThoai));

            if (isDuplicate) return (false, "CCCD hoặc Số điện thoại bị trùng với khách khác.");

            // Cập nhật dữ liệu
            entity.HoVaTen = input.HoVaTen;
            entity.SoDienThoai = input.SoDienThoai;
            entity.CCCD = input.CCCD;
            entity.NoiCapCCCD = input.NoiCapCCCD;
            entity.QueQUan = input.QueQuan;
            entity.GhiChu = input.GhiChu;
            entity.NgaySinh = input.NgaySinh?.ToUniversalTime();
            entity.NgayCapCCCD = input.NgayCapCCCD?.ToUniversalTime();
            entity.NgayCapNhat = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> DeleteAsync(int id)
        {
            var entity = await _context.NguoiThues
                .Include(x => x.HopDongs) // Kéo theo hợp đồng để kiểm tra
                .FirstOrDefaultAsync(x => x.NguoiThueId == id && !x.IsDeleted);

            if (entity == null) return (false, "Không tìm thấy người thuê.");

            // Kiểm tra ràng buộc: Nếu đang có hợp đồng Active thì không cho xóa
            if (entity.HopDongs != null && entity.HopDongs.Any(h => h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)) // Giả sử bạn có cờ TrangThai
            {
                return (false, "Không thể xóa do người thuê đang có hợp đồng hiệu lực.");
            }

            // Xóa mềm
            entity.IsDeleted = true;
            entity.NgayCapNhat = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return (true, string.Empty);
        }
    }
}
