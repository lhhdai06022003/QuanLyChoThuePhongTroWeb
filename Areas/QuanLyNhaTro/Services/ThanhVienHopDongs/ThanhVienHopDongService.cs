using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ThanhVienHopDongs
{
    public class ThanhVienHopDongService : IThanhVienHopDongService
    {
        private readonly ApplicationDbContext _context;

        public ThanhVienHopDongService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ThanhVienHopDongRes>> GetThanhVienByHopDongIdAsync(int hopDongId)
        {
            var hopDong = await _context.HopDongs.FirstOrDefaultAsync(x => x.HopDongId == hopDongId);
            if (hopDong == null) return new List<ThanhVienHopDongRes>();

            return await _context.ChiTietThanhVienHopDongs
                .Include(x => x.NguoiThue)
                .Where(x => x.HopDongId == hopDongId && !x.IsDeleted)
                .Select(x => new ThanhVienHopDongRes
                {
                    ChiTietThanhVienHopDongId = x.ChiTietThanhVienHopDongId,
                    NguoiThueId = x.NguoiThueId,
                    HoVaTen = x.NguoiThue.HoVaTen,
                    SoDienThoai = x.NguoiThue.SoDienThoai,
                    CCCD = x.NguoiThue.CCCD,
                    NgayVao = x.NgayVao,
                    NgayChuyenDi = x.NgayChuyenDi,
                    IsChuHopDong = x.NguoiThueId == hopDong.NguoiThueId
                })
                .OrderByDescending(x => x.IsChuHopDong)
                .ThenBy(x => x.NgayVao)
                .ToListAsync();
        }

        /// <summary>
        /// Thêm thành viên vào hợp đồng – Sử dụng Transaction.
        /// Nếu tạo NguoiThue mới + tạo ChiTietThanhVienHopDong, cả 2 bước phải
        /// thành công cùng lúc hoặc Rollback hết để tránh bản ghi rác.
        /// </summary>
        public async Task<(bool IsSuccess, string ErrorMessage)> AddThanhVienVaoHopDongAsync(ThanhVienHopDongReq request)
        {
            var hopDong = await _context.HopDongs
                .Include(x => x.PhongTro)
                .FirstOrDefaultAsync(x => x.HopDongId == request.HopDongId && !x.IsDeleted);

            if (hopDong == null) return (false, "Không tìm thấy hợp đồng.");
            if (hopDong.TrangThaiHopDong != TrangThaiHopDong.DangHoatDong)
                return (false, "Chỉ có thể thêm thành viên vào hợp đồng đang hoạt động.");

            // === VALIDATION: Kiểm tra số lượng người ở tối đa ===
            int currentActiveMembers = await _context.ChiTietThanhVienHopDongs
                .CountAsync(x => x.HopDongId == request.HopDongId && !x.IsDeleted && x.NgayChuyenDi == null);
            
            if (currentActiveMembers >= hopDong.PhongTro.SoNguoiToiDa)
                return (false, $"Phòng đã đạt số người ở tối đa ({hopDong.PhongTro.SoNguoiToiDa} người).");

            // === BỌC TRANSACTION: Đảm bảo tạo NguoiThue mới + gắn vào HĐ nguyên tử ===
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                int nguoiThueIdToUse = 0;

                if (request.NguoiThueId.HasValue && request.NguoiThueId.Value > 0)
                {
                    // Trường hợp chọn khách có sẵn từ dropdown
                    nguoiThueIdToUse = request.NguoiThueId.Value;
                    var nguoiThueExists = await _context.NguoiThues.AnyAsync(x => x.NguoiThueId == nguoiThueIdToUse && !x.IsDeleted);
                    if (!nguoiThueExists) return (false, "Người thuê không tồn tại.");
                }
                else
                {
                    // Trường hợp tạo khách mới
                    if (string.IsNullOrWhiteSpace(request.HoVaTen)) return (false, "Họ và tên không được để trống.");
                    if (string.IsNullOrWhiteSpace(request.SoDienThoai)) return (false, "Số điện thoại không được để trống.");
                    if (string.IsNullOrWhiteSpace(request.CCCD)) return (false, "CCCD không được để trống.");

                    // Kiểm tra trùng CCCD: nếu đã tồn tại thì tái sử dụng, không tạo mới
                    var existingNguoiThue = await _context.NguoiThues.FirstOrDefaultAsync(x => x.CCCD == request.CCCD && !x.IsDeleted);
                    if (existingNguoiThue != null)
                    {
                        nguoiThueIdToUse = existingNguoiThue.NguoiThueId;
                    }
                    else
                    {
                        var newNguoiThue = new NguoiThue
                        {
                            HoVaTen = request.HoVaTen,
                            SoDienThoai = request.SoDienThoai,
                            CCCD = request.CCCD,
                            NgayTao = DateTime.UtcNow,
                            IsDeleted = false
                        };
                        _context.NguoiThues.Add(newNguoiThue);
                        await _context.SaveChangesAsync(); // Lưu để lấy ID (trong Transaction)
                        nguoiThueIdToUse = newNguoiThue.NguoiThueId;
                    }
                }

                // === VALIDATION: Kiểm tra Overlapping (Khách có đang ở một hợp đồng khác hay không) ===
                bool isOverlapping = await _context.ChiTietThanhVienHopDongs
                    .Include(x => x.HopDong)
                    .AnyAsync(x => x.NguoiThueId == nguoiThueIdToUse && 
                                   x.NgayChuyenDi == null && 
                                   !x.IsDeleted && 
                                   x.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong);

                if (isOverlapping)
                {
                    // Rollback việc tạo NguoiThue mới (nếu có) bằng cách không commit
                    await transaction.RollbackAsync();
                    return (false, "Người này hiện đang ở một phòng khác (Hợp đồng khác đang hoạt động).");
                }

                var chiTiet = new ChiTietThanhVienHopDong
                {
                    HopDongId = request.HopDongId,
                    NguoiThueId = nguoiThueIdToUse,
                    NgayVao = request.NgayVao.ToUniversalTime(),
                    IsDeleted = false
                };

                _context.ChiTietThanhVienHopDongs.Add(chiTiet);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, string.Empty);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                string errorDetails = e.InnerException != null ? e.InnerException.Message : e.Message;
                return (false, $"Lỗi hệ thống khi thêm thành viên: {errorDetails}");
            }
        }

        /// <summary>
        /// Báo thành viên rời phòng (set NgàyChuyểnĐi = now).
        /// RÀNG BUỘC NGHIỆP VỤ: Không cho phép check-out Chủ hợp đồng khi HĐ còn hoạt động.
        /// Chủ HĐ chỉ được check-out tự động khi toàn bộ hợp đồng kết thúc/hủy.
        /// </summary>
        public async Task<(bool IsSuccess, string ErrorMessage)> BaoRoiPhongAsync(int chiTietId)
        {
            var chiTiet = await _context.ChiTietThanhVienHopDongs
                .Include(x => x.HopDong) // Include để kiểm tra trạng thái HĐ
                .FirstOrDefaultAsync(x => x.ChiTietThanhVienHopDongId == chiTietId && !x.IsDeleted);
            if (chiTiet == null) return (false, "Không tìm thấy thông tin thành viên.");

            if (chiTiet.NgayChuyenDi.HasValue) return (false, "Thành viên này đã rời phòng trước đó.");

            // === BẢO VỆ CHỦ HỢP ĐỒNG: Không cho check-out khi HĐ đang hoạt động ===
            if (chiTiet.HopDong != null 
                && chiTiet.NguoiThueId == chiTiet.HopDong.NguoiThueId 
                && chiTiet.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
            {
                return (false, "Không thể báo rời phòng cho người đại diện hợp đồng khi hợp đồng còn hiệu lực. Vui lòng kết thúc hợp đồng trước.");
            }

            chiTiet.NgayChuyenDi = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return (true, string.Empty);
        }

        /// <summary>
        /// Xoá hoàn toàn thành viên (do gõ sai dữ liệu) – Soft delete.
        /// RÀNG BUỘC NGHIỆP VỤ: Không cho phép xoá Chủ hợp đồng khi HĐ còn hoạt động.
        /// </summary>
        public async Task<(bool IsSuccess, string ErrorMessage)> XoaThanhVienNhamAsync(int chiTietId)
        {
            var chiTiet = await _context.ChiTietThanhVienHopDongs
                .Include(x => x.HopDong) // Include để kiểm tra Chủ HĐ
                .FirstOrDefaultAsync(x => x.ChiTietThanhVienHopDongId == chiTietId && !x.IsDeleted);
            if (chiTiet == null) return (false, "Không tìm thấy thông tin thành viên.");

            // === BẢO VỆ CHỦ HỢP ĐỒNG: Không cho xoá khi HĐ đang hoạt động ===
            if (chiTiet.HopDong != null 
                && chiTiet.NguoiThueId == chiTiet.HopDong.NguoiThueId 
                && chiTiet.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
            {
                return (false, "Không thể xoá người đại diện hợp đồng khỏi danh sách thành viên khi hợp đồng còn hiệu lực.");
            }

            chiTiet.IsDeleted = true;
            await _context.SaveChangesAsync();

            return (true, string.Empty);
        }
    }
}
