using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DienNuocs
{
    public class DienNuocService : IDienNuocService
    {
        private readonly ApplicationDbContext _context;

        public DienNuocService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<DienNuocPhongRes>> GetDanhSachDienNuocAsync(int chiNhanhId, int thang, int nam)
        {
            // 1. Lấy danh sách các phòng đang được thuê tại chi nhánh
            var phongĐangThue = await _context.HopDongs
                .Include(h => h.PhongTro)
                .Include(h => h.NguoiThue)
                .Where(h => h.PhongTro.ChiNhanhId == chiNhanhId &&
                            h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                            !h.IsDeleted)
                .ToListAsync();

            var result = new List<DienNuocPhongRes>();

            // Tính tháng/năm trước đó để lấy chỉ số cũ nếu cần
            int prevThang = thang == 1 ? 12 : thang - 1;
            int prevNam = thang == 1 ? nam - 1 : nam;

            foreach (var hd in phongĐangThue)
            {
                var phongTroId = hd.PhongTroId;

                // Kiểm tra xem đã có bản ghi cho tháng hiện tại chưa
                var currentRecord = await _context.DichVuDienNuocCuaPhongs
                    .FirstOrDefaultAsync(x => x.PhongTroId == phongTroId && x.Thang == thang && x.Nam == nam && !x.IsDeleted);

                if (currentRecord != null)
                {
                    result.Add(new DienNuocPhongRes
                    {
                        DichVuDienNuocCuaPhongId = currentRecord.DichVuDienNuocCuaPhongId,
                        PhongTroId = phongTroId,
                        TenPhong = hd.PhongTro.SoPhong,
                        TenNguoiDaiDien = hd.NguoiThue.HoVaTen,
                        ChiSoDienCu = currentRecord.ChiSoDienCu,
                        ChiSoDienMoi = currentRecord.ChiSoDienMoi,
                        ChiSoNuocCu = currentRecord.ChiSoNuocCu,
                        ChiSoNuocMoi = currentRecord.ChiSoNuocMoi,
                        IsDaChot = true
                    });
                }
                else
                {
                    // Lấy chỉ số mới của tháng trước đó làm chỉ số cũ cho tháng này
                    var prevRecord = await _context.DichVuDienNuocCuaPhongs
                        .FirstOrDefaultAsync(x => x.PhongTroId == phongTroId && x.Thang == prevThang && x.Nam == prevNam && !x.IsDeleted);

                    double dienCu = prevRecord != null ? prevRecord.ChiSoDienMoi : 0;
                    double nuocCu = prevRecord != null ? prevRecord.ChiSoNuocMoi : 0;

                    result.Add(new DienNuocPhongRes
                    {
                        DichVuDienNuocCuaPhongId = 0,
                        PhongTroId = phongTroId,
                        TenPhong = hd.PhongTro.SoPhong,
                        TenNguoiDaiDien = hd.NguoiThue.HoVaTen,
                        ChiSoDienCu = dienCu,
                        ChiSoDienMoi = 0,
                        ChiSoNuocCu = nuocCu,
                        ChiSoNuocMoi = 0,
                        IsDaChot = false
                    });
                }
            }

            return result.OrderBy(x => x.TenPhong).ToList();
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> SaveChotDienNuocAsync(ChotDienNuocReq input)
        {
            if (input == null || input.DanhSachPhong == null || !input.DanhSachPhong.Any())
            {
                return (false, "Không có dữ liệu để lưu.");
            }

            // Lấy đơn giá Điện / Nước cấu hình ở Chi nhánh
            // Tìm DichVu có tên "Điện" hoặc "Nước"
            var dichVuDien = await _context.DichVus.FirstOrDefaultAsync(x => x.TenDichVu.ToLower().Contains("điện") && !x.IsDeleted);
            var dichVuNuoc = await _context.DichVus.FirstOrDefaultAsync(x => x.TenDichVu.ToLower().Contains("nước") && !x.IsDeleted);

            double donGiaDien = 0;
            double donGiaNuoc = 0;

            if (dichVuDien != null)
            {
                var bgDien = await _context.Set<DichVuChiNhanh>().FirstOrDefaultAsync(x => x.DichVuId == dichVuDien.DichVuId && x.ChiNhanhId == input.ChiNhanhId && !x.IsDeleted);
                if (bgDien != null) donGiaDien = bgDien.GiaDichVu;
            }

            if (dichVuNuoc != null)
            {
                var bgNuoc = await _context.Set<DichVuChiNhanh>().FirstOrDefaultAsync(x => x.DichVuId == dichVuNuoc.DichVuId && x.ChiNhanhId == input.ChiNhanhId && !x.IsDeleted);
                if (bgNuoc != null) donGiaNuoc = bgNuoc.GiaDichVu;
            }

            foreach (var req in input.DanhSachPhong)
            {
                if (req.ChiSoDienMoi < req.ChiSoDienCu) return (false, "Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.");
                if (req.ChiSoNuocMoi < req.ChiSoNuocCu) return (false, "Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.");

                if (req.DichVuDienNuocCuaPhongId > 0)
                {
                    // Update
                    var record = await _context.DichVuDienNuocCuaPhongs.FindAsync(req.DichVuDienNuocCuaPhongId);
                    if (record != null && !record.IsDeleted)
                    {
                        record.ChiSoDienCu = req.ChiSoDienCu;
                        record.ChiSoDienMoi = req.ChiSoDienMoi;
                        record.ChiSoNuocCu = req.ChiSoNuocCu;
                        record.ChiSoNuocMoi = req.ChiSoNuocMoi;
                        record.DonGiaDien = donGiaDien;
                        record.DonGiaNuoc = donGiaNuoc;
                        record.NgayCapNhat = DateTime.UtcNow;
                        _context.DichVuDienNuocCuaPhongs.Update(record);
                    }
                }
                else
                {
                    // Create
                    var record = new DichVuDienNuocCuaPhong
                    {
                        PhongTroId = req.PhongTroId,
                        Thang = input.Thang,
                        Nam = input.Nam,
                        ChiSoDienCu = req.ChiSoDienCu,
                        ChiSoDienMoi = req.ChiSoDienMoi,
                        DonGiaDien = donGiaDien,
                        ChiSoNuocCu = req.ChiSoNuocCu,
                        ChiSoNuocMoi = req.ChiSoNuocMoi,
                        DonGiaNuoc = donGiaNuoc
                    };
                    _context.DichVuDienNuocCuaPhongs.Add(record);
                }
            }

            await _context.SaveChangesAsync();
            return (true, null);
        }
    }
}
