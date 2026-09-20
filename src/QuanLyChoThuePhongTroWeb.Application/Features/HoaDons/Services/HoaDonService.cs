using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public class HoaDonService : IHoaDonService
    {
        private readonly IHoaDonStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly VietQrSettings _vietQrSettings;
        private readonly ILogger<HoaDonService> _logger;
        private readonly IHoaDonCalculatorService _calculatorService;
        private readonly IInvoiceDocumentExporter _documentExporter;
        private readonly IVietQRService _vietQRService;

        public HoaDonService(
            IHoaDonStore store,
            IUnitOfWork unitOfWork,
            VietQrSettings vietQrSettings,
            ILogger<HoaDonService> logger,
            IHoaDonCalculatorService calculatorService,
            IInvoiceDocumentExporter documentExporter,
            IVietQRService vietQRService)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _vietQrSettings = vietQrSettings;
            _logger = logger;
            _calculatorService = calculatorService;
            _documentExporter = documentExporter;
            _vietQRService = vietQRService;
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

                var chiNhanh = await _store.GetChiNhanhByIdAsync(chiNhanhId);
                if (chiNhanh == null)
                {
                    result.IsSuccess = false;
                    result.Message = "Không tìm thấy chi nhánh.";
                    return result;
                }

                // 2. Lấy TẤT CẢ hợp đồng thuộc danh sách phòng được chọn và có hiệu lực trong tháng
                var hopDongs = await _store.GetValidContractsForBillingAsync(chiNhanhId, selectedPhongTroIds, startOfMonthUtc, endOfMonthUtc);

                if (!hopDongs.Any())
                {
                    result.IsSuccess = false;
                    result.Message = "Không tìm thấy hợp đồng hợp lệ nào cho các phòng đã chọn.";
                    return result;
                }

                var hopDongIds = hopDongs.Select(h => h.HopDongId).ToList();
                var selectedPhongIds = hopDongs.Select(h => h.PhongTroId).Distinct().ToList();

                // 3. Tải trước dữ liệu
                var existingInvoiceHopDongIds = await _store.GetExistingInvoiceContractIdsAsync(hopDongIds, thang, nam);
                var existingInvoiceHopDongIdsSet = new HashSet<int>(existingInvoiceHopDongIds);

                var dienNuocRecords = await _store.GetDichVuDienNuocByRoomIdsAsync(selectedPhongIds, thang, nam);
                var dienNuocDict = dienNuocRecords.ToDictionary(x => x.PhongTroId);

                var dangKyDvs = await _store.GetDangKyDichVusForBillingAsync(selectedPhongIds, startOfMonthUtc, endOfMonthUtc);
                var dangKyDvsLookup = dangKyDvs.ToLookup(d => d.PhongTroId);

                var suCosCanCong = await _store.GetBillableSuCosAsync(selectedPhongIds);
                var suCosLookup = suCosCanCong.ToLookup(x => x.PhongTroId);

                var validInvoices = new List<HoaDon>();
                var suCosToUpdate = new List<YeuCauSuCo>();

                // 4. Lặp qua TỪNG HỢP ĐỒNG để phát sinh hóa đơn
                foreach (var hd in hopDongs)
                {
                    string baseRoomName = hd.PhongTro.SoPhong;

                    if (existingInvoiceHopDongIdsSet.Contains(hd.HopDongId))
                    {
                        result.Skipped.Add($"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Đã tồn tại hóa đơn trong tháng.");
                        continue;
                    }

                    var chiTietList = new List<ChiTietHoaDon>();
                    double tongTien = 0;

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

                    int? dienNuocId = null;
                    if (dienNuocDict.TryGetValue(hd.PhongTroId, out var dienNuoc))
                    {
                        dienNuocId = dienNuoc.DichVuDienNuocCuaPhongId;
                        try
                        {
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
                    await _store.AddInvoicesAsync(validInvoices);

                    if (suCosToUpdate.Any())
                    {
                        _store.UpdateSuCos(suCosToUpdate.Distinct());
                    }

                    await _unitOfWork.SaveChangesAsync();
                }

                result.SoHoaDonMoi = validInvoices.Count;
                result.Message = $"Phát sinh thành công {validInvoices.Count} hóa đơn.";
                if (result.Skipped.Any())
                {
                    result.Message += $" Bỏ qua {result.Skipped.Count} hợp đồng (xem chi tiết).";
                }

                return result;
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                _logger.LogError(ex, "Lỗi khi phát sinh hóa đơn chi nhánh {ChiNhanhId}, tháng {Thang}/{Nam}. Inner: {InnerMessage}", chiNhanhId, thang, nam, innerMsg);
                result.IsSuccess = false;
                result.Message = ex.GetType().Name.Contains("DbUpdate")
                    ? "Hợp đồng đã phát sinh hóa đơn trong tháng này (Lỗi dữ liệu trùng lặp). Vui lòng thử lại."
                    : $"Lỗi hệ thống: {innerMsg}";
                return result;
            }
        }

        // =================== XEM TRƯỚC PHÁT SINH HÓA ĐƠN ===================
        public async Task<List<PhatSinhPreviewRes>> PreviewPhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam)
        {
            var result = new List<PhatSinhPreviewRes>();

            var phongTros = await _store.GetPhongTrosByChiNhanhIdAsync(chiNhanhId);
            if (!phongTros.Any())
                return result;

            var startOfMonthVn = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var endOfMonthVn = startOfMonthVn.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            var startOfMonthUtc = DateTime.SpecifyKind(startOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var endOfMonthUtc = DateTime.SpecifyKind(endOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var daysInMonth = DateTime.DaysInMonth(nam, thang);

            var phongIds = phongTros.Select(p => p.PhongTroId).ToList();

            var hopDongs = await _store.GetValidContractsForBillingAsync(chiNhanhId, phongIds, startOfMonthUtc, endOfMonthUtc);
            var dienNuocRecords = await _store.GetDichVuDienNuocByRoomIdsAsync(phongIds, thang, nam);
            var dienNuocDict = dienNuocRecords.ToDictionary(x => x.PhongTroId);

            var dangKyDvs = await _store.GetDangKyDichVusForBillingAsync(phongIds, startOfMonthUtc, endOfMonthUtc);
            var dangKyDvsLookup = dangKyDvs.ToLookup(d => d.PhongTroId);

            var suCosCanCong = await _store.GetBillableSuCosAsync(phongIds);
            var suCosLookup = suCosCanCong.ToLookup(x => x.PhongTroId);

            var hopDongIds = hopDongs.Select(h => h.HopDongId).ToList();
            var existingInvoiceHopDongIds = await _store.GetExistingInvoiceContractIdsAsync(hopDongIds, thang, nam);
            var existingInvoiceHopDongIdsSet = new HashSet<int>(existingInvoiceHopDongIds);

            var phongIdsCoHopDong = hopDongs.Select(h => h.PhongTroId).Distinct().ToHashSet();

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
        public async Task<(bool IsSuccess, string? ErrorMessage)> UpdateHoaDonAsync(int hoaDonId, UpdateHoaDonReq req)
        {
            var hd = await _store.GetHoaDonWithDetailsForUpdateAsync(hoaDonId);

            if (hd == null) return (false, "Không tìm thấy hóa đơn.");
            if (hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                return (false, "Không thể chỉnh sửa hóa đơn đã được thanh toán.");

            _store.RemoveChiTietHoaDons(hd.ChiTietHoaDonDichVus);

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

            _store.UpdateHoaDon(hd);
            await _unitOfWork.SaveChangesAsync();

            return (true, string.Empty);
        }

        // =================== DANH SÁCH HÓA ĐƠN ===================
        public async Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai)
        {
            return await _store.GetHoaDonsDataTableAsync(request, chiNhanhId, thang, nam, trangThai);
        }

        // =================== CHI TIẾT HÓA ĐƠN ===================
        public async Task<HoaDonChiTietRes?> GetHoaDonByIdAsync(int id)
        {
            return await _store.GetHoaDonDetailByIdAsync(id);
        }

        // =================== THU TIỀN ===================
        public async Task<(bool IsSuccess, string? ErrorMessage)> ThuTienAsync(int hoaDonId, int phuongThuc, string ghiChu, int nguoiXacNhanId)
        {
            var hd = await _store.GetActiveHoaDonByIdAsync(hoaDonId);
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

            await _store.AddLichSuThanhToanAsync(lichSu);

            hd.TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan;
            hd.NgayCapNhat = DateTime.UtcNow;
            _store.UpdateHoaDon(hd);

            await _unitOfWork.SaveChangesAsync();
            return (true, string.Empty);
        }

        // =================== XÓA HÓA ĐƠN ===================
        public async Task<(bool IsSuccess, string? ErrorMessage)> DeleteHoaDonAsync(int id)
        {
            var hd = await _store.GetActiveHoaDonByIdAsync(id);
            if (hd == null) return (false, "Không tìm thấy hóa đơn.");
            if (hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                return (false, "Không thể xóa hóa đơn đã thanh toán.");

            hd.IsDeleted = true;
            hd.NgayCapNhat = DateTime.UtcNow;
            _store.UpdateHoaDon(hd);
            await _unitOfWork.SaveChangesAsync();
            return (true, string.Empty);
        }

        // =================== XUẤT EXCEL ===================
        public async Task<byte[]?> ExportExcelAsync(int hoaDonId)
        {
            var hd = await GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return null;

            return _documentExporter.ExportExcel(hd);
        }

        // =================== XUẤT PDF ===================
        public async Task<byte[]?> ExportPdfAsync(int hoaDonId)
        {
            var hd = await GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return null;

            byte[]? qrBytes = null;
            string bankId = "";
            string accountNumber = "";
            string accountName = "";

            if (hd.TrangThaiHoaDon == "Chưa thanh toán")
            {
                bankId = _vietQrSettings.BankId ?? "MB";
                accountNumber = _vietQrSettings.AccountNumber ?? "";
                accountName = _vietQrSettings.AccountName ?? "";

                string memo = $"THANH TOAN {hd.MaHoaDon}";
                string qrString = _vietQRService.GenerateVietQRString(bankId, accountNumber, hd.TongTien, memo);
                qrBytes = _vietQRService.GenerateQRCodePNGBytes(qrString);
            }

            return _documentExporter.ExportPdf(hd, qrBytes, bankId, accountNumber, accountName);
        }

        public async Task<List<HoaDonRes>> GetDanhSachHoaDonChuaThanhToanAsync(int chiNhanhId, int thang, int nam)
        {
            var result = await _store.GetUnpaidInvoicesAsync(chiNhanhId, thang, nam);
            return result.ToList();
        }

        public async Task<List<HoaDonRes>> GetHoaDonsByNguoiThueIdAsync(int nguoiThueId)
        {
            var hopDongIds = await _store.GetContractIdsByTenantIdAsync(nguoiThueId);

            if (!hopDongIds.Any())
            {
                return new List<HoaDonRes>();
            }

            var hoaDons = await _store.GetInvoicesByContractIdsAsync(hopDongIds);
            return hoaDons.ToList();
        }

        public async Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId)
        {
            return await _store.CheckHoaDonOwnershipAsync(hoaDonId, nguoiThueId);
        }
    }
}
