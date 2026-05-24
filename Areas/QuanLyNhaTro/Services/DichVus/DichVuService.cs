using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DichVus
{
    public class DichVuService : IDichVuService
    {
        private readonly ApplicationDbContext _context;

        public DichVuService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DataTableResponse<DichVuRes>> GetDanhSachDichVuAsync(DataTableRequest request)
        {
            var query = _context.DichVus.Where(x => !x.IsDeleted);

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string searchLower = request.SearchValue.ToLower();
                query = query.Where(x => x.TenDichVu.ToLower().Contains(searchLower));
            }

            int totalRecords = await query.CountAsync();
            int filteredRecords = totalRecords;

            // Sort
            if (!string.IsNullOrEmpty(request.SortColumnName))
            {
                if (request.SortColumnName == "tenDichVu")
                    query = request.SortDirection == "asc" ? query.OrderBy(x => x.TenDichVu) : query.OrderByDescending(x => x.TenDichVu);
                else
                    query = query.OrderByDescending(x => x.DichVuId);
            }
            else
            {
                query = query.OrderByDescending(x => x.DichVuId);
            }

            var data = await query
                .Select(x => new DichVuRes
                {
                    DichVuId = x.DichVuId,
                    TenDichVu = x.TenDichVu,
                    DonVi = x.DonVi,
                    GhiChu = x.GhiChu
                })
                .Skip(request.Start)
                .Take(request.Length)
                .ToListAsync();

            return new DataTableResponse<DichVuRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = filteredRecords,
                data = data
            };
        }

        public async Task<DichVuRes> GetDichVuByIdAsync(int id)
        {
            var dv = await _context.DichVus.FirstOrDefaultAsync(x => x.DichVuId == id && !x.IsDeleted);
            if (dv == null) return null;
            return new DichVuRes { DichVuId = dv.DichVuId, TenDichVu = dv.TenDichVu, DonVi = dv.DonVi, GhiChu = dv.GhiChu };
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> CreateDichVuAsync(DichVuReq input)
        {
            bool exists = await _context.DichVus.AnyAsync(x => x.TenDichVu.ToLower() == input.TenDichVu.ToLower() && !x.IsDeleted);
            if (exists) return (false, "Tên dịch vụ đã tồn tại.");

            var dv = new DichVu
            {
                TenDichVu = input.TenDichVu,
                DonVi = input.DonVi,
                GhiChu = input.GhiChu
            };
            _context.DichVus.Add(dv);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> UpdateDichVuAsync(int id, DichVuReq input)
        {
            var dv = await _context.DichVus.FirstOrDefaultAsync(x => x.DichVuId == id && !x.IsDeleted);
            if (dv == null) return (false, "Không tìm thấy dịch vụ.");

            bool exists = await _context.DichVus.AnyAsync(x => x.DichVuId != id && x.TenDichVu.ToLower() == input.TenDichVu.ToLower() && !x.IsDeleted);
            if (exists) return (false, "Tên dịch vụ đã tồn tại.");

            dv.TenDichVu = input.TenDichVu;
            dv.DonVi = input.DonVi;
            dv.GhiChu = input.GhiChu;
            dv.NgayCapNhat = DateTime.UtcNow;

            _context.DichVus.Update(dv);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> DeleteDichVuAsync(int id)
        {
            var dv = await _context.DichVus.FirstOrDefaultAsync(x => x.DichVuId == id && !x.IsDeleted);
            if (dv == null) return (false, "Không tìm thấy dịch vụ.");

            // Check if any active Branch Service exists
            bool inUse = await _context.Set<DichVuChiNhanh>().AnyAsync(x => x.DichVuId == id && !x.IsDeleted);
            if (inUse) return (false, "Dịch vụ đang được sử dụng ở chi nhánh. Vui lòng xóa bảng giá trước.");

            dv.IsDeleted = true;
            dv.NgayCapNhat = DateTime.UtcNow;
            _context.DichVus.Update(dv);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<DataTableResponse<DichVuChiNhanhRes>> GetDanhSachDichVuChiNhanhAsync(DataTableRequest request, int chiNhanhId)
        {
            var query = _context.Set<DichVuChiNhanh>()
                .Include(x => x.DichVu)
                .Include(x => x.ChiNhanh)
                .Where(x => !x.IsDeleted);

            if (chiNhanhId > 0)
            {
                query = query.Where(x => x.ChiNhanhId == chiNhanhId);
            }

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string searchLower = request.SearchValue.ToLower();
                query = query.Where(x => x.DichVu.TenDichVu.ToLower().Contains(searchLower) || x.ChiNhanh.TenChiNhanh.ToLower().Contains(searchLower));
            }

            int totalRecords = await query.CountAsync();
            int filteredRecords = totalRecords;

            query = query.OrderByDescending(x => x.DichVuChiNhanhId);

            var data = await query
                .Select(x => new DichVuChiNhanhRes
                {
                    DichVuChiNhanhId = x.DichVuChiNhanhId,
                    ChiNhanhId = x.ChiNhanhId,
                    TenChiNhanh = x.ChiNhanh.TenChiNhanh,
                    DichVuId = x.DichVuId,
                    TenDichVu = x.DichVu.TenDichVu,
                    GiaDichVu = x.GiaDichVu
                })
                .Skip(request.Start)
                .Take(request.Length)
                .ToListAsync();

            return new DataTableResponse<DichVuChiNhanhRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = filteredRecords,
                data = data
            };
        }

        public async Task<DichVuChiNhanhRes> GetDichVuChiNhanhByIdAsync(int id)
        {
            var dcn = await _context.Set<DichVuChiNhanh>()
                .Include(x => x.DichVu)
                .Include(x => x.ChiNhanh)
                .FirstOrDefaultAsync(x => x.DichVuChiNhanhId == id && !x.IsDeleted);
            if (dcn == null) return null;

            return new DichVuChiNhanhRes
            {
                DichVuChiNhanhId = dcn.DichVuChiNhanhId,
                ChiNhanhId = dcn.ChiNhanhId,
                TenChiNhanh = dcn.ChiNhanh.TenChiNhanh,
                DichVuId = dcn.DichVuId,
                TenDichVu = dcn.DichVu.TenDichVu,
                GiaDichVu = dcn.GiaDichVu
            };
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> CreateDichVuChiNhanhAsync(DichVuChiNhanhReq input)
        {
            bool exists = await _context.Set<DichVuChiNhanh>().AnyAsync(x => x.ChiNhanhId == input.ChiNhanhId && x.DichVuId == input.DichVuId && !x.IsDeleted);
            if (exists) return (false, "Dịch vụ này đã được cài đặt giá cho chi nhánh đã chọn.");

            var dcn = new DichVuChiNhanh
            {
                ChiNhanhId = input.ChiNhanhId,
                DichVuId = input.DichVuId,
                GiaDichVu = input.GiaDichVu
            };
            _context.Set<DichVuChiNhanh>().Add(dcn);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> UpdateDichVuChiNhanhAsync(int id, DichVuChiNhanhReq input)
        {
            var dcn = await _context.Set<DichVuChiNhanh>().FirstOrDefaultAsync(x => x.DichVuChiNhanhId == id && !x.IsDeleted);
            if (dcn == null) return (false, "Không tìm thấy bảng giá dịch vụ.");

            bool exists = await _context.Set<DichVuChiNhanh>().AnyAsync(x => x.DichVuChiNhanhId != id && x.ChiNhanhId == input.ChiNhanhId && x.DichVuId == input.DichVuId && !x.IsDeleted);
            if (exists) return (false, "Dịch vụ này đã được cài đặt giá cho chi nhánh đã chọn.");

            dcn.ChiNhanhId = input.ChiNhanhId;
            dcn.DichVuId = input.DichVuId;
            dcn.GiaDichVu = input.GiaDichVu;
            dcn.NgayCapNhat = DateTime.UtcNow;

            _context.Set<DichVuChiNhanh>().Update(dcn);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> DeleteDichVuChiNhanhAsync(int id)
        {
            var dcn = await _context.Set<DichVuChiNhanh>().FirstOrDefaultAsync(x => x.DichVuChiNhanhId == id && !x.IsDeleted);
            if (dcn == null) return (false, "Không tìm thấy bảng giá dịch vụ.");

            dcn.IsDeleted = true;
            dcn.NgayCapNhat = DateTime.UtcNow;
            _context.Set<DichVuChiNhanh>().Update(dcn);
            await _context.SaveChangesAsync();
            return (true, null);
        }
    }
}
