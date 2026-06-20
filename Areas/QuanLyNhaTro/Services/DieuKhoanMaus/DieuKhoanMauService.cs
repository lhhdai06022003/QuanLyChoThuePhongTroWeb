using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DieuKhoanMaus
{
    public class DieuKhoanMauService : IDieuKhoanMauService
    {
        private readonly ApplicationDbContext _context;

        public DieuKhoanMauService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<DieuKhoanMauRes>> GetAllAsync()
        {
            return await _context.DieuKhoanMaus
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.NgayTao)
                .Select(x => new DieuKhoanMauRes
                {
                    DieuKhoanMauId = x.DieuKhoanMauId,
                    TieuDe = x.TieuDe,
                    NoiDung = x.NoiDung,
                    NgayTao = x.NgayTao,
                    NgayCapNhat = x.NgayCapNhat
                }).ToListAsync();
        }

        public async Task<DieuKhoanMauRes> GetByIdAsync(int id)
        {
            var entity = await _context.DieuKhoanMaus.FirstOrDefaultAsync(x => x.DieuKhoanMauId == id && !x.IsDeleted);
            if (entity == null) return null;

            return new DieuKhoanMauRes
            {
                DieuKhoanMauId = entity.DieuKhoanMauId,
                TieuDe = entity.TieuDe,
                NoiDung = entity.NoiDung,
                NgayTao = entity.NgayTao,
                NgayCapNhat = entity.NgayCapNhat
            };
        }

        public async Task<ServiceResult> CreateAsync(DieuKhoanMauReq request)
        {
            try
            {
                var entity = new DieuKhoanMau
                {
                    TieuDe = request.TieuDe,
                    NoiDung = request.NoiDung,
                    NgayTao = DateTime.UtcNow
                };
                _context.DieuKhoanMaus.Add(entity);
                await _context.SaveChangesAsync();
                return new ServiceResult { Success = true, Message = "Thêm điều khoản mẫu thành công" };
            }
            catch (Exception ex)
            {
                return new ServiceResult { Success = false, Message = "Có lỗi xảy ra: " + ex.Message };
            }
        }

        public async Task<ServiceResult> UpdateAsync(DieuKhoanMauReq request)
        {
            try
            {
                var entity = await _context.DieuKhoanMaus.FindAsync(request.DieuKhoanMauId);
                if (entity == null || entity.IsDeleted)
                    return new ServiceResult { Success = false, Message = "Không tìm thấy điều khoản" };

                entity.TieuDe = request.TieuDe;
                entity.NoiDung = request.NoiDung;
                entity.NgayCapNhat = DateTime.UtcNow;

                _context.DieuKhoanMaus.Update(entity);
                await _context.SaveChangesAsync();
                return new ServiceResult { Success = true, Message = "Cập nhật điều khoản mẫu thành công" };
            }
            catch (Exception ex)
            {
                return new ServiceResult { Success = false, Message = "Có lỗi xảy ra: " + ex.Message };
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                var entity = await _context.DieuKhoanMaus.FindAsync(id);
                if (entity == null || entity.IsDeleted)
                    return new ServiceResult { Success = false, Message = "Không tìm thấy điều khoản" };

                entity.IsDeleted = true;
                _context.DieuKhoanMaus.Update(entity);
                await _context.SaveChangesAsync();
                return new ServiceResult { Success = true, Message = "Xóa điều khoản mẫu thành công" };
            }
            catch (Exception ex)
            {
                return new ServiceResult { Success = false, Message = "Có lỗi xảy ra: " + ex.Message };
            }
        }
    }
}
