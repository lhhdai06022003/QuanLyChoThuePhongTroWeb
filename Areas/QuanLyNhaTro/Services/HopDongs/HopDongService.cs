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
                    // Lọc hợp đồng theo Chi Nhánh thông qua bảng PhongTro liên kết
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

        /// <summary>
        /// Lấy chi tiết hợp đồng theo ID.
        /// Trả về DTO (HopDongDetailRes) thay vì Entity trực tiếp để:
        /// 1. Tránh Circular Reference khi serialize JSON (HopDong -> NguoiThue -> HopDongs -> ...)
        /// 2. Lọc bỏ các trường nhạy cảm (CCCD, Email, Token...) của NguoiThue/NguoiDung
        /// </summary>
        public async Task<HopDongDetailRes> GetByIdAsync(int id)
        {
            return await _context.HopDongs
                .Where(x => x.HopDongId == id && !x.IsDeleted)
                .Select(x => new HopDongDetailRes
                {
                    HopDongId = x.HopDongId,
                    MaHopDong = x.MaHopDong,
                    PhongTroId = x.PhongTroId,
                    SoPhong = x.PhongTro.SoPhong,
                    ChiNhanhId = x.PhongTro.ChiNhanhId,
                    TenChiNhanh = x.PhongTro.ChiNhanh.TenChiNhanh,
                    NguoiThueId = x.NguoiThueId,
                    TenNguoiThue = x.NguoiThue.HoVaTen,
                    ThoiDiemBatDau = x.ThoiDiemBatDau,
                    ThoiDiemKetThuc = x.ThoiDiemKetThuc,
                    TienCocPhong = x.TienCocPhong,
                    TienThuePhong = x.TienThuePhong,
                    TrangThaiHopDong = (int)x.TrangThaiHopDong
                })
                .FirstOrDefaultAsync();
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

            // 4. Kiểm tra người đại diện đã đứng tên hợp đồng hiệu lực nào khác chưa
            bool isNguoiThueDaCoHopDong = await _context.HopDongs.AnyAsync(x =>
                x.NguoiThueId == input.NguoiThueId &&
                x.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                !x.IsDeleted);
            if (isNguoiThueDaCoHopDong) 
                return (false, "Người đại diện này hiện đã đứng tên một hợp đồng đang hoạt động khác.");

            // 5. Kiểm tra giới hạn số người tối đa của phòng trọ
            var phong = await _context.PhongTros.FindAsync(input.PhongTroId);
            if (phong == null) return (false, "Không tìm thấy phòng trọ.");

            int totalMembersToAdd = (input.NguoiDungCoOPhongKhong ? 1 : 0) + 
                                    (input.ThanhVienKhacIds != null ? input.ThanhVienKhacIds.Where(id => id != input.NguoiThueId).Distinct().Count() : 0);
            if (totalMembersToAdd > phong.SoNguoiToiDa)
            {
                return (false, $"Số lượng người đăng ký vào phòng ({totalMembersToAdd} người) vượt quá số người tối đa cho phép của phòng này ({phong.SoNguoiToiDa} người).");
            }

            // 6. Kiểm tra xem các thành viên thêm vào có đang sinh sống ở phòng trọ khác có hợp đồng hoạt động hay không
            var allMemberIds = new List<int>();
            if (input.NguoiDungCoOPhongKhong) allMemberIds.Add(input.NguoiThueId);
            if (input.ThanhVienKhacIds != null)
            {
                allMemberIds.AddRange(input.ThanhVienKhacIds.Where(id => id != input.NguoiThueId).Distinct());
            }

            if (allMemberIds.Any())
            {
                var overlappingMembers = await _context.ChiTietThanhVienHopDongs
                    .Include(x => x.HopDong)
                    .Include(x => x.NguoiThue)
                    .Where(x => allMemberIds.Contains(x.NguoiThueId) &&
                                 x.NgayChuyenDi == null &&
                                 !x.IsDeleted &&
                                 x.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
                    .Select(x => x.NguoiThue.HoVaTen)
                    .ToListAsync();

                if (overlappingMembers.Any())
                {
                    return (false, $"Các thành viên sau đang ở một phòng khác có hợp đồng hoạt động: {string.Join(", ", overlappingMembers)}.");
                }
            }

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
                phong.TrangThai = TrangThaiPhong.DaThue;
                phong.NgayCapNhat = DateTime.UtcNow;

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

        /// <summary>
        /// Cập nhật hợp đồng – Sử dụng Transaction để đảm bảo tính nguyên tử.
        /// Nếu bất kỳ bước nào thất bại (cập nhật phòng, check-out thành viên, lưu hợp đồng),
        /// toàn bộ thay đổi sẽ được Rollback để tránh dữ liệu rác.
        /// </summary>
        public async Task<(bool IsSuccess, string ErrorMessage)> UpdateAsync(int id, HopDongReq input)
        {
            var entity = await _context.HopDongs.FirstOrDefaultAsync(x => x.HopDongId == id && !x.IsDeleted);
            if (entity == null) return (false, "Không tìm thấy hợp đồng.");

            if (input.ThoiDiemKetThuc.HasValue && input.ThoiDiemKetThuc.Value.Date < input.ThoiDiemBatDau.Date)
                return (false, "Ngày kết thúc không được trước ngày bắt đầu.");

            bool isDuplicateMa = await _context.HopDongs.AnyAsync(x => x.MaHopDong == input.MaHopDong && x.HopDongId != id && !x.IsDeleted);
            if (isDuplicateMa) return (false, "Mã hợp đồng bị trùng với hợp đồng khác.");

            // Nếu thay đổi trạng thái sang Hoạt động, cần kiểm tra người đại diện và phòng trọ
            if (input.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && entity.TrangThaiHopDong != TrangThaiHopDong.DangHoatDong)
            {
                bool isNguoiThueDaCoHopDong = await _context.HopDongs.AnyAsync(x =>
                    x.NguoiThueId == entity.NguoiThueId &&
                    x.HopDongId != id &&
                    x.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                    !x.IsDeleted);
                if (isNguoiThueDaCoHopDong) 
                    return (false, "Người đại diện của hợp đồng này hiện đang đứng tên một hợp đồng hoạt động khác.");

                bool isPhongDangThue = await _context.HopDongs.AnyAsync(x =>
                    x.PhongTroId == entity.PhongTroId &&
                    x.HopDongId != id &&
                    x.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                    !x.IsDeleted);
                if (isPhongDangThue) 
                    return (false, "Phòng trọ này hiện đang có một hợp đồng hoạt động khác.");
            }

            // === BỌC TRANSACTION: Đảm bảo tất cả thao tác thành công hoặc Rollback toàn bộ ===
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Bước 1: Xử lý cascade khi trạng thái hợp đồng thay đổi
                if (entity.TrangThaiHopDong != input.TrangThaiHopDong)
                {
                    var phong = await _context.PhongTros.FindAsync(entity.PhongTroId);
                    if (phong != null)
                    {
                        if (input.TrangThaiHopDong == TrangThaiHopDong.DaKetThuc || input.TrangThaiHopDong == TrangThaiHopDong.DaHuy)
                        {
                            phong.TrangThai = TrangThaiPhong.Trong;

                            // Tự động check-out TẤT CẢ thành viên đang ở (bao gồm chủ hợp đồng)
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

                // Bước 2: Cập nhật thông tin hợp đồng
                entity.MaHopDong = input.MaHopDong;
                entity.ThoiDiemBatDau = input.ThoiDiemBatDau.ToUniversalTime();
                entity.ThoiDiemKetThuc = input.ThoiDiemKetThuc?.ToUniversalTime();
                entity.TienCocPhong = input.TienCocPhong;
                entity.TienThuePhong = input.TienThuePhong;
                entity.TrangThaiHopDong = input.TrangThaiHopDong;
                entity.NgayCapNhat = DateTime.UtcNow;

                // Bước 3: Lưu tất cả thay đổi trong cùng 1 SaveChanges
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, string.Empty);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                string errorDetails = e.InnerException != null ? e.InnerException.Message : e.Message;
                return (false, $"Lỗi hệ thống khi cập nhật hợp đồng: {errorDetails}");
            }
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
