using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
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

        public async Task<IEnumerable<NguoiThue>> GetAvailableAsync()
        {
            // Lấy những người chưa bị xóa mềm VÀ:
            // 1. Không phải là chủ của bất kỳ hợp đồng nào đang hoạt động
            // 2. Không phải là thành viên đang ở của bất kỳ hợp đồng nào đang hoạt động
            return await _context.NguoiThues
                .Where(x => !x.IsDeleted)
                .Where(x => !x.HopDongs.Any(h => h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong))
                .Where(x => !x.ChiTietThanhVienHopDongs.Any(tv => tv.NgayChuyenDi == null && tv.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong))
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
            try
            {
                // 1. Kiểm tra trùng lặp CCCD hoặc SĐT (với những người chưa bị xóa)
                bool isDuplicate = await _context.NguoiThues.AnyAsync(x =>
                !x.IsDeleted &&
                (
                    x.CCCD == input.CCCD ||
                    x.SoDienThoai == input.SoDienThoai ||
                    // Chỉ kiểm tra trùng Email nếu input.Email có dữ liệu (khác null)
                    (!string.IsNullOrWhiteSpace(input.Email) && x.Email == input.Email)
                ));

                if (isDuplicate) return (false, "CCCD hoặc Số điện thoại đã tồn tại trong hệ thống.");

                // 2. Ép kiểu UTC cho PostgreSQL để tránh lỗi Múi giờ
                var entity = new NguoiThue
                {
                    HoVaTen = input.HoVaTen,
                    Email = input.Email,
                    SoDienThoai = input.SoDienThoai,
                    CCCD = input.CCCD,
                    NoiCapCCCD = input.NoiCapCCCD,
                    QueQuan = input.QueQuan,
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
            catch (Exception e)
            {

                // Lấy thông báo lỗi thật sự từ InnerException để dễ debug
                string errorDetails = e.InnerException != null ? e.InnerException.Message : e.Message;

                // Bạn nên dùng ILogger để ghi log ở đây thay vì trả về cho Client
                // _logger.LogError($"Lỗi tạo người thuê: {errorDetails}");

                return (false, $"Lỗi hệ thống: {errorDetails}"); 
            }
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
            entity.Email = input.Email;
            entity.SoDienThoai = input.SoDienThoai;
            entity.CCCD = input.CCCD;
            entity.NoiCapCCCD = input.NoiCapCCCD;
            entity.QueQuan = input.QueQuan;
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
        public async Task<List<SelectListItem>> DanhSachNguoiThue()
        {
            return await _context.NguoiThues
                .Where(p => !p.IsDeleted)
                .Select(p => new SelectListItem { Value = p.NguoiThueId.ToString(), Text = p.HoVaTen + " - " + p.CCCD })
                .ToListAsync();
        }
    }
}
