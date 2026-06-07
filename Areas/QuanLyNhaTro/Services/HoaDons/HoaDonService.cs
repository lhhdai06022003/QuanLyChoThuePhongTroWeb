using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HoaDons
{
    public class HoaDonService : IHoaDonService
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public HoaDonService(ApplicationDbContext context, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // =================== PHÁT SINH HÓA ĐƠN HÀ LOẠT ===================
        public async Task<(bool IsSuccess, string Message, int SoHoaDonMoi)> PhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam, List<int> selectedPhongTroIds)
        {
            if (selectedPhongTroIds == null || !selectedPhongTroIds.Any())
                return (false, "Không có phòng nào được chọn để phát sinh hóa đơn.", 0);

            var startOfMonth = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            var daysInMonth = DateTime.DaysInMonth(nam, thang);

            // Lấy thông tin dịch vụ Điện và Nước từ database
            var dienDichVu = await _context.DichVus.FirstOrDefaultAsync(d => (d.TenDichVu.Contains("Điện") || d.TenDichVu.Contains("điện")) && !d.IsDeleted);
            var nuocDichVu = await _context.DichVus.FirstOrDefaultAsync(d => (d.TenDichVu.Contains("Nước") || d.TenDichVu.Contains("nước")) && !d.IsDeleted);

            // 1. Lấy tất cả hợp đồng thuộc danh sách phòng được chọn và có hiệu lực trong tháng
            var hopDongs = await _context.HopDongs
                .Include(h => h.PhongTro)
                .Include(h => h.NguoiThue)
                .Where(h => h.PhongTro.ChiNhanhId == chiNhanhId &&
                            selectedPhongTroIds.Contains(h.PhongTroId) &&
                            !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau <= endOfMonth &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc.Value >= startOfMonth))
                .ToListAsync();

            if (!hopDongs.Any())
                return (false, "Không tìm thấy hợp đồng hợp lệ nào cho các phòng đã chọn.", 0);

            int soHoaDonMoi = 0;

            foreach (var hd in hopDongs)
            {
                // 2. Kiểm tra đã tồn tại hóa đơn cho tháng/năm này chưa
                bool exists = await _context.HoaDons.AnyAsync(x =>
                    x.HopDongId == hd.HopDongId && x.Thang == thang && x.Nam == nam && !x.IsDeleted);
                if (exists) continue;

                // 3. Tìm dữ liệu điện nước đã chốt
                var dienNuoc = await _context.DichVuDienNuocCuaPhongs
                    .FirstOrDefaultAsync(x => x.PhongTroId == hd.PhongTroId && x.Thang == thang && x.Nam == nam && !x.IsDeleted);
                
                // Ràng buộc nghiêm ngặt: Nếu chưa chốt điện nước, bỏ qua không sinh hóa đơn
                if (dienNuoc == null) continue;

                // 4. Tạo các dòng chi tiết hóa đơn
                var chiTietList = new List<ChiTietHoaDon>();

                // Dòng 1: Tiền thuê phòng (tính lẻ ngày nếu có)
                DateTime activeStart = hd.ThoiDiemBatDau > startOfMonth ? hd.ThoiDiemBatDau.Date : startOfMonth;
                DateTime activeEnd = (hd.ThoiDiemKetThuc != null && hd.ThoiDiemKetThuc.Value < endOfMonth) ? hd.ThoiDiemKetThuc.Value.Date : endOfMonth;
                int activeDays = (activeEnd - activeStart).Days + 1;
                double tienPhong = Math.Round((hd.TienThuePhong / daysInMonth) * activeDays);

                string tenTienPhong = "Tiền thuê phòng";
                if (activeDays < daysInMonth)
                {
                    tenTienPhong = $"Tiền thuê phòng (thực tế ở {activeDays}/{daysInMonth} ngày)";
                }

                chiTietList.Add(new ChiTietHoaDon
                {
                    TenDichVu = tenTienPhong,
                    DonGia = hd.TienThuePhong, // Lưu đơn giá gốc
                    SoLuong = 1,
                    TongTien = tienPhong
                });

                // Dòng 2: Tiền điện
                double soDien = dienNuoc.ChiSoDienMoi - dienNuoc.ChiSoDienCu;
                double tienDien = soDien * dienNuoc.DonGiaDien;
                chiTietList.Add(new ChiTietHoaDon
                {
                    TenDichVu = $"Tiền điện ({dienNuoc.ChiSoDienCu} → {dienNuoc.ChiSoDienMoi})",
                    DonGia = dienNuoc.DonGiaDien,
                    SoLuong = (int)soDien,
                    TongTien = tienDien,
                    DichVuId = dienDichVu?.DichVuId
                });

                // Dòng 3: Tiền nước
                double soNuoc = dienNuoc.ChiSoNuocMoi - dienNuoc.ChiSoNuocCu;
                double tienNuoc = soNuoc * dienNuoc.DonGiaNuoc;
                chiTietList.Add(new ChiTietHoaDon
                {
                    TenDichVu = $"Tiền nước ({dienNuoc.ChiSoNuocCu} → {dienNuoc.ChiSoNuocMoi})",
                    DonGia = dienNuoc.DonGiaNuoc,
                    SoLuong = (int)soNuoc,
                    TongTien = tienNuoc,
                    DichVuId = nuocDichVu?.DichVuId
                });

                // Dòng 4+: Các dịch vụ khác đã đăng ký cho phòng (có tính lẻ ngày nếu đăng ký giữa tháng)
                var dangKyDvs = await _context.DangKyDichVus
                    .Include(d => d.DichVuChiNhanh)
                        .ThenInclude(dcn => dcn.DichVu)
                    .Where(d => d.PhongTroId == hd.PhongTroId)
                    .ToListAsync();

                foreach (var dk in dangKyDvs)
                {
                    // Bỏ qua dịch vụ Điện/Nước vì đã tính ở trên
                    var tenDv = dk.DichVuChiNhanh.DichVu.TenDichVu.ToLower();
                    if (tenDv.Contains("điện") || tenDv.Contains("nước")) continue;

                    // Tính lẻ ngày đăng ký dịch vụ:
                    if (dk.NgayBatDau.Date <= activeEnd)
                    {
                        DateTime serviceStart = dk.NgayBatDau.Date > activeStart ? dk.NgayBatDau.Date : activeStart;
                        int serviceActiveDays = (activeEnd - serviceStart).Days + 1;

                        if (serviceActiveDays > 0)
                        {
                            double dvTongTien = Math.Round((dk.DichVuChiNhanh.GiaDichVu * dk.SoLuong / daysInMonth) * serviceActiveDays);
                            string finalTenDichVu = dk.DichVuChiNhanh.DichVu.TenDichVu;

                            if (serviceActiveDays < daysInMonth)
                            {
                                finalTenDichVu = $"{dk.DichVuChiNhanh.DichVu.TenDichVu} (thực tế dùng {serviceActiveDays}/{daysInMonth} ngày)";
                            }

                            chiTietList.Add(new ChiTietHoaDon
                            {
                                TenDichVu = finalTenDichVu,
                                DonGia = dk.DichVuChiNhanh.GiaDichVu,
                                SoLuong = dk.SoLuong,
                                TongTien = dvTongTien,
                                DichVuId = dk.DichVuChiNhanh.DichVuId
                            });
                        }
                    }
                }

                double tongTien = chiTietList.Sum(x => x.TongTien);

                // 5. Tạo hóa đơn
                var hoaDon = new HoaDon
                {
                    MaHoaDon = $"HD-{hd.PhongTro.SoPhong}-{thang:D2}{nam}",
                    HopDongId = hd.HopDongId,
                    Thang = thang,
                    Nam = nam,
                    TongTien = tongTien,
                    TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                    DichVuDienNuocCuaPhongId = dienNuoc.DichVuDienNuocCuaPhongId,
                    ChiTietHoaDonDichVus = chiTietList
                };

                _context.HoaDons.Add(hoaDon);
                soHoaDonMoi++;
            }

            await _context.SaveChangesAsync();
            return (true, $"Đã phát sinh {soHoaDonMoi} hóa đơn thành công.", soHoaDonMoi);
        }

        // =================== XEM TRƯỚC PHÁT SINH HÓA ĐƠN ===================
        public async Task<List<PhatSinhPreviewRes>> PreviewPhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam)
        {
            var startOfMonth = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            var daysInMonth = DateTime.DaysInMonth(nam, thang);

            // Lấy tất cả phòng của chi nhánh
            var phongTros = await _context.PhongTros
                .Where(p => p.ChiNhanhId == chiNhanhId && !p.IsDeleted)
                .ToListAsync();

            // Lấy tất cả hợp đồng có hiệu lực trong tháng của chi nhánh
            var hopDongs = await _context.HopDongs
                .Include(h => h.NguoiThue)
                .Where(h => h.PhongTro.ChiNhanhId == chiNhanhId &&
                            !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau <= endOfMonth &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc.Value >= startOfMonth))
                .ToListAsync();

            var result = new List<PhatSinhPreviewRes>();

            foreach (var p in phongTros)
            {
                var hd = hopDongs.FirstOrDefault(h => h.PhongTroId == p.PhongTroId);
                var item = new PhatSinhPreviewRes
                {
                    PhongTroId = p.PhongTroId,
                    SoPhong = p.SoPhong
                };

                if (hd == null)
                {
                    item.TenNguoiThue = "";
                    item.HopDongHopLe = false;
                    item.GhiChuTrangThai = "Phòng trống / Không có hợp đồng hoạt động";
                    result.Add(item);
                    continue;
                }

                item.TenNguoiThue = hd.NguoiThue?.HoVaTen ?? "";

                // Kiểm tra hóa đơn đã tồn tại
                bool exists = await _context.HoaDons.AnyAsync(x =>
                    x.HopDongId == hd.HopDongId && x.Thang == thang && x.Nam == nam && !x.IsDeleted);

                if (exists)
                {
                    item.DaCoHoaDon = true;
                    item.HopDongHopLe = false;
                    item.GhiChuTrangThai = "Đã phát sinh hóa đơn tháng này";
                    result.Add(item);
                    continue;
                }

                // Kiểm tra chốt điện nước
                var dienNuoc = await _context.DichVuDienNuocCuaPhongs
                    .FirstOrDefaultAsync(x => x.PhongTroId == p.PhongTroId && x.Thang == thang && x.Nam == nam && !x.IsDeleted);

                item.DaChotDienNuoc = (dienNuoc != null);

                // Tính số ngày ở thực tế và tiền phòng dự kiến
                DateTime activeStart = hd.ThoiDiemBatDau > startOfMonth ? hd.ThoiDiemBatDau.Date : startOfMonth;
                DateTime activeEnd = (hd.ThoiDiemKetThuc != null && hd.ThoiDiemKetThuc.Value < endOfMonth) ? hd.ThoiDiemKetThuc.Value.Date : endOfMonth;
                int activeDays = (activeEnd - activeStart).Days + 1;
                double tienPhong = Math.Round((hd.TienThuePhong / daysInMonth) * activeDays);

                item.SoNgayO = activeDays;
                item.TongSoNgayTrongThang = daysInMonth;
                item.TienPhongDuKien = tienPhong;

                // Tính tổng tiền dự kiến
                double tongTien = tienPhong;
                if (dienNuoc != null)
                {
                    double soDien = dienNuoc.ChiSoDienMoi - dienNuoc.ChiSoDienCu;
                    double soNuoc = dienNuoc.ChiSoNuocMoi - dienNuoc.ChiSoNuocCu;
                    tongTien += (soDien * dienNuoc.DonGiaDien) + (soNuoc * dienNuoc.DonGiaNuoc);
                }

                // Tính thêm các dịch vụ cố định (có tính lẻ ngày nếu đăng ký giữa tháng)
                var dangKyDvs = await _context.DangKyDichVus
                    .Include(d => d.DichVuChiNhanh).ThenInclude(dcn => dcn.DichVu)
                    .Where(d => d.PhongTroId == p.PhongTroId)
                    .ToListAsync();

                foreach (var dk in dangKyDvs)
                {
                    var tenDv = dk.DichVuChiNhanh.DichVu.TenDichVu.ToLower();
                    if (tenDv.Contains("điện") || tenDv.Contains("nước")) continue;

                    if (dk.NgayBatDau.Date <= activeEnd)
                    {
                        DateTime serviceStart = dk.NgayBatDau.Date > activeStart ? dk.NgayBatDau.Date : activeStart;
                        int serviceActiveDays = (activeEnd - serviceStart).Days + 1;

                        if (serviceActiveDays > 0)
                        {
                            double dvTongTien = Math.Round((dk.DichVuChiNhanh.GiaDichVu * dk.SoLuong / daysInMonth) * serviceActiveDays);
                            tongTien += dvTongTien;
                        }
                    }
                }

                item.TongTienDuKien = tongTien;

                if (!item.DaChotDienNuoc)
                {
                    item.HopDongHopLe = false;
                    item.GhiChuTrangThai = "Chưa chốt chỉ số điện nước";
                }
                else
                {
                    item.HopDongHopLe = true;
                    item.GhiChuTrangThai = activeDays < daysInMonth ? $"Sẵn sàng (Tính lẻ ngày ở: {activeDays}/{daysInMonth} ngày)" : "Sẵn sàng";
                }

                result.Add(item);
            }

            return result;
        }

        // =================== CẬP NHẬT/CHỈNH SỬA HÓA ĐƠN ===================
        public async Task<(bool IsSuccess, string ErrorMessage)> UpdateHoaDonAsync(int hoaDonId, UpdateHoaDonReq req)
        {
            var hd = await _context.HoaDons
                .Include(h => h.ChiTietHoaDonDichVus)
                .FirstOrDefaultAsync(h => h.HoaDonId == hoaDonId && !h.IsDeleted);

            if (hd == null) return (false, "Không tìm thấy hóa đơn.");
            if (hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                return (false, "Không thể chỉnh sửa hóa đơn đã được thanh toán.");

            // Xóa các dòng chi tiết cũ
            foreach (var ct in hd.ChiTietHoaDonDichVus)
            {
                _context.ChiTietHoaDons.Remove(ct);
            }

            // Chèn các dòng chi tiết mới
            var newChiTiets = new List<ChiTietHoaDon>();
            foreach (var r in req.ChiTiets)
            {
                if (string.IsNullOrWhiteSpace(r.TenDichVu))
                    return (false, "Tên dịch vụ không được để trống.");
                if (r.DonGia < 0 || r.SoLuong < 0)
                    return (false, "Đơn giá và số lượng phải lớn hơn hoặc bằng 0.");

                newChiTiets.Add(new ChiTietHoaDon
                {
                    HoaDonId = hoaDonId,
                    TenDichVu = r.TenDichVu,
                    DonGia = r.DonGia,
                    SoLuong = r.SoLuong,
                    TongTien = r.DonGia * r.SoLuong,
                    DichVuId = r.DichVuId > 0 ? r.DichVuId : null
                });
            }

            hd.ChiTietHoaDonDichVus = newChiTiets;
            hd.TongTien = newChiTiets.Sum(x => x.TongTien);
            hd.NgayCapNhat = DateTime.UtcNow;

            _context.HoaDons.Update(hd);
            await _context.SaveChangesAsync();

            return (true, null);
        }

        // =================== DANH SÁCH HÓA ĐƠN ===================
        public async Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai)
        {
            var query = _context.HoaDons
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Where(h => !h.IsDeleted);

            if (chiNhanhId > 0)
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == chiNhanhId);
            if (thang > 0)
                query = query.Where(h => h.Thang == thang);
            if (nam > 0)
                query = query.Where(h => h.Nam == nam);
            if (trangThai >= 0)
                query = query.Where(h => (int)h.TrangThaiHoaDon == trangThai);

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string s = request.SearchValue.ToLower();
                query = query.Where(h => h.MaHoaDon.ToLower().Contains(s) ||
                                         h.HopDong.PhongTro.SoPhong.ToLower().Contains(s) ||
                                         h.HopDong.NguoiThue.HoVaTen.ToLower().Contains(s));
            }

            int totalRecords = await query.CountAsync();

            query = query.OrderByDescending(h => h.HoaDonId);

            var data = await query
                .Skip(request.Start)
                .Take(request.Length)
                .Select(h => new HoaDonRes
                {
                    HoaDonId = h.HoaDonId,
                    MaHoaDon = h.MaHoaDon,
                    HopDongId = h.HopDongId,
                    MaHopDong = h.HopDong.MaHopDong,
                    TenPhong = h.HopDong.PhongTro.SoPhong,
                    TenNguoiThue = h.HopDong.NguoiThue.HoVaTen,
                    TenChiNhanh = h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    Thang = h.Thang,
                    Nam = h.Nam,
                    TongTien = h.TongTien,
                    TrangThaiHoaDon = h.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan ? "Đã thanh toán" : "Chưa thanh toán",
                    TrangThaiHoaDonValue = (int)h.TrangThaiHoaDon,
                    NgayTao = h.NgayTao.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return new DataTableResponse<HoaDonRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data = data
            };
        }

        // =================== CHI TIẾT HÓA ĐƠN ===================
        public async Task<HoaDonChiTietRes> GetHoaDonByIdAsync(int id)
        {
            var hd = await _context.HoaDons
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Include(h => h.ChiTietHoaDonDichVus).ThenInclude(ct => ct.DichVu)
                .Include(h => h.LichSuThanhToans).ThenInclude(l => l.NguoiXacNhan)
                .FirstOrDefaultAsync(h => h.HoaDonId == id && !h.IsDeleted);

            if (hd == null) return null;

            // Lấy đơn vị của dịch vụ Điện và Nước từ Database làm fallback
            var dienDichVu = await _context.DichVus.FirstOrDefaultAsync(d => (d.TenDichVu.Contains("Điện") || d.TenDichVu.Contains("điện")) && !d.IsDeleted);
            var nuocDichVu = await _context.DichVus.FirstOrDefaultAsync(d => (d.TenDichVu.Contains("Nước") || d.TenDichVu.Contains("nước")) && !d.IsDeleted);

            string donViDien = dienDichVu?.DonVi ?? "kWh";
            string donViNuoc = nuocDichVu?.DonVi ?? "m³";

            return new HoaDonChiTietRes
            {
                HoaDonId = hd.HoaDonId,
                MaHoaDon = hd.MaHoaDon,
                TenPhong = hd.HopDong.PhongTro.SoPhong,
                TenNguoiThue = hd.HopDong.NguoiThue.HoVaTen,
                TenChiNhanh = hd.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                DiaChiChiNhanh = hd.HopDong.PhongTro.ChiNhanh.DiaChi,
                Thang = hd.Thang,
                Nam = hd.Nam,
                TongTien = hd.TongTien,
                TrangThaiHoaDon = hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan ? "Đã thanh toán" : "Chưa thanh toán",
                NgayTao = hd.NgayTao.ToString("dd/MM/yyyy HH:mm"),
                ChiTietHoaDons = hd.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).Select(ct => new ChiTietHoaDonRes
                {
                    ChiTietHoaDonId = ct.ChiTietHoaDonId,
                    TenDichVu = ct.TenDichVu,
                    DonGia = ct.DonGia,
                    SoLuong = ct.SoLuong,
                    TongTien = ct.TongTien,
                    DonVi = ct.DichVu != null ? ct.DichVu.DonVi : 
                            (ct.TenDichVu.Contains("Tiền thuê phòng") ? "Tháng" : 
                            (ct.TenDichVu.ToLower().Contains("điện") ? donViDien : 
                            (ct.TenDichVu.ToLower().Contains("nước") ? donViNuoc : "")))
                }).ToList(),
                LichSuThanhToans = hd.LichSuThanhToans.Where(x => !x.IsDeleted).Select(ls => new LichSuThanhToanRes
                {
                    LichSuThanhToanId = ls.LichSuThanhToanId,
                    MaGiaoDich = ls.MaGiaoDich,
                    SoTienThanhToan = ls.SoTienThanhToan,
                    PhuongThucThanhToan = ls.PhuongThucThanhToan == PhuongThucThanhToan.TienMat ? "Tiền mặt" : "Chuyển khoản",
                    NgayThanhToan = ls.NgayThanhToan.ToString("dd/MM/yyyy HH:mm"),
                    NguoiXacNhan = ls.NguoiXacNhan?.TenDangNhap ?? "",
                    GhiChu = ls.GhiChu
                }).ToList()
            };
        }

        // =================== THU TIỀN ===================
        public async Task<(bool IsSuccess, string ErrorMessage)> ThuTienAsync(int hoaDonId, int phuongThuc, string ghiChu, int nguoiXacNhanId)
        {
            var hd = await _context.HoaDons.FirstOrDefaultAsync(h => h.HoaDonId == hoaDonId && !h.IsDeleted);
            if (hd == null) return (false, "Không tìm thấy hóa đơn.");
            if (hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                return (false, "Hóa đơn này đã được thanh toán trước đó.");

            var lichSu = new LichSuThanhToan
            {
                MaGiaoDich = $"GD-{DateTime.UtcNow:yyyyMMddHHmmss}-{hoaDonId}",
                HoaDonId = hoaDonId,
                NguoiXacNhanId = nguoiXacNhanId,
                SoTienThanhToan = hd.TongTien,
                PhuongThucThanhToan = (PhuongThucThanhToan)phuongThuc,
                NgayThanhToan = DateTime.UtcNow,
                GhiChu = ghiChu
            };

            _context.LichSuThanhToans.Add(lichSu);

            hd.TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan;
            hd.NgayCapNhat = DateTime.UtcNow;
            _context.HoaDons.Update(hd);

            await _context.SaveChangesAsync();
            return (true, null);
        }

        // =================== XÓA HÓA ĐƠN ===================
        public async Task<(bool IsSuccess, string ErrorMessage)> DeleteHoaDonAsync(int id)
        {
            var hd = await _context.HoaDons.FirstOrDefaultAsync(h => h.HoaDonId == id && !h.IsDeleted);
            if (hd == null) return (false, "Không tìm thấy hóa đơn.");
            if (hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                return (false, "Không thể xóa hóa đơn đã thanh toán.");

            hd.IsDeleted = true;
            hd.NgayCapNhat = DateTime.UtcNow;
            _context.HoaDons.Update(hd);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        // =================== XUẤT EXCEL ===================
        public async Task<byte[]> ExportExcelAsync(int hoaDonId)
        {
            var hd = await GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return null;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Hóa đơn");

            // Header info
            ws.Cell(1, 1).Value = "HÓA ĐƠN THANH TOÁN";
            ws.Range(1, 1, 1, 6).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 6).Style.Font.FontSize = 16;
            ws.Range(1, 1, 1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(3, 1).Value = "Mã hóa đơn:"; ws.Cell(3, 2).Value = hd.MaHoaDon;
            ws.Cell(4, 1).Value = "Chi nhánh:"; ws.Cell(4, 2).Value = hd.TenChiNhanh;
            ws.Cell(5, 1).Value = "Phòng:"; ws.Cell(5, 2).Value = hd.TenPhong;
            ws.Cell(6, 1).Value = "Người thuê:"; ws.Cell(6, 2).Value = hd.TenNguoiThue;
            ws.Cell(7, 1).Value = "Kỳ thanh toán:"; ws.Cell(7, 2).Value = $"Tháng {hd.Thang}/{hd.Nam}";
            ws.Cell(8, 1).Value = "Trạng thái:"; ws.Cell(8, 2).Value = hd.TrangThaiHoaDon;
            ws.Cell(9, 1).Value = "Ngày tạo:"; ws.Cell(9, 2).Value = hd.NgayTao;

            for (int r = 3; r <= 9; r++)
            {
                ws.Cell(r, 1).Style.Font.Bold = true;
            }

            // Table header
            int row = 11;
            ws.Cell(row, 1).Value = "STT";
            ws.Cell(row, 2).Value = "Tên dịch vụ";
            ws.Cell(row, 3).Value = "Đơn giá";
            ws.Cell(row, 4).Value = "Số lượng";
            ws.Cell(row, 5).Value = "Đơn vị";
            ws.Cell(row, 6).Value = "Thành tiền";

            var headerRange = ws.Range(row, 1, row, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Table data
            int stt = 1;
            foreach (var ct in hd.ChiTietHoaDons)
            {
                row++;
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = ct.TenDichVu;
                ws.Cell(row, 3).Value = ct.DonGia;
                ws.Cell(row, 4).Value = ct.SoLuong;
                ws.Cell(row, 5).Value = string.IsNullOrEmpty(ct.DonVi) ? "-" : ct.DonVi;
                ws.Cell(row, 6).Value = ct.TongTien;
                
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            }

            // Total row
            row++;
            ws.Cell(row, 1).Value = "";
            ws.Range(row, 1, row, 5).Merge();
            ws.Cell(row, 5).Value = "TỔNG CỘNG:";
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 6).Value = hd.TongTien;
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;

            // Border
            var tableRange = ws.Range(11, 1, row, 6);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Column widths
            ws.Column(1).Width = 6;
            ws.Column(2).Width = 35;
            ws.Column(3).Width = 15;
            ws.Column(4).Width = 12;
            ws.Column(5).Width = 12;
            ws.Column(6).Width = 18;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // =================== XUẤT PDF ===================
        public async Task<byte[]> ExportPdfAsync(int hoaDonId)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var hd = await GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return null;

            byte[] qrBytes = null;
            string bankId = "";
            string accountNumber = "";
            string accountName = "";

            if (hd.TrangThaiHoaDon == "Chưa thanh toán")
            {
                bankId = _configuration["VietQRSettings:BankId"] ?? "MB";
                accountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "";
                accountName = _configuration["VietQRSettings:AccountName"] ?? "";
                
                string memo = $"THANH TOAN {hd.MaHoaDon}";
                string qrString = Helpers.VietQRHelper.GenerateVietQRString(bankId, accountNumber, hd.TongTien, memo);
                qrBytes = Helpers.VietQRHelper.GenerateQRCodePNGBytes(qrString);
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("HÓA ĐƠN THANH TOÁN").Bold().FontSize(20).FontColor(Colors.Blue.Darken2);
                        col.Item().AlignCenter().Text($"{hd.TenChiNhanh}").FontSize(12).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrEmpty(hd.DiaChiChiNhanh))
                            col.Item().AlignCenter().Text($"Địa chỉ: {hd.DiaChiChiNhanh}").FontSize(10).FontColor(Colors.Grey.Medium);
                        col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Info rows
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Mã hóa đơn: ").Bold();
                                t.Span(hd.MaHoaDon);
                            });
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Ngày tạo: ").Bold();
                                t.Span(hd.NgayTao);
                            });
                        });

                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Phòng: ").Bold();
                                t.Span(hd.TenPhong);
                            });
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Kỳ: ").Bold();
                                t.Span($"Tháng {hd.Thang}/{hd.Nam}");
                            });
                        });

                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Người thuê: ").Bold();
                                t.Span(hd.TenNguoiThue);
                            });
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Trạng thái: ").Bold();
                                t.Span(hd.TrangThaiHoaDon).FontColor(
                                    hd.TrangThaiHoaDon == "Đã thanh toán" ? Colors.Green.Darken2 : Colors.Red.Darken2);
                            });
                        });

                        col.Item().PaddingVertical(10);

                        // Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(35);  // STT
                                columns.RelativeColumn(5);   // Tên DV
                                columns.RelativeColumn(2);   // Đơn giá
                                columns.ConstantColumn(50);  // SL
                                columns.RelativeColumn(1.5f);// Đơn vị
                                columns.RelativeColumn(2);   // Thành tiền
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter()
                                    .Text("STT").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Tên dịch vụ").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight()
                                    .Text("Đơn giá").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter()
                                    .Text("SL").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter()
                                    .Text("Đơn vị").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight()
                                    .Text("Thành tiền").FontColor(Colors.White).Bold();
                            });

                            int stt = 1;
                            foreach (var ct in hd.ChiTietHoaDons)
                            {
                                var bgColor = stt % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                                table.Cell().Background(bgColor).Padding(5).AlignCenter().Text(stt.ToString());
                                table.Cell().Background(bgColor).Padding(5).Text(ct.TenDichVu);
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text(FormatVND(ct.DonGia));
                                table.Cell().Background(bgColor).Padding(5).AlignCenter().Text(ct.SoLuong.ToString());
                                table.Cell().Background(bgColor).Padding(5).AlignCenter().Text(string.IsNullOrEmpty(ct.DonVi) ? "-" : ct.DonVi);
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text(FormatVND(ct.TongTien));
                                stt++;
                            }
                        });

                        col.Item().PaddingTop(5).AlignRight().Text(t =>
                        {
                            t.Span("TỔNG CỘNG: ").Bold().FontSize(14);
                            t.Span(FormatVND(hd.TongTien)).Bold().FontSize(14).FontColor(Colors.Red.Darken2);
                        });

                        if (qrBytes != null)
                        {
                            col.Item().PaddingTop(15).Row(row =>
                            {
                                row.RelativeItem();
                                row.ConstantItem(150).Column(c =>
                                {
                                    c.Item().AlignCenter().Text("Quét mã QR để thanh toán").FontSize(10).Italic();
                                    c.Item().PaddingTop(5).Image(qrBytes);
                                    c.Item().AlignCenter().Text(accountName).Bold().FontSize(9);
                                    c.Item().AlignCenter().Text($"{bankId} - {accountNumber}").FontSize(8);
                                });
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Ngày xuất: ").FontSize(9);
                        t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                    });
                });
            });

            return document.GeneratePdf();
        }

        private string FormatVND(double amount)
        {
            return string.Format("{0:#,##0} ₫", amount);
        }
    }
}
