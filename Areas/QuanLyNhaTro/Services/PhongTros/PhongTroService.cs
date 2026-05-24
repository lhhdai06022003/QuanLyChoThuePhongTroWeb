using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros
{
    public class PhongTroService : IPhongTroService
    {
        private readonly ApplicationDbContext _context;

        public PhongTroService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<SelectListItem>> GetDanhSachChiNhanhDropdownAsync()
        {
            return await _context.ChiNhanhs
                .Where(c => !c.IsDeleted)
                .Select(c => new SelectListItem { Value = c.ChiNhanhId.ToString(), Text = c.TenChiNhanh })
                .ToListAsync();
        }

        public async Task<object> GetDanhSachPhongTroAsync()
        {
            // Trả về object ẩn danh khớp với cột của DataTable
            return await _context.PhongTros
                .Include(p => p.ChiNhanh)
                .Where(p => !p.IsDeleted)
                .Select(p => new
                {
                    p.PhongTroId,
                    p.ChiNhanh.TenChiNhanh,
                    p.SoPhong,
                    p.TangLau,
                    p.GiaThue,
                    p.DienTich,
                    TrangThai = (int)p.TrangThai
                })
                .OrderByDescending(p => p.PhongTroId)
                .ToListAsync();
        }

        public async Task<ServiceResult> GetPhongTroByIdAsync(int id)
        {
            var phong = await _context.PhongTros.FirstOrDefaultAsync(p => p.PhongTroId == id && !p.IsDeleted);
            if (phong == null)
                return ServiceResult.Fail("Không tìm thấy phòng trọ!");

            return ServiceResult.Ok("Thành công", phong);
        }

        public async Task<ServiceResult> ThemPhongTroAsync(PhongTro model)
        {
            // Validation Logic
            if (string.IsNullOrWhiteSpace(model.SoPhong))
                return ServiceResult.Fail("Số phòng không được để trống!");

            if (model.ChiNhanhId <= 0)
                return ServiceResult.Fail("Vui lòng chọn chi nhánh!");

            bool isExist = await _context.PhongTros.AnyAsync(p => p.SoPhong == model.SoPhong && p.ChiNhanhId == model.ChiNhanhId && !p.IsDeleted);
            if (isExist)
                return ServiceResult.Fail("Số phòng này đã tồn tại trong chi nhánh!");

            model.NgayTao = DateTime.UtcNow;
            model.IsDeleted = false;

            _context.PhongTros.Add(model);
            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Thêm phòng trọ thành công!");
        }

        public async Task<ServiceResult> CapNhatPhongTroAsync(PhongTro model)
        {
            if (model.PhongTroId <= 0)
                return ServiceResult.Fail("ID không hợp lệ!");

            var phongTonTai = await _context.PhongTros.FirstOrDefaultAsync(p => p.PhongTroId == model.PhongTroId && !p.IsDeleted);
            if (phongTonTai == null)
                return ServiceResult.Fail("Dữ liệu không tồn tại hoặc đã bị xóa!");

            bool isExist = await _context.PhongTros.AnyAsync(p => p.SoPhong == model.SoPhong && p.ChiNhanhId == model.ChiNhanhId && p.PhongTroId != model.PhongTroId && !p.IsDeleted);
            if (isExist)
                return ServiceResult.Fail("Số phòng này đã tồn tại trong chi nhánh!");

            // Cập nhật dữ liệu
            phongTonTai.ChiNhanhId = model.ChiNhanhId;
            phongTonTai.SoPhong = model.SoPhong;
            phongTonTai.TangLau = model.TangLau;
            phongTonTai.GiaThue = model.GiaThue;
            phongTonTai.DienTich = model.DienTich;
            phongTonTai.SoNguoiToiDa = model.SoNguoiToiDa;
            phongTonTai.TrangThai = model.TrangThai;
            phongTonTai.MoTa = model.MoTa;
            phongTonTai.NgayCapNhat = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ServiceResult.Ok("Cập nhật phòng trọ thành công!");
        }

        public async Task<ServiceResult> XoaPhongTroAsync(int id)
        {
            var phong = await _context.PhongTros.FirstOrDefaultAsync(p => p.PhongTroId == id && !p.IsDeleted);
            if (phong == null)
                return ServiceResult.Fail("Không tìm thấy phòng trọ!");

            // Xóa mềm
            phong.IsDeleted = true;
            phong.NgayCapNhat = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ServiceResult.Ok("Xóa phòng trọ thành công!");
        }
        public async Task<List<PhongTro>> DanhSachPhongTroConTrong()
        {
            return await _context.PhongTros
                .Where(p => !p.IsDeleted)
                .Select(p => new PhongTro 
                { 
                    PhongTroId = p.PhongTroId, 
                    SoPhong = p.SoPhong,
                    ChiNhanhId = p.ChiNhanhId ,
                    TrangThai = p.TrangThai
                })
                .ToListAsync();
        }

    }
}
