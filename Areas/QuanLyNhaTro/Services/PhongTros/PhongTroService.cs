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

            // Kiểm tra ràng buộc khi có hợp đồng đang hoạt động
            bool coHopDongHoatDong = await _context.HopDongs.AnyAsync(h => 
                h.PhongTroId == model.PhongTroId && 
                h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && 
                !h.IsDeleted);

            if (coHopDongHoatDong)
            {
                if (phongTonTai.ChiNhanhId != model.ChiNhanhId)
                {
                    return ServiceResult.Fail("Không thể chuyển chi nhánh cho phòng trọ đang có hợp đồng hoạt động!");
                }
                if (phongTonTai.SoPhong != model.SoPhong)
                {
                    return ServiceResult.Fail("Không thể đổi số phòng của phòng trọ đang có hợp đồng hoạt động!");
                }
                if (model.TrangThai == TrangThaiPhong.Trong || model.TrangThai == TrangThaiPhong.BaoTri)
                {
                    return ServiceResult.Fail("Không thể chuyển trạng thái phòng về Trống hoặc Bảo trì khi đang có hợp đồng hoạt động!");
                }
                if (model.SoNguoiToiDa < phongTonTai.SoNguoiToiDa)
                {
                    var hopDongActive = await _context.HopDongs
                        .FirstOrDefaultAsync(h => h.PhongTroId == model.PhongTroId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && !h.IsDeleted);
                    if (hopDongActive != null)
                    {
                        int currentActiveMembers = await _context.ChiTietThanhVienHopDongs
                            .CountAsync(x => x.HopDongId == hopDongActive.HopDongId && !x.IsDeleted && x.NgayChuyenDi == null);
                        
                        if (model.SoNguoiToiDa < currentActiveMembers)
                        {
                            return ServiceResult.Fail($"Số người tối đa mới ({model.SoNguoiToiDa} người) không được nhỏ hơn số người đang ở thực tế trong phòng ({currentActiveMembers} người)!");
                        }
                    }
                }
            }

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

            if (phong.TrangThai == TrangThaiPhong.DaThue)
            {
                return ServiceResult.Fail("Không thể xóa phòng trọ đang trong trạng thái cho thuê!");
            }

            bool coHopDongHoatDong = await _context.HopDongs.AnyAsync(h => 
                h.PhongTroId == id && 
                h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && 
                !h.IsDeleted);

            if (coHopDongHoatDong)
            {
                return ServiceResult.Fail("Không thể xóa phòng trọ đang có hợp đồng hoạt động!");
            }

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
                    ChiNhanhId = p.ChiNhanhId,
                    TrangThai = p.TrangThai,
                    GiaThue = p.GiaThue
                })
                .ToListAsync();
        }

        public async Task<List<PhongCardRes>> GetSoDoPhongAsync(int chiNhanhId)
        {
            var today = DateTime.UtcNow;
            var in15Days = today.AddDays(15);

            var query = _context.PhongTros
                .Include(p => p.ChiNhanh)
                .Where(p => !p.IsDeleted);

            if (chiNhanhId > 0)
                query = query.Where(p => p.ChiNhanhId == chiNhanhId);

            var phongs = await query.OrderBy(p => p.TangLau).ThenBy(p => p.SoPhong).ToListAsync();

            // 1. Pre-load all active contracts for these rooms
            var phongTroIds = phongs.Select(p => p.PhongTroId).ToList();
            var hopDongs = await _context.HopDongs
                .Include(h => h.NguoiThue)
                .Where(h => phongTroIds.Contains(h.PhongTroId) &&
                            h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                            !h.IsDeleted)
                .ToListAsync();

            // 2. Pre-load the sum of unpaid invoices for these contracts in a single query
            var hopDongIds = hopDongs.Select(h => h.HopDongId).ToList();
            var unpaidInvoicesSums = await _context.HoaDons
                .Where(hd => hopDongIds.Contains(hd.HopDongId) &&
                             hd.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan &&
                             !hd.IsDeleted)
                .GroupBy(hd => hd.HopDongId)
                .Select(g => new { HopDongId = g.Key, Sum = g.Sum(hd => hd.TongTien) })
                .ToDictionaryAsync(x => x.HopDongId, x => x.Sum);

            var result = new List<PhongCardRes>();

            foreach (var p in phongs)
            {
                var hopDong = hopDongs.FirstOrDefault(h => h.PhongTroId == p.PhongTroId);

                double soTienNo = 0;
                bool coNoTien = false;
                if (hopDong != null)
                {
                    unpaidInvoicesSums.TryGetValue(hopDong.HopDongId, out soTienNo);
                    coNoTien = soTienNo > 0;
                }

                // Tính cảnh báo hết hạn
                bool sapHetHan = false;
                int? soNgayConLai = null;
                if (hopDong?.ThoiDiemKetThuc != null)
                {
                    soNgayConLai = (int)(hopDong.ThoiDiemKetThuc.Value - today).TotalDays;
                    sapHetHan = hopDong.ThoiDiemKetThuc.Value <= in15Days && hopDong.ThoiDiemKetThuc.Value >= today;
                }

                string trangThaiText = p.TrangThai switch
                {
                    TrangThaiPhong.Trong => "Trống",
                    TrangThaiPhong.DaThue => "Đã thuê",
                    TrangThaiPhong.BaoTri => "Bảo trì",
                    _ => ""
                };

                result.Add(new PhongCardRes
                {
                    PhongTroId = p.PhongTroId,
                    SoPhong = p.SoPhong,
                    TangLau = p.TangLau,
                    GiaThue = p.GiaThue,
                    DienTich = p.DienTich,
                    SoNguoiToiDa = p.SoNguoiToiDa,
                    TrangThai = (int)p.TrangThai,
                    TrangThaiText = trangThaiText,
                    ChiNhanhId = p.ChiNhanhId,
                    TenChiNhanh = p.ChiNhanh.TenChiNhanh,
                    HopDongId = hopDong?.HopDongId,
                    TenNguoiThue = hopDong?.NguoiThue?.HoVaTen,
                    SdtNguoiThue = hopDong?.NguoiThue?.SoDienThoai,
                    NgayHetHan = hopDong?.ThoiDiemKetThuc,
                    CoNoTien = coNoTien,
                    SoTienNo = soTienNo,
                    SapHetHan = sapHetHan,
                    SoNgayConLai = soNgayConLai
                });
            }

            return result;
        }

        public async Task<object> GetQuickContractAsync(int phongTroId)
        {
            var contract = await _context.HopDongs
                .Include(h => h.NguoiThue)
                .Include(h => h.PhongTro)
                .Where(h => h.PhongTroId == phongTroId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && !h.IsDeleted)
                .FirstOrDefaultAsync();

            if (contract == null) return null;

            var members = await _context.ChiTietThanhVienHopDongs
                .Include(m => m.NguoiThue)
                .Where(m => m.HopDongId == contract.HopDongId && m.NgayChuyenDi == null && !m.IsDeleted)
                .Select(m => new {
                    hoVaTen = m.NguoiThue.HoVaTen,
                    soDienThoai = m.NguoiThue.SoDienThoai,
                    cccd = m.NguoiThue.CCCD,
                    ngayVao = m.NgayVao.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            var services = await _context.DangKyDichVus
                .Include(s => s.DichVuChiNhanh.DichVu)
                .Where(s => s.PhongTroId == phongTroId && !s.DichVuChiNhanh.IsDeleted)
                .Select(s => new {
                    tenDichVu = s.DichVuChiNhanh.DichVu.TenDichVu,
                    donGia = s.DichVuChiNhanh.GiaDichVu,
                    donVi = s.DichVuChiNhanh.DichVu.DonVi,
                    soLuong = s.SoLuong
                })
                .ToListAsync();

            return new {
                hopDongId = contract.HopDongId,
                maHopDong = contract.MaHopDong,
                thoiDiemBatDau = contract.ThoiDiemBatDau.ToString("dd/MM/yyyy"),
                thoiDiemKetThuc = contract.ThoiDiemKetThuc.HasValue ? contract.ThoiDiemKetThuc.Value.ToString("dd/MM/yyyy") : "Không xác định",
                tienCocPhong = contract.TienCocPhong,
                tienThuePhong = contract.TienThuePhong,
                tenNguoiDaiDien = contract.NguoiThue.HoVaTen,
                soDienThoaiDaiDien = contract.NguoiThue.SoDienThoai,
                cccdDaiDien = contract.NguoiThue.CCCD,
                members = members,
                services = services
            };
        }

        public async Task<object> GetUnpaidInvoiceAsync(int phongTroId)
        {
            var invoice = await _context.HoaDons
                .Include(i => i.HopDong)
                    .ThenInclude(h => h.PhongTro)
                .Include(i => i.HopDong)
                    .ThenInclude(h => h.NguoiThue)
                .Where(i => i.HopDong.PhongTroId == phongTroId && i.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan && !i.IsDeleted)
                .OrderByDescending(i => i.NgayTao)
                .FirstOrDefaultAsync();

            if (invoice == null) return null;

            var details = await _context.ChiTietHoaDons
                .Include(d => d.DichVu)
                .Where(d => d.HoaDonId == invoice.HoaDonId && !d.IsDeleted)
                .ToListAsync();

            var mappedDetails = details.Select(d => new {
                tenDichVu = d.TenDichVu,
                donGia = d.DonGia,
                soLuong = d.SoLuong,
                donVi = d.DichVu != null ? d.DichVu.DonVi : 
                        (d.TenDichVu.ToLower().Contains("điện") ? "kWh" : 
                        (d.TenDichVu.ToLower().Contains("nước") ? "m³" : "-")),
                tongTien = d.TongTien
            }).ToList();

            return new {
                hoaDonId = invoice.HoaDonId,
                maHoaDon = invoice.MaHoaDon,
                thang = invoice.Thang,
                nam = invoice.Nam,
                tongTien = invoice.TongTien,
                tenPhong = invoice.HopDong.PhongTro.SoPhong,
                tenNguoiThue = invoice.HopDong.NguoiThue.HoVaTen,
                chiTiets = mappedDetails
            };
        }

        public async Task<ServiceResult> PhatSinhNgauNhienAsync()
        {
            var chiNhanhs = await _context.ChiNhanhs.Where(c => !c.IsDeleted).ToListAsync();
            if (!chiNhanhs.Any())
            {
                return ServiceResult.Fail("Không tìm thấy chi nhánh nào trong database. Vui lòng thêm chi nhánh trước!");
            }

            var random = new Random();
            var addedRooms = new List<string>();

            // Query max rooms for all branches in a single query
            var allRooms = await _context.PhongTros
                .Where(p => !p.IsDeleted)
                .Select(p => new { p.ChiNhanhId, p.SoPhong })
                .ToListAsync();

            var branchMaxRooms = new Dictionary<int, int>();
            foreach (var cn in chiNhanhs)
            {
                var maxRoom = allRooms.Where(p => p.ChiNhanhId == cn.ChiNhanhId).Select(p => p.SoPhong).ToList();
                int maxVal = 100;
                if (maxRoom.Any())
                {
                    maxVal = maxRoom
                        .Select(sp => int.TryParse(sp, out var n) ? n : 100)
                        .Max();
                }
                branchMaxRooms[cn.ChiNhanhId] = maxVal;
            }

            for (int i = 0; i < 10; i++)
            {
                var chiNhanh = chiNhanhs[random.Next(chiNhanhs.Count)];
                int nextRoomNum = branchMaxRooms[chiNhanh.ChiNhanhId] + 1;
                branchMaxRooms[chiNhanh.ChiNhanhId] = nextRoomNum;

                var pt = new PhongTro
                {
                    ChiNhanhId = chiNhanh.ChiNhanhId,
                    SoPhong = nextRoomNum.ToString(),
                    TangLau = (nextRoomNum / 100) == 0 ? 1 : (nextRoomNum / 100),
                    GiaThue = random.Next(18, 45) * 100000,
                    DienTich = random.Next(15, 30),
                    SoNguoiToiDa = random.Next(2, 4),
                    TrangThai = TrangThaiPhong.Trong,
                    MoTa = $"Phòng {nextRoomNum} ngẫu nhiên thuộc {chiNhanh.TenChiNhanh}",
                    NgayTao = DateTime.UtcNow
                };
                _context.PhongTros.Add(pt);
                addedRooms.Add($"{pt.SoPhong} ({chiNhanh.TenChiNhanh})");
            }

            await _context.SaveChangesAsync();
            return ServiceResult.Ok($"Đã phát sinh 10 phòng thành công: {string.Join(", ", addedRooms)}");
        }
    }
}
