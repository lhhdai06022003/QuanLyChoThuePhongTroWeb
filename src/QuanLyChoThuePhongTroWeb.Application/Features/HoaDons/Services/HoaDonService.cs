using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
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
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IInvoiceIssuanceStore? _issuanceStore;

        public HoaDonService(
            IHoaDonStore store,
            IUnitOfWork unitOfWork,
            VietQrSettings vietQrSettings,
            ILogger<HoaDonService> logger,
            IHoaDonCalculatorService calculatorService,
            IInvoiceDocumentExporter documentExporter,
            IVietQRService vietQRService,
            IEmployeeAccessService employeeAccessService,
            IEmailService emailService,
            IInvoiceIssuanceStore? issuanceStore = null)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _vietQrSettings = vietQrSettings;
            _logger = logger;
            _calculatorService = calculatorService;
            _documentExporter = documentExporter;
            _vietQRService = vietQRService;
            _employeeAccessService = employeeAccessService;
            _issuanceStore = issuanceStore;
        }

        // =================== XEM TRƯỚC PHÁT SINH HÓA ĐƠN ===================
        public async Task<List<PhatSinhPreviewRes>> PreviewPhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam, int actorId)
        {
            var result = new List<PhatSinhPreviewRes>();

            if (actorId <= 0 ||
                !await _employeeAccessService.CanPerformAsync(actorId, chiNhanhId, EmployeeActionCodes.InvoiceRead))
            {
                return result;
            }

            var phongTros = await _store.GetPhongTrosByChiNhanhIdAsync(chiNhanhId);
            if (!phongTros.Any())
                return result;

            var startOfMonthVn = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var endOfMonthVn = startOfMonthVn.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            var startOfMonthUtc = DateTime.SpecifyKind(startOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var endOfMonthUtc = DateTime.SpecifyKind(endOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var nextMonthStartUtc = DateTime.SpecifyKind(startOfMonthVn.AddMonths(1).AddHours(-7), DateTimeKind.Utc);
            var daysInMonth = DateTime.DaysInMonth(nam, thang);

            var phongIds = phongTros.Select(p => p.PhongTroId).ToList();

            var hopDongs = await _store.GetValidContractsForBillingAsync(chiNhanhId, phongIds, startOfMonthUtc, endOfMonthUtc);
            var dienNuocRecords = await _store.GetDichVuDienNuocByRoomIdsAsync(phongIds, thang, nam);
            var dienNuocDict = dienNuocRecords.ToDictionary(x => x.PhongTroId);

            var dangKyDvs = await _store.GetDangKyDichVusForBillingAsync(phongIds, startOfMonthUtc, endOfMonthUtc);
            var dangKyDvsLookup = dangKyDvs.ToLookup(d => d.PhongTroId);

            var suCosCanCong = _issuanceStore != null
                ? await _issuanceStore.GetBillableIncidentsInPeriodAsync(phongIds, startOfMonthUtc, nextMonthStartUtc)
                : await _store.GetBillableSuCosAsync(phongIds);
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
                    if (dienNuoc.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet)
                    {
                        previewItem.DaChotDienNuoc = true;
                        try
                        {
                            var dienData = _calculatorService.TinhTienDienNuoc(dienNuoc.ChiSoDienMoi, dienNuoc.ChiSoDienCu, dienNuoc.DonGiaDien, "Điện", previewItem.SoNgayO, daysInMonth);
                            var nuocData = _calculatorService.TinhTienDienNuoc(dienNuoc.ChiSoNuocMoi, dienNuoc.ChiSoNuocCu, dienNuoc.DonGiaNuoc, "Nước", previewItem.SoNgayO, daysInMonth);
                            previewItem.TongTienDuKien += dienData.SoTien + nuocData.SoTien;
                        }
                        catch (InvalidOperationException)
                        {
                            previewItem.GhiChuTrangThai = "Lỗi tính toán chỉ số điện/nước.";
                            previewItem.HopDongHopLe = false;
                        }
                    }
                    else
                    {
                        previewItem.DaChotDienNuoc = false;
                        previewItem.GhiChuTrangThai = "Chỉ số điện/nước chưa được duyệt";
                    }
                }
                else
                {
                    previewItem.DaChotDienNuoc = false;
                }

                var dsDichVu = dangKyDvsLookup[hd.PhongTroId];
                foreach (var dk in dsDichVu)
                {
                    var dichVuChiNhanh = dk.DichVuChiNhanh;
                    var dichVu = dichVuChiNhanh?.DichVu;
                    if (dichVu == null || dichVu.LoaiDichVu == LoaiDichVu.Dien || dichVu.LoaiDichVu == LoaiDichVu.Nuoc)
                        continue;

                    var dvData = _calculatorService.TinhTienDichVuCoDinh(dichVuChiNhanh!.GiaDichVu, dk.SoLuong, dichVu.TenDichVu, dk.NgayBatDau, dk.NgayKetThuc, thang, nam, hd.ThoiDiemBatDau, hd.ThoiDiemKetThuc);
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
                    if (string.IsNullOrEmpty(previewItem.GhiChuTrangThai))
                    {
                        previewItem.GhiChuTrangThai = "Chưa chốt chỉ số Đ/N";
                    }
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
        public async Task<(bool IsSuccess, string? ErrorMessage)> UpdateHoaDonAsync(int hoaDonId, UpdateHoaDonReq req, int actorId = 0)
        {
            if (actorId <= 0)
            {
                return (false, "Người thực hiện không hợp lệ.");
            }

            var branchId = await _store.GetHoaDonBranchIdAsync(hoaDonId);
            if (!branchId.HasValue)
            {
                return (false, "Không tìm thấy hóa đơn.");
            }

            var canPerform = await _employeeAccessService.CanPerformAsync(actorId, branchId.Value, EmployeeActionCodes.InvoiceDraft);
            if (!canPerform)
            {
                return (false, "Bạn không có quyền chỉnh sửa hóa đơn tại chi nhánh này.");
            }

            var hd = await _store.GetHoaDonWithDetailsForUpdateAsync(hoaDonId);

            if (hd == null) return (false, "Không tìm thấy hóa đơn.");
            if (hd.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap)
            {
                return (false, "Chỉ hóa đơn nháp mới được chỉnh sửa. Hóa đơn chờ duyệt phải được Admin trả lại trước khi sửa.");
            }
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
                    TongTien = decimal.Round(r.DonGia * r.SoLuong, 2, MidpointRounding.AwayFromZero),
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
        public Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai)
        {
            return _store.GetHoaDonsDataTableAsync(request, chiNhanhId, thang, nam, trangThai);
        }

        public async Task<DataTableResponse<HoaDonRes>> GetEmployeeInvoiceListAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai, int actorId)
        {
            var denied = new DataTableResponse<HoaDonRes>
            {
                draw = request.Draw,
                recordsTotal = 0,
                recordsFiltered = 0,
                data = new List<HoaDonRes>()
            };

            if (actorId <= 0)
            {
                return denied;
            }

            var scope = await _employeeAccessService.GetScopeAsync(actorId);
            if (scope == null)
            {
                return denied;
            }

            if (!scope.IsAdmin)
            {
                if (scope.ActiveBranchIds.Count == 0)
                {
                    return denied;
                }

                if (chiNhanhId > 0)
                {
                    if (!scope.CanAccessBranch(chiNhanhId))
                    {
                        return denied;
                    }

                    return await _store.GetHoaDonsDataTableAsync(request, chiNhanhId, thang, nam, trangThai);
                }

                return await _store.GetHoaDonsDataTableAsync(request, 0, thang, nam, trangThai, scope.ActiveBranchIds.ToList());
            }

            return await _store.GetHoaDonsDataTableAsync(request, chiNhanhId, thang, nam, trangThai);
        }

        // =================== CHI TIẾT HÓA ĐƠN ===================
        public Task<HoaDonChiTietRes?> GetHoaDonByIdAsync(int id)
        {
            return _store.GetHoaDonDetailByIdAsync(id);
        }

        public async Task<HoaDonChiTietRes?> GetEmployeeInvoiceDetailAsync(int id, int actorId)
        {
            if (actorId <= 0)
            {
                return null;
            }

            var branchId = await _store.GetHoaDonBranchIdAsync(id);
            if (!branchId.HasValue)
            {
                return null;
            }

            var canRead = await _employeeAccessService.CanPerformAsync(actorId, branchId.Value, EmployeeActionCodes.InvoiceRead);
            return canRead ? await _store.GetHoaDonDetailByIdAsync(id) : null;
        }

        // =================== THU TIỀN ===================
        // =================== THU TIỀN ===================
        public async Task<(bool IsSuccess, string? ErrorMessage)> ThuTienAsync(int hoaDonId, int phuongThuc, string ghiChu, int nguoiXacNhanId)
        {
            if (nguoiXacNhanId <= 0)
            {
                return (false, "Người thực hiện không hợp lệ.");
            }

            var branchId = await _store.GetHoaDonBranchIdAsync(hoaDonId);
            if (!branchId.HasValue)
            {
                return (false, "Không tìm thấy hóa đơn.");
            }

            var canPerform = await _employeeAccessService.CanPerformAsync(nguoiXacNhanId, branchId.Value, EmployeeActionCodes.InvoiceSend);
            if (!canPerform)
            {
                return (false, "Bạn không có quyền thu tiền hóa đơn tại chi nhánh này.");
            }

            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var hd = await _store.GetHoaDonWithDetailsForUpdateAsync(hoaDonId);
                if (hd == null || hd.IsDeleted)
                {
                    await tx.RollbackAsync();
                    return (false, "Không tìm thấy hóa đơn.");
                }

                if (hd.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
                {
                    await tx.RollbackAsync();
                    return (false, "Chỉ có thể thu tiền cho hóa đơn đã gửi đến khách thuê.");
                }

                if (hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                {
                    await tx.RollbackAsync();
                    return (false, "Hóa đơn này đã được thanh toán trước đó.");
                }

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

                await _unitOfWork.SaveChangesAsync();
                await tx.CommitAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi thu tiền hóa đơn Id={Id}", hoaDonId);
                return (false, "Lỗi hệ thống khi thu tiền hóa đơn.");
            }
        }

        // =================== XÓA / HỦY HÓA ĐƠN ===================
        public async Task<(bool IsSuccess, string? ErrorMessage)> DeleteHoaDonAsync(int id, int actorId, string lyDo, CancellationToken cancellationToken = default)
        {
            if (actorId <= 0)
            {
                return (false, "Người thực hiện không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(lyDo))
            {
                return (false, "Lý do hủy hóa đơn là bắt buộc.");
            }

            var branchId = await _store.GetHoaDonBranchIdAsync(id, cancellationToken);
            if (!branchId.HasValue)
            {
                return (false, "Không tìm thấy hóa đơn.");
            }

            var canPerform = await _employeeAccessService.CanPerformAsync(actorId, branchId.Value, EmployeeActionCodes.InvoiceCancel);
            if (!canPerform)
            {
                return (false, "Bạn không có quyền hủy hóa đơn tại chi nhánh này.");
            }

            await using var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var hd = await _store.GetHoaDonWithDetailsForUpdateAsync(id, cancellationToken);
                if (hd == null || hd.IsDeleted)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return (false, "Không tìm thấy hóa đơn.");
                }

                if (hd.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaChot && hd.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return (false, "Chỉ hóa đơn đã chốt hoặc đã gửi mới được phép hủy.");
                }

                var blockers = await _store.GetCancellationBlockersAsync(id, cancellationToken);
                if (blockers.HasPayment)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return (false, "Không thể hủy hóa đơn đã phát sinh khoản thanh toán.");
                }

                if (blockers.HasPendingRequest || blockers.HasPendingProof)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return (false, "Không thể hủy hóa đơn đang có yêu cầu hoặc minh chứng thanh toán chờ xử lý.");
                }

                hd.HuyHoaDon(actorId, lyDo.Trim());
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Lỗi khi hủy hóa đơn Id={Id}", id);
                return (false, "Lỗi hệ thống khi hủy hóa đơn.");
            }
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> CheckInvoicePermissionAsync(int hoaDonId, int actorId, string actionCode)
        {
            if (actorId <= 0)
            {
                return (false, "Người thực hiện không hợp lệ.");
            }

            var branchId = await _store.GetHoaDonBranchIdAsync(hoaDonId);
            if (!branchId.HasValue)
            {
                return (false, "Không tìm thấy hóa đơn.");
            }

            var canPerform = await _employeeAccessService.CanPerformAsync(actorId, branchId.Value, actionCode);
            if (!canPerform)
            {
                return (false, $"Bạn không có quyền thực hiện thao tác '{actionCode}' đối với hóa đơn này.");
            }

            return (true, string.Empty);
        }

        // =================== XUẤT EXCEL ===================
        public async Task<byte[]?> ExportExcelAsync(int hoaDonId)
        {
            var hd = await GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return null;

            return _documentExporter.ExportExcel(hd);
        }

        public async Task<byte[]?> ExportEmployeeExcelAsync(int hoaDonId, int actorId)
        {
            var hd = await GetEmployeeInvoiceDetailAsync(hoaDonId, actorId);
            return hd == null ? null : _documentExporter.ExportExcel(hd);
        }

        // =================== XUẤT PDF ===================
        public async Task<byte[]?> ExportPdfAsync(int hoaDonId)
        {
            var hd = await GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return null;

            return ExportPdf(hd);
        }

        public async Task<byte[]?> ExportEmployeePdfAsync(int hoaDonId, int actorId)
        {
            var hd = await GetEmployeeInvoiceDetailAsync(hoaDonId, actorId);
            return hd == null ? null : ExportPdf(hd);
        }

        private byte[] ExportPdf(HoaDonChiTietRes hd)
        {
            return InvoicePdfComposer.Compose(hd, _vietQRService, _vietQrSettings, _documentExporter);
        }

        public async Task<List<HoaDonRes>> GetDanhSachHoaDonChuaThanhToanAsync(int chiNhanhId, int thang, int nam)
        {
            var result = await _store.GetUnpaidInvoicesAsync(chiNhanhId, thang, nam);
            return result.ToList();
        }

        public async Task<List<HoaDonRes>> GetEmployeeUnpaidInvoicesAsync(int chiNhanhId, int thang, int nam, int actorId)
        {
            if (actorId <= 0 ||
                !await _employeeAccessService.CanPerformAsync(actorId, chiNhanhId, EmployeeActionCodes.InvoiceRead))
            {
                return new List<HoaDonRes>();
            }

            return await GetDanhSachHoaDonChuaThanhToanAsync(chiNhanhId, thang, nam);
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

        public async Task<bool> CanTenantRequestPaymentAsync(int hoaDonId, int nguoiThueId, CancellationToken cancellationToken = default)
        {
            var isOwn = await _store.CheckHoaDonOwnershipAsync(hoaDonId, nguoiThueId, cancellationToken);
            if (!isOwn) return false;

            var hd = await _store.GetActiveHoaDonByIdAsync(hoaDonId, cancellationToken);
            if (hd == null || hd.IsDeleted) return false;

            return hd.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui
                && hd.TrangThaiHoaDon != TrangThaiHoaDon.DaThanhToan;
        }
    }
}
