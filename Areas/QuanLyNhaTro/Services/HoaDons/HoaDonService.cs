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
        private readonly ILogger<HoaDonService> _logger;
        private readonly IHoaDonCalculatorService _calculatorService;

        public HoaDonService(ApplicationDbContext context, Microsoft.Extensions.Configuration.IConfiguration configuration, ILogger<HoaDonService> logger, IHoaDonCalculatorService calculatorService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _calculatorService = calculatorService;
        }

        // =================== PHÁT SINH HÓA ĐƠN HÀNG LOẠT ===================
        public async Task<PhatSinhHoaDonResult> PhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam, List<int> selectedPhongTroIds)
        {
            var result = new PhatSinhHoaDonResult { IsSuccess = true, Message = "" };
            
            try
            {
                if (selectedPhongTroIds == null || !selectedPhongTroIds.Any())
                {
                    result.IsSuccess = false;
                    result.Message = "Không có phòng nào được chọn để phát sinh hóa đơn.";
                    return result;
                }

                // 1. Cấu hình thời gian chuẩn
                var startOfMonthVn = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Unspecified);
                var endOfMonthVn = startOfMonthVn.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
                var startOfMonthUtc = DateTime.SpecifyKind(startOfMonthVn.AddHours(-7), DateTimeKind.Utc);
                var endOfMonthUtc = DateTime.SpecifyKind(endOfMonthVn.AddHours(-7), DateTimeKind.Utc);
                var daysInMonth = DateTime.DaysInMonth(nam, thang);

                // Lấy chi nhánh để lấy Mã Chi Nhánh cho Hóa Đơn
                var chiNhanh = await _context.ChiNhanhs.FirstOrDefaultAsync(c => c.ChiNhanhId == chiNhanhId);
                if (chiNhanh == null)
                {
                    result.IsSuccess = false;
                    result.Message = "Không tìm thấy chi nhánh.";
                    return result;
                }

                // 2. Lấy TẤT CẢ hợp đồng thuộc danh sách phòng được chọn và có hiệu lực trong tháng
                var hopDongs = await _context.HopDongs
                    .Include(h => h.PhongTro)
                    .Include(h => h.NguoiThue)
                    .Where(h => h.PhongTro.ChiNhanhId == chiNhanhId &&
                                selectedPhongTroIds.Contains(h.PhongTroId) &&
                                !h.IsDeleted &&
                                h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                                h.ThoiDiemBatDau <= endOfMonthUtc &&
                                (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc >= startOfMonthUtc.AddDays(1)))
                    .ToListAsync();

                if (!hopDongs.Any())
                {
                    result.IsSuccess = false;
                    result.Message = "Không tìm thấy hợp đồng hợp lệ nào cho các phòng đã chọn.";
                    return result;
                }

                var hopDongIds = hopDongs.Select(h => h.HopDongId).ToList();
                var selectedPhongIds = hopDongs.Select(h => h.PhongTroId).Distinct().ToList();

                // 3. Tải trước dữ liệu
                // Hóa đơn đã tồn tại cho các HỢP ĐỒNG này trong tháng (bao gồm cả soft-deleted, khớp với filtered unique index)
                var existingInvoiceHopDongIds = await _context.HoaDons
                    .Where(x => hopDongIds.Contains(x.HopDongId) && x.Thang == thang && x.Nam == nam && !x.IsDeleted)
                    .Select(x => x.HopDongId)
                    .ToListAsync();
                var existingInvoiceHopDongIdsSet = new HashSet<int>(existingInvoiceHopDongIds);

                // Dữ liệu chốt điện nước của các phòng được chọn
                var dienNuocRecords = await _context.DichVuDienNuocCuaPhongs
                    .Where(x => selectedPhongIds.Contains(x.PhongTroId) && x.Thang == thang && x.Nam == nam && !x.IsDeleted)
                    .ToListAsync();
                var dienNuocDict = dienNuocRecords.ToDictionary(x => x.PhongTroId);

                // Dịch vụ đã đăng ký
                var dangKyDvs = await _context.DangKyDichVus
                    .Include(d => d.DichVuChiNhanh)
                        .ThenInclude(dcn => dcn.DichVu)
                    .Where(d => selectedPhongIds.Contains(d.PhongTroId) &&
                                d.NgayBatDau <= endOfMonthUtc &&
                                (d.NgayKetThuc == null || d.NgayKetThuc >= startOfMonthUtc))
                    .ToListAsync();
                var dangKyDvsLookup = dangKyDvs.ToLookup(d => d.PhongTroId);

                // Sự cố cần cộng vào hóa đơn
                var suCosCanCong = await _context.YeuCauSuCos
                    .Where(x => selectedPhongIds.Contains(x.PhongTroId)
                        && x.TrangThai == TrangThaiSuCo.DaHoanThanh
                        && x.CongVaoHoaDon && !x.IsDeleted)
                    .ToListAsync();
                var suCosLookup = suCosCanCong.ToLookup(x => x.PhongTroId);

                var validInvoices = new List<HoaDon>();
                var suCosToUpdate = new List<YeuCauSuCo>();

                // 4. Lặp qua TỪNG HỢP ĐỒNG để phát sinh hóa đơn (1 phòng có thể có nhiều HD)
                foreach (var hd in hopDongs)
                {
                    string baseRoomName = hd.PhongTro.SoPhong;
                    
                    // Kiểm tra tồn tại hóa đơn (Unique Constraint: HopDongId, Thang, Nam)
                    if (existingInvoiceHopDongIdsSet.Contains(hd.HopDongId))
                    {
                        result.Skipped.Add($"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Đã tồn tại hóa đơn trong tháng.");
                        continue;
                    }

                    var chiTietList = new List<ChiTietHoaDon>();
                    double tongTien = 0;
                    
                    // Tính số ngày ở thực tế của hợp đồng này
                    var tienPhongData = _calculatorService.TinhTienPhong(hd.TienThuePhong, hd.ThoiDiemBatDau, hd.ThoiDiemKetThuc, thang, nam);
                    if (tienPhongData.SoNgayO <= 0)
                    {
                        result.Skipped.Add($"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Không có số ngày ở thực tế.");
                        continue;
                    }

                    chiTietList.Add(new ChiTietHoaDon
                    {
                        TenDichVu = tienPhongData.DienGiai,
                        DonGia = hd.TienThuePhong,
                        SoLuong = 1,
                        TongTien = tienPhongData.SoTien
                    });
                    tongTien += tienPhongData.SoTien;

                    // Tính tiền điện nước theo tỷ lệ số ngày ở của hợp đồng này
                    int? dienNuocId = null;
                    if (dienNuocDict.TryGetValue(hd.PhongTroId, out var dienNuoc))
                    {
                        dienNuocId = dienNuoc.DichVuDienNuocCuaPhongId;
                        try
                        {
                            // Điện
                            var dienDichVu = dangKyDvsLookup[hd.PhongTroId].FirstOrDefault(d => d.DichVuChiNhanh?.DichVu?.LoaiDichVu == LoaiDichVu.Dien)?.DichVuChiNhanh?.DichVuId;
                            var dienData = _calculatorService.TinhTienDienNuoc(dienNuoc.ChiSoDienMoi, dienNuoc.ChiSoDienCu, dienNuoc.DonGiaDien, "Tiền điện", tienPhongData.SoNgayO, daysInMonth);
                            if (dienData.SoTien > 0)
                            {
                                chiTietList.Add(new ChiTietHoaDon
                                {
                                    TenDichVu = dienData.DienGiai,
                                    DonGia = dienNuoc.DonGiaDien,
                                    SoLuong = Math.Round(dienData.SoLuong, 2),
                                    TongTien = dienData.SoTien,
                                    DichVuId = dienDichVu
                                });
                                tongTien += dienData.SoTien;
                            }

                            // Nước
                            var nuocDichVu = dangKyDvsLookup[hd.PhongTroId].FirstOrDefault(d => d.DichVuChiNhanh?.DichVu?.LoaiDichVu == LoaiDichVu.Nuoc)?.DichVuChiNhanh?.DichVuId;
                            var nuocData = _calculatorService.TinhTienDienNuoc(dienNuoc.ChiSoNuocMoi, dienNuoc.ChiSoNuocCu, dienNuoc.DonGiaNuoc, "Tiền nước", tienPhongData.SoNgayO, daysInMonth);
                            if (nuocData.SoTien > 0)
                            {
                                chiTietList.Add(new ChiTietHoaDon
                                {
                                    TenDichVu = nuocData.DienGiai,
                                    DonGia = dienNuoc.DonGiaNuoc,
                                    SoLuong = Math.Round(nuocData.SoLuong, 2),
                                    TongTien = nuocData.SoTien,
                                    DichVuId = nuocDichVu
                                });
                                tongTien += nuocData.SoTien;
                            }
                        }
                        catch (InvalidOperationException ex)
                        {
                            result.Skipped.Add($"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): {ex.Message}");
                            continue;
                        }
                    }
                    else
                    {
                        result.Skipped.Add($"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Chưa chốt chỉ số điện/nước tháng này.");
                        continue;
                    }

                    // Tính tiền dịch vụ cố định (clamp theo phạm vi hợp đồng)
                    var dsDichVu = dangKyDvsLookup[hd.PhongTroId];
                    foreach (var dk in dsDichVu)
                    {
                        var dichVu = dk.DichVuChiNhanh?.DichVu;
                        if (dichVu == null || dichVu.LoaiDichVu == LoaiDichVu.Dien || dichVu.LoaiDichVu == LoaiDichVu.Nuoc) 
                            continue;

                        var dvData = _calculatorService.TinhTienDichVuCoDinh(dk.DichVuChiNhanh.GiaDichVu, dk.SoLuong, dichVu.TenDichVu, dk.NgayBatDau, dk.NgayKetThuc, thang, nam, hd.ThoiDiemBatDau, hd.ThoiDiemKetThuc);
                        if (dvData.SoTien > 0)
                        {
                            chiTietList.Add(new ChiTietHoaDon
                            {
                                TenDichVu = dvData.DienGiai,
                                DonGia = dk.DichVuChiNhanh.GiaDichVu,
                                SoLuong = dk.SoLuong,
                                TongTien = dvData.SoTien,
                                DichVuId = dichVu.DichVuId
                            });
                            tongTien += dvData.SoTien;
                        }
                    }

                    // Cộng tiền sự cố (chỉ gán cho HĐ của người báo sự cố)
                    var suCos = suCosLookup[hd.PhongTroId]
                        .Where(sc => sc.CongVaoHoaDon && sc.ChiPhiSuaChua > 0 && sc.NguoiThueId == hd.NguoiThueId)
                        .ToList();
                    foreach (var sc in suCos)
                    {
                        chiTietList.Add(new ChiTietHoaDon
                        {
                            TenDichVu = $"Sửa chữa sự cố: {sc.TieuDe}",
                            DonGia = sc.ChiPhiSuaChua,
                            SoLuong = 1,
                            TongTien = sc.ChiPhiSuaChua
                        });
                        tongTien += sc.ChiPhiSuaChua;
                        
                        sc.CongVaoHoaDon = false;
                        suCosToUpdate.Add(sc);
                    }

                    // Khởi tạo Hóa đơn (Mã Hóa Đơn Duy Nhất)
                    var hoaDon = new HoaDon
                    {
                        MaHoaDon = $"HD-{chiNhanh.MaChiNhanh}-P{baseRoomName}-{hd.HopDongId}-{thang:D2}{nam}",
                        HopDongId = hd.HopDongId,
                        Thang = thang,
                        Nam = nam,
                        TongTien = tongTien,
                        TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                        DichVuDienNuocCuaPhongId = dienNuocId,
                        ChiTietHoaDonDichVus = chiTietList
                    };

                    validInvoices.Add(hoaDon);
                    result.Successes.Add($"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Đã tạo hóa đơn.");
                }

                if (validInvoices.Any())
                {
                    _context.HoaDons.AddRange(validInvoices);
                    
                    if (suCosToUpdate.Any())
                    {
                        // Distinct sự cố để tránh cập nhật lặp lại
                        _context.YeuCauSuCos.UpdateRange(suCosToUpdate.Distinct());
                    }

                    await _context.SaveChangesAsync();
                }

                result.SoHoaDonMoi = validInvoices.Count;
                result.Message = $"Phát sinh thành công {validInvoices.Count} hóa đơn.";
                if (result.Skipped.Any())
                {
                    result.Message += $" Bỏ qua {result.Skipped.Count} hợp đồng (xem chi tiết).";
                }
                
                return result;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                var innerMsg = dbEx.InnerException != null ? dbEx.InnerException.Message : dbEx.Message;
                _logger.LogError(dbEx, "Lỗi Unique Constraint khi phát sinh hóa đơn chi nhánh {ChiNhanhId}, tháng {Thang}/{Nam}. Inner: {InnerMessage}", chiNhanhId, thang, nam, innerMsg);
                result.IsSuccess = false;
                result.Message = "Hợp đồng đã phát sinh hóa đơn trong tháng này (Lỗi dữ liệu trùng lặp). Vui lòng thử lại.";
                return result;
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                _logger.LogError(ex, "Lỗi khi phát sinh hóa đơn chi nhánh {ChiNhanhId}, tháng {Thang}/{Nam}. Inner: {InnerMessage}", chiNhanhId, thang, nam, innerMsg);
                result.IsSuccess = false;
                result.Message = $"Lỗi hệ thống: {innerMsg}";
                return result;
            }
        }

        // =================== XEM TRƯỚC PHÁT SINH HÓA ĐƠN ===================
        public async Task<List<PhatSinhPreviewRes>> PreviewPhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam)
        {
            var result = new List<PhatSinhPreviewRes>();

            // Lấy tất cả phòng trong chi nhánh
            var phongTros = await _context.PhongTros.AsNoTracking()
                .Where(p => p.ChiNhanhId == chiNhanhId && !p.IsDeleted)
                .ToListAsync();

            if (!phongTros.Any())
                return result;

            var startOfMonthVn = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var endOfMonthVn = startOfMonthVn.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            var startOfMonthUtc = DateTime.SpecifyKind(startOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var endOfMonthUtc = DateTime.SpecifyKind(endOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var daysInMonth = DateTime.DaysInMonth(nam, thang);

            var phongIds = phongTros.Select(p => p.PhongTroId).ToList();

            // Lấy TẤT CẢ hợp đồng có hiệu lực trong tháng
            var hopDongs = await _context.HopDongs.AsNoTracking()
                .Include(h => h.NguoiThue)
                .Where(h => phongIds.Contains(h.PhongTroId) &&
                            !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau <= endOfMonthUtc &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc >= startOfMonthUtc.AddDays(1)))
                .ToListAsync();

            // Điện nước
            var dienNuocRecords = await _context.DichVuDienNuocCuaPhongs.AsNoTracking()
                .Where(x => phongIds.Contains(x.PhongTroId) && x.Thang == thang && x.Nam == nam && !x.IsDeleted)
                .ToListAsync();
            var dienNuocDict = dienNuocRecords.ToDictionary(x => x.PhongTroId);

            // Dịch vụ
            var dangKyDvs = await _context.DangKyDichVus.AsNoTracking()
                .Include(d => d.DichVuChiNhanh)
                    .ThenInclude(dcn => dcn.DichVu)
                .Where(d => phongIds.Contains(d.PhongTroId) &&
                            d.NgayBatDau <= endOfMonthUtc &&
                            (d.NgayKetThuc == null || d.NgayKetThuc >= startOfMonthUtc))
                .ToListAsync();
            var dangKyDvsLookup = dangKyDvs.ToLookup(d => d.PhongTroId);

            // Sự cố cần cộng vào hóa đơn
            var suCosCanCong = await _context.YeuCauSuCos.AsNoTracking()
                .Where(x => phongIds.Contains(x.PhongTroId)
                    && x.TrangThai == TrangThaiSuCo.DaHoanThanh
                    && x.CongVaoHoaDon && !x.IsDeleted)
                .ToListAsync();
            var suCosLookup = suCosCanCong.ToLookup(x => x.PhongTroId);

            // Tải trước hóa đơn ĐÃ tồn tại (Kiểm tra theo HopDongId)
            var hopDongIds = hopDongs.Select(h => h.HopDongId).ToList();
            var existingInvoiceHopDongIds = await _context.HoaDons.AsNoTracking()
                .Where(x => hopDongIds.Contains(x.HopDongId) && x.Thang == thang && x.Nam == nam && !x.IsDeleted)
                .Select(x => x.HopDongId)
                .ToListAsync();
            var existingInvoiceHopDongIdsSet = new HashSet<int>(existingInvoiceHopDongIds);

            var phongIdsCoHopDong = hopDongs.Select(h => h.PhongTroId).Distinct().ToHashSet();
            
            // Xử lý những phòng trống (không có hợp đồng trong tháng)
            foreach (var p in phongTros)
            {
                if (!phongIdsCoHopDong.Contains(p.PhongTroId))
                {
                    result.Add(new PhatSinhPreviewRes
                    {
                        PhongTroId = p.PhongTroId,
                        HopDongId = 0,
                        SoPhong = p.SoPhong,
                        TenNguoiThue = "Trống",
                        HopDongHopLe = false,
                        DaCoHoaDon = false,
                        DaChotDienNuoc = false,
                        GhiChuTrangThai = "Không có hợp đồng"
                    });
                }
            }

            // Xử lý từng hợp đồng (để có thể xuất nhiều dòng nếu 1 phòng có nhiều hợp đồng)
            foreach (var hd in hopDongs)
            {
                var p = phongTros.First(x => x.PhongTroId == hd.PhongTroId);
                
                var previewItem = new PhatSinhPreviewRes
                {
                    PhongTroId = p.PhongTroId,
                    HopDongId = hd.HopDongId,
                    SoPhong = p.SoPhong,
                    TenNguoiThue = hd.NguoiThue?.HoVaTen ?? "Khách thuê",
                    HopDongHopLe = true,
                    TongSoNgayTrongThang = daysInMonth,
                    DaCoHoaDon = existingInvoiceHopDongIdsSet.Contains(hd.HopDongId)
                };

                var tienPhongData = _calculatorService.TinhTienPhong(hd.TienThuePhong, hd.ThoiDiemBatDau, hd.ThoiDiemKetThuc, thang, nam);
                previewItem.SoNgayO = tienPhongData.SoNgayO;
                previewItem.TienPhongDuKien = tienPhongData.SoTien;
                previewItem.TongTienDuKien += tienPhongData.SoTien;

                if (dienNuocDict.TryGetValue(hd.PhongTroId, out var dienNuoc))
                {
                    previewItem.DaChotDienNuoc = true;
                    try
                    {
                        var dienData = _calculatorService.TinhTienDienNuoc(dienNuoc.ChiSoDienMoi, dienNuoc.ChiSoDienCu, dienNuoc.DonGiaDien, "Điện", previewItem.SoNgayO, daysInMonth);
                        var nuocData = _calculatorService.TinhTienDienNuoc(dienNuoc.ChiSoNuocMoi, dienNuoc.ChiSoNuocCu, dienNuoc.DonGiaNuoc, "Nước", previewItem.SoNgayO, daysInMonth);
                        previewItem.TongTienDuKien += dienData.SoTien + nuocData.SoTien;
                    }
                    catch (InvalidOperationException ex)
                    {
                        previewItem.GhiChuTrangThai = ex.Message;
                        previewItem.HopDongHopLe = false;
                    }
                }
                else
                {
                    previewItem.DaChotDienNuoc = false;
                }

                var dsDichVu = dangKyDvsLookup[hd.PhongTroId];
                foreach (var dk in dsDichVu)
                {
                    var dichVu = dk.DichVuChiNhanh?.DichVu;
                    if (dichVu == null || dichVu.LoaiDichVu == LoaiDichVu.Dien || dichVu.LoaiDichVu == LoaiDichVu.Nuoc) 
                        continue;

                    var dvData = _calculatorService.TinhTienDichVuCoDinh(dk.DichVuChiNhanh.GiaDichVu, dk.SoLuong, dichVu.TenDichVu, dk.NgayBatDau, dk.NgayKetThuc, thang, nam, hd.ThoiDiemBatDau, hd.ThoiDiemKetThuc);
                    previewItem.TongTienDuKien += dvData.SoTien;
                }

                // Tính sự cố (match theo NguoiThueId)
                var suCos = suCosLookup[hd.PhongTroId]
                    .Where(sc => sc.NguoiThueId == hd.NguoiThueId)
                    .ToList();
                foreach (var sc in suCos)
                {
                    previewItem.TongTienDuKien += sc.ChiPhiSuaChua;
                    previewItem.SuCoCount++;
                }

                if (previewItem.DaCoHoaDon)
                {
                    previewItem.GhiChuTrangThai = "Hợp đồng đã xuất HĐ";
                }
                else if (!previewItem.DaChotDienNuoc)
                {
                    previewItem.GhiChuTrangThai = "Chưa chốt chỉ số Đ/N";
                }
                else if (previewItem.HopDongHopLe && string.IsNullOrEmpty(previewItem.GhiChuTrangThai))
                {
                    previewItem.GhiChuTrangThai = "Sẵn sàng";
                }

                result.Add(previewItem);
            }

            return result.OrderBy(x => x.SoPhong).ThenBy(x => x.TenNguoiThue).ToList();
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

            return (true, string.Empty);
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
                    NgayTao = h.NgayTao.ToString("dd/MM/yyyy HH:mm"),
                    SoDienThoai = h.HopDong.NguoiThue.SoDienThoai,
                    Email = h.HopDong.NguoiThue.Email ?? ""
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
                SoDienThoaiChiNhanh = hd.HopDong.PhongTro.ChiNhanh.SoDienThoai ?? "",
                Thang = hd.Thang,
                Nam = hd.Nam,
                TongTien = hd.TongTien,
                TrangThaiHoaDon = hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan ? "Đã thanh toán" : "Chưa thanh toán",
                NgayTao = hd.NgayTao.ToString("dd/MM/yyyy HH:mm"),
                Email = hd.HopDong.NguoiThue.Email ?? "",
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
            return (true, string.Empty);
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
            return (true, string.Empty);
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
                        t.Span(DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                    });
                });
            });

            return document.GeneratePdf();
        }

        private string FormatVND(double amount)
        {
            return string.Format("{0:#,##0} ₫", amount);
        }

        public async Task<List<HoaDonRes>> GetDanhSachHoaDonChuaThanhToanAsync(int chiNhanhId, int thang, int nam)
        {
            var query = _context.HoaDons
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Where(h => !h.IsDeleted && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan);

            if (chiNhanhId > 0)
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == chiNhanhId);
            if (thang > 0)
                query = query.Where(h => h.Thang == thang);
            if (nam > 0)
                query = query.Where(h => h.Nam == nam);

            return await query
                .OrderByDescending(h => h.HoaDonId)
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
                    TrangThaiHoaDon = "Chưa thanh toán",
                    TrangThaiHoaDonValue = (int)h.TrangThaiHoaDon,
                    NgayTao = h.NgayTao.ToString("dd/MM/yyyy HH:mm"),
                    SoDienThoai = h.HopDong.NguoiThue.SoDienThoai,
                    Email = h.HopDong.NguoiThue.Email ?? ""
                })
                .ToListAsync();
        }

        public async Task<List<HoaDonRes>> GetHoaDonsByNguoiThueIdAsync(int nguoiThueId)
        {
            var hopDongIds = await _context.HopDongs
                .Where(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted)
                .Select(x => x.HopDongId)
                .ToListAsync();

            if (!hopDongIds.Any())
            {
                return new List<HoaDonRes>();
            }

            var hoaDons = await _context.HoaDons
                .Include(x => x.HopDong).ThenInclude(h => h.PhongTro)
                .Where(x => hopDongIds.Contains(x.HopDongId) && !x.IsDeleted)
                .OrderByDescending(x => x.NgayTao)
                .Select(x => new HoaDonRes
                {
                    HoaDonId = x.HoaDonId,
                    MaHoaDon = x.MaHoaDon,
                    HopDongId = x.HopDongId,
                    MaHopDong = x.HopDong.MaHopDong,
                    TenPhong = x.HopDong.PhongTro.SoPhong,
                    Thang = x.Thang,
                    Nam = x.Nam,
                    TongTien = x.TongTien,
                    TrangThaiHoaDon = x.TrangThaiHoaDon == QuanLyChoThuePhongTroWeb.Models.TrangThaiHoaDon.DaThanhToan ? "Đã thanh toán" : "Chưa thanh toán",
                    TrangThaiHoaDonValue = (int)x.TrangThaiHoaDon,
                    NgayTao = x.NgayTao.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return hoaDons;
        }

        public async Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId)
        {
            var isOwn = await _context.HoaDons
                .Include(h => h.HopDong)
                .AnyAsync(h => h.HoaDonId == hoaDonId && h.HopDong.NguoiThueId == nguoiThueId && !h.IsDeleted);
            return isOwn;
        }
    }
}
