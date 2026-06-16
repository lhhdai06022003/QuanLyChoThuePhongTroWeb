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
                    GhiChu = x.GhiChu,
                    MacDinh = x.MacDinh
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
            return new DichVuRes { DichVuId = dv.DichVuId, TenDichVu = dv.TenDichVu, DonVi = dv.DonVi, GhiChu = dv.GhiChu, MacDinh = dv.MacDinh };
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> CreateDichVuAsync(DichVuReq input)
        {
            bool exists = await _context.DichVus.AnyAsync(x => x.TenDichVu.ToLower() == input.TenDichVu.ToLower() && !x.IsDeleted);
            if (exists) return (false, "Tên dịch vụ đã tồn tại.");

            var dv = new DichVu
            {
                TenDichVu = input.TenDichVu,
                DonVi = input.DonVi,
                GhiChu = input.GhiChu,
                MacDinh = input.MacDinh
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
            dv.MacDinh = input.MacDinh;
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

        // ===== ĐĂNG KÝ DỊCH VỤ CHO PHÒNG =====

        public async Task<List<DichVuChiNhanhWithDangKyRes>> GetDichVuVaDangKyCuaPhongAsync(int phongTroId)
        {
            // Lấy ChiNhanhId từ phòng trọ
            var phong = await _context.PhongTros.FirstOrDefaultAsync(p => p.PhongTroId == phongTroId && !p.IsDeleted);
            if (phong == null) return new List<DichVuChiNhanhWithDangKyRes>();

            int chiNhanhId = phong.ChiNhanhId;

            // Lấy tất cả dịch vụ đang active của chi nhánh
            var dichVuChiNhanhs = await _context.Set<DichVuChiNhanh>()
                .Include(x => x.DichVu)
                .Where(x => x.ChiNhanhId == chiNhanhId && !x.IsDeleted && !x.DichVu.IsDeleted)
                .ToListAsync();

            // Lấy tất cả đăng ký hiện tại của phòng
            var dangKys = await _context.DangKyDichVus
                .Where(x => x.PhongTroId == phongTroId)
                .ToListAsync();

            // Map kết hợp
            var result = dichVuChiNhanhs.Select(dcn =>
            {
                var dk = dangKys.FirstOrDefault(d => d.DichVuChiNhanhId == dcn.DichVuChiNhanhId);
                return new DichVuChiNhanhWithDangKyRes
                {
                    DichVuChiNhanhId = dcn.DichVuChiNhanhId,
                    TenDichVu = dcn.DichVu.TenDichVu,
                    DonVi = dcn.DichVu.DonVi,
                    GiaDichVu = dcn.GiaDichVu,
                    MacDinh = dcn.DichVu.MacDinh,
                    IsSelected = dk != null,
                    SoLuong = dk?.SoLuong ?? 1,
                    DangKyDichVuId = dk?.DangKyDichVuId,
                    NgayBatDau = dk?.NgayBatDau
                };
            }).OrderByDescending(x => x.MacDinh).ThenBy(x => x.TenDichVu).ToList();

            return result;
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> LuuDangKyDichVuAsync(DangKyDichVuReq input)
        {
            var phong = await _context.PhongTros.FirstOrDefaultAsync(p => p.PhongTroId == input.PhongTroId && !p.IsDeleted);
            if (phong == null) return (false, "Phòng trọ không tồn tại.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Lấy tất cả đăng ký cũ của phòng
                var existingDangKys = await _context.DangKyDichVus
                    .Where(x => x.PhongTroId == input.PhongTroId)
                    .ToListAsync();

                foreach (var item in input.DichVus)
                {
                    var existing = existingDangKys.FirstOrDefault(x => x.DichVuChiNhanhId == item.DichVuChiNhanhId);

                    if (item.IsSelected)
                    {
                        if (existing != null)
                        {
                            // UPDATE: cập nhật số lượng
                            existing.SoLuong = item.SoLuong > 0 ? item.SoLuong : 1;
                            if (item.NgayBatDau.HasValue)
                            {
                                existing.NgayBatDau = DateTime.SpecifyKind(item.NgayBatDau.Value, DateTimeKind.Utc);
                            }
                        }
                        else
                        {
                            // INSERT: thêm mới
                            _context.DangKyDichVus.Add(new DangKyDichVu
                            {
                                PhongTroId = input.PhongTroId,
                                DichVuChiNhanhId = item.DichVuChiNhanhId,
                                SoLuong = item.SoLuong > 0 ? item.SoLuong : 1,
                                NgayBatDau = item.NgayBatDau.HasValue
                                    ? DateTime.SpecifyKind(item.NgayBatDau.Value, DateTimeKind.Utc)
                                    : DateTime.UtcNow
                            });
                        }
                    }
                    else
                    {
                        if (existing != null)
                        {
                            // DELETE: bỏ chọn → xóa cứng
                            _context.DangKyDichVus.Remove(existing);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, null);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                string errorDetails = e.InnerException != null ? e.InnerException.Message : e.Message;
                return (false, $"Lỗi hệ thống: {errorDetails}");
            }
        }
    }
}
