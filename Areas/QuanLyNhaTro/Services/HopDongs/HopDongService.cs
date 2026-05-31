using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HopDongs
{
    public class HopDongService : IHopDongService
    {
        private readonly ApplicationDbContext _context;

        public HopDongService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DataTableResponse<HopDongRes>> DanhSachHopDongSideAsync(HopDongFilterReq request)
        {
            // 1. Tạo Query gốc (Chưa thực thi)
            try
            {
                IQueryable<HopDong> query = _context.HopDongs
                .Include(x => x.PhongTro)
                .Include(x => x.NguoiThue)
                .Where(x => !x.IsDeleted);

                // 2. Đếm tổng số bản ghi gốc (trước khi lọc)
                int totalRecords = await query.CountAsync();

                // 3. Xử lý Lọc dữ liệu (Custom Filters từ ViewBag)
                if (request.ChiNhanhId.HasValue && request.ChiNhanhId > 0)
                {
                    // Giả sử PhongTro có ChiNhanhId
                    query = query.Where(x => x.PhongTro.ChiNhanhId == request.ChiNhanhId.Value);
                }
                if (request.NguoiThueId.HasValue && request.NguoiThueId > 0)
                {
                    query = query.Where(x => x.NguoiThueId == request.NguoiThueId.Value);
                }
                if (request.TrangThai.HasValue && request.TrangThai > 0)
                {
                    query = query.Where(x => (int)x.TrangThaiHopDong == request.TrangThai.Value);
                }
                if (request.StartDate.HasValue && request.EndDate.HasValue)
                {
                    var startUtc = DateTime.SpecifyKind(request.StartDate.Value.Date, DateTimeKind.Utc);
                    var endUtc = DateTime.SpecifyKind(request.EndDate.Value.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.ThoiDiemBatDau >= startUtc && x.ThoiDiemBatDau <= endUtc);
                }

                // 4. Xử lý Tìm kiếm toàn cục (Global Search) từ ô Search mặc định của DataTable
                if (!string.IsNullOrEmpty(request.SearchValue))
                {
                    string searchValue = request.SearchValue.ToLower();
                    query = query.Where(x =>
                        x.MaHopDong.ToLower().Contains(searchValue) ||
                        x.PhongTro.SoPhong.ToLower().Contains(searchValue) ||
                        x.NguoiThue.HoVaTen.ToLower().Contains(searchValue));
                }

                // 5. Đếm số bản ghi sau khi lọc (Dùng cho phân trang)
                int filteredRecords = await query.CountAsync();

                // 6. Xử lý Sắp xếp (Sorting)
                if (!string.IsNullOrEmpty(request.SortColumnName))
                {
                    var sortColumnData = request.SortColumnName; // Lấy tên cột được click trên UI
                    bool isAscending = request.SortDirection.ToLower() == "asc";

                    // Tùy biến sắp xếp (Cẩn thận để khớp với 'data' config ở JS)
                    query = sortColumnData switch
                    {
                        "maHopDong" => isAscending ? query.OrderBy(x => x.MaHopDong) : query.OrderByDescending(x => x.MaHopDong),
                        "soPhong" => isAscending ? query.OrderBy(x => x.PhongTro.SoPhong) : query.OrderByDescending(x => x.PhongTro.SoPhong),
                        "tienThuePhong" => isAscending ? query.OrderBy(x => x.TienThuePhong) : query.OrderByDescending(x => x.TienThuePhong),
                        "thoiDiemBatDau" => isAscending ? query.OrderBy(x => x.ThoiDiemBatDau) : query.OrderByDescending(x => x.ThoiDiemBatDau),
                        _ => query.OrderByDescending(x => x.NgayTao) // Mặc định
                    };
                }
                else
                {
                    query = query.OrderByDescending(x => x.NgayTao); // Mặc định nếu UI không sort
                }

                // 7. Xử lý Phân trang (Pagination)
                // Chặn query tải toàn bộ DB, chỉ tải số lượng cần thiết
                var data = await query
                    .Select(x => new HopDongRes
                    {
                        HopDongId = x.HopDongId,
                        MaHopDong = x.MaHopDong,
                        SoPhong = x.PhongTro.SoPhong,
                        TenNguoiThue =x.NguoiThue.HoVaTen,
                        ThoiDiemBatDau = x.ThoiDiemBatDau,
                        ThoiDiemKetThuc = x.ThoiDiemKetThuc,
                        TienCocPhong = x.TienCocPhong,
                        TienThuePhong = x.TienThuePhong,
                        TrangThaiHopDong = x.TrangThaiHopDong,
                        TenChiNhanh = x.PhongTro.ChiNhanh.TenChiNhanh
                    })
                    .Skip(request.Start)
                    .Take(request.Length)
                    .ToListAsync();

                // 8. Trả về đúng format DataTables cần
                return new DataTableResponse<HopDongRes>
                {
                    draw = request.Draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = filteredRecords,
                    data = data
                };
            }
            catch (Exception e)
            {

                throw;
            }
        }

        public async Task<HopDong> GetByIdAsync(int id)
        {
            return await _context.HopDongs
                .Include(x => x.PhongTro)
                .Include(x => x.NguoiThue)
                .Include(x => x.ChiTietThanhVienHopDongs.Where(ct => !ct.IsDeleted))
                    .ThenInclude(ct => ct.NguoiThue)
                .FirstOrDefaultAsync(x => x.HopDongId == id && !x.IsDeleted);
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> CreateAsync(HopDongReq input)
        {
            // 1. Kiểm tra Logic Ngày tháng
            if (input.ThoiDiemKetThuc.HasValue && input.ThoiDiemKetThuc.Value.Date < input.ThoiDiemBatDau.Date)
            {
                return (false, "Ngày kết thúc không được trước ngày bắt đầu.");
            }

            // 2. Kiểm tra mã hợp đồng trùng lặp
            bool isDuplicateMa = await _context.HopDongs.AnyAsync(x => x.MaHopDong == input.MaHopDong && !x.IsDeleted);
            if (isDuplicateMa) return (false, "Mã hợp đồng đã tồn tại trong hệ thống.");

            // 3. Kiểm tra phòng trọ có đang được thuê không
            bool isPhongDangThue = await _context.HopDongs.AnyAsync(x =>
                x.PhongTroId == input.PhongTroId &&
                x.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                !x.IsDeleted);
            if (isPhongDangThue) return (false, "Phòng trọ này hiện đang có hợp đồng hiệu lực.");

            // Sử dụng Transaction để bảo đảm thêm Hợp đồng và Chi tiết thành viên cùng thành công
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var hopDong = new HopDong
                {
                    MaHopDong = input.MaHopDong,
                    PhongTroId = input.PhongTroId,
                    NguoiThueId = input.NguoiThueId,
                    ThoiDiemBatDau = input.ThoiDiemBatDau.ToUniversalTime(), // PostgreSQL yêu cầu UTC
                    ThoiDiemKetThuc = input.ThoiDiemKetThuc?.ToUniversalTime(),
                    TienCocPhong = input.TienCocPhong,
                    TienThuePhong = input.TienThuePhong,
                    TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                    NgayTao = DateTime.UtcNow,
                    IsDeleted = false
                };

                _context.HopDongs.Add(hopDong);
                await _context.SaveChangesAsync(); // Lưu để lấy HopDongId

                // Thêm người đại diện vào bảng chi tiết thành viên nếu họ có ở
                var members = new List<ChiTietThanhVienHopDong>();
                if (input.NguoiDungCoOPhongKhong)
                {
                    members.Add(new ChiTietThanhVienHopDong
                    {
                        HopDongId = hopDong.HopDongId,
                        NguoiThueId = input.NguoiThueId, // Chủ hợp đồng
                        NgayVao = hopDong.ThoiDiemBatDau,
                        IsDeleted = false
                    });
                }

                // Thêm các thành viên ở ghép (nếu có)
                if (input.ThanhVienKhacIds != null && input.ThanhVienKhacIds.Any())
                {
                    var uniqueMembers = input.ThanhVienKhacIds.Where(id => id != input.NguoiThueId).Distinct();
                    foreach (var memberId in uniqueMembers)
                    {
                        members.Add(new ChiTietThanhVienHopDong
                        {
                            HopDongId = hopDong.HopDongId,
                            NguoiThueId = memberId,
                            NgayVao = hopDong.ThoiDiemBatDau,
                            IsDeleted = false
                        });
                    }
                }

                if (members.Any())
                {
                    _context.ChiTietThanhVienHopDongs.AddRange(members);
                }

                // Cập nhật trạng thái của PhongTro thành "Đã cho thuê"
                var phong = await _context.PhongTros.FindAsync(input.PhongTroId);
                if (phong != null)
                {
                    phong.TrangThai = TrangThaiPhong.DaThue;
                    phong.NgayCapNhat = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, string.Empty);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                string errorDetails = e.InnerException != null ? e.InnerException.Message : e.Message;
                return (false, $"Lỗi hệ thống khi tạo hợp đồng: {errorDetails}");
            }
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> UpdateAsync(int id, HopDongReq input)
        {
            var entity = await _context.HopDongs.FirstOrDefaultAsync(x => x.HopDongId == id && !x.IsDeleted);
            if (entity == null) return (false, "Không tìm thấy hợp đồng.");

            if (input.ThoiDiemKetThuc.HasValue && input.ThoiDiemKetThuc.Value.Date < input.ThoiDiemBatDau.Date)
                return (false, "Ngày kết thúc không được trước ngày bắt đầu.");

            bool isDuplicateMa = await _context.HopDongs.AnyAsync(x => x.MaHopDong == input.MaHopDong && x.HopDongId != id && !x.IsDeleted);
            if (isDuplicateMa) return (false, "Mã hợp đồng bị trùng với hợp đồng khác.");

            // Cập nhật trạng thái phòng trọ nếu trạng thái hợp đồng thay đổi
            if (entity.TrangThaiHopDong != input.TrangThaiHopDong)
            {
                var phong = await _context.PhongTros.FindAsync(entity.PhongTroId);
                if (phong != null)
                {
                    if (input.TrangThaiHopDong == TrangThaiHopDong.DaKetThuc || input.TrangThaiHopDong == TrangThaiHopDong.DaHuy)
                    {
                        phong.TrangThai = TrangThaiPhong.Trong;

                        // Tự động check-out các thành viên đang ở
                        var activeMembers = await _context.ChiTietThanhVienHopDongs
                            .Where(x => x.HopDongId == id && x.NgayChuyenDi == null && !x.IsDeleted)
                            .ToListAsync();
                        foreach (var member in activeMembers)
                        {
                            member.NgayChuyenDi = DateTime.UtcNow;
                        }
                    }
                    else if (input.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
                    {
                        phong.TrangThai = TrangThaiPhong.DaThue;
                    }
                    phong.NgayCapNhat = DateTime.UtcNow;
                }
            }

            entity.MaHopDong = input.MaHopDong;
            // Thông thường không cho phép đổi Phòng hoặc Chủ Hợp đồng khi đang active, nếu cần bạn có thể xử lý thêm logic ở đây.
            entity.ThoiDiemBatDau = input.ThoiDiemBatDau.ToUniversalTime();
            entity.ThoiDiemKetThuc = input.ThoiDiemKetThuc?.ToUniversalTime();
            entity.TienCocPhong = input.TienCocPhong;
            entity.TienThuePhong = input.TienThuePhong;
            entity.TrangThaiHopDong = input.TrangThaiHopDong;
            entity.NgayCapNhat = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> DeleteAsync(int id)
        {
            var entity = await _context.HopDongs.FirstOrDefaultAsync(x => x.HopDongId == id && !x.IsDeleted);
            if (entity == null) return (false, "Không tìm thấy hợp đồng.");

            if (entity.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
            {
                return (false, "Không thể xóa hợp đồng đang trong trạng thái Hoạt động. Vui lòng thanh lý hợp đồng trước.");
            }

            entity.IsDeleted = true;
            entity.NgayCapNhat = DateTime.UtcNow;

            // Tự động check-out các thành viên liên quan (đang ở)
            var thanhViens = await _context.ChiTietThanhVienHopDongs.Where(x => x.HopDongId == id && !x.IsDeleted && x.NgayChuyenDi == null).ToListAsync();
            foreach (var tv in thanhViens)
            {
                tv.NgayChuyenDi = DateTime.UtcNow;
            }

            // Tự động chuyển trạng thái phòng về "Trống" nếu không còn HĐ hoạt động nào khác
            bool conHopDongKhac = await _context.HopDongs.AnyAsync(x =>
                x.PhongTroId == entity.PhongTroId &&
                x.HopDongId != id &&
                x.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                !x.IsDeleted);

            if (!conHopDongKhac)
            {
                var phong = await _context.PhongTros.FindAsync(entity.PhongTroId);
                if (phong != null)
                {
                    phong.TrangThai = TrangThaiPhong.Trong;
                    phong.NgayCapNhat = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            return (true, string.Empty);
        }
    }
}
