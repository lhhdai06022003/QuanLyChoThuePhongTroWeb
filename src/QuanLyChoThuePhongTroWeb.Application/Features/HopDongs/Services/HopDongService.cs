using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    public class HopDongService : IHopDongService
    {
        private readonly IHopDongStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordService _passwordService;

        public HopDongService(
            IHopDongStore store,
            IUnitOfWork unitOfWork,
            IPasswordService passwordService)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _passwordService = passwordService;
        }

        public async Task<DataTableResponse<HopDongRes>> DanhSachHopDongSideAsync(HopDongFilterReq request)
        {
            return await _store.GetDataTableResponseAsync(request);
        }

        public async Task<HopDongDetailRes?> GetByIdAsync(int id)
        {
            return await _store.GetDetailByIdAsync(id);
        }

        private async Task<string> GenerateMaHopDongAsync(int chiNhanhId, DateTime thoiDiemBatDau, int offset = 0)
        {
            var maChiNhanh = await _store.GetMaChiNhanhAsync(chiNhanhId) ?? $"CN{chiNhanhId}";
            string datePart = thoiDiemBatDau.ToString("yyMM");
            string prefix = $"HD-{maChiNhanh}-{datePart}-";

            int count = await _store.CountContractsWithPrefixAsync(prefix);
            string seq = (count + 1 + offset).ToString("D3");
            return $"{prefix}{seq}";
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> CreateAsync(HopDongReq input)
        {
            if (input.ThoiDiemKetThuc.HasValue && input.ThoiDiemKetThuc.Value.Date < input.ThoiDiemBatDau.Date)
            {
                return (false, "Ngày kết thúc không được trước ngày bắt đầu.");
            }

            bool isPhongDangThue = await _store.IsRoomRentedAsync(input.PhongTroId);
            if (isPhongDangThue) return (false, "Phòng trọ này hiện đang có hợp đồng hiệu lực.");

            bool isNguoiThueDaCoHopDong = await _store.IsTenantActiveInAnotherContractAsync(input.NguoiThueId);
            if (isNguoiThueDaCoHopDong)
                return (false, "Người đại diện này hiện đã đứng tên một hợp đồng đang hoạt động khác.");

            var phong = await _store.GetPhongTroByIdAsync(input.PhongTroId);
            if (phong == null) return (false, "Không tìm thấy phòng trọ.");

            string maHopDong = "";
            int maxRetries = 5;
            int retryCount = 0;
            bool isCodeUnique = false;

            while (!isCodeUnique && retryCount < maxRetries)
            {
                maHopDong = await GenerateMaHopDongAsync(phong.ChiNhanhId, input.ThoiDiemBatDau, retryCount);
                bool exists = await _store.ExistsContractWithCodeAsync(maHopDong);
                if (!exists)
                {
                    isCodeUnique = true;
                }
                else
                {
                    retryCount++;
                }
            }

            if (!isCodeUnique)
            {
                return (false, "Không thể tự động sinh mã hợp đồng duy nhất. Vui lòng thử lại.");
            }

            int totalMembersToAdd = (input.NguoiDungCoOPhongKhong ? 1 : 0) +
                                    (input.ThanhVienKhacIds != null ? input.ThanhVienKhacIds.Where(id => id != input.NguoiThueId).Distinct().Count() : 0);
            if (totalMembersToAdd > phong.SoNguoiToiDa)
            {
                return (false, $"Số lượng người đăng ký vào phòng ({totalMembersToAdd} người) vượt quá số người tối đa cho phép của phòng này ({phong.SoNguoiToiDa} người).");
            }

            var allMemberIds = new List<int>();
            if (input.NguoiDungCoOPhongKhong) allMemberIds.Add(input.NguoiThueId);
            if (input.ThanhVienKhacIds != null)
            {
                allMemberIds.AddRange(input.ThanhVienKhacIds.Where(id => id != input.NguoiThueId).Distinct());
            }

            if (allMemberIds.Any())
            {
                var overlappingMembers = await _store.GetOverlappingLivingMemberNamesAsync(allMemberIds);
                if (overlappingMembers.Any())
                {
                    return (false, $"Các thành viên sau đang ở một phòng khác có hợp đồng hoạt động: {string.Join(", ", overlappingMembers)}.");
                }
            }

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var hopDong = new HopDong
                {
                    MaHopDong = maHopDong,
                    PhongTroId = input.PhongTroId,
                    NguoiThueId = input.NguoiThueId,
                    ThoiDiemBatDau = input.ThoiDiemBatDau.ToUniversalTime(),
                    ThoiDiemKetThuc = input.ThoiDiemKetThuc?.ToUniversalTime(),
                    TienCocPhong = input.TienCocPhong,
                    TienThuePhong = input.TienThuePhong,
                    TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                    NgayTao = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _store.AddAsync(hopDong);
                await _unitOfWork.SaveChangesAsync();

                var members = new List<ChiTietThanhVienHopDong>();
                if (input.NguoiDungCoOPhongKhong)
                {
                    members.Add(new ChiTietThanhVienHopDong
                    {
                        HopDongId = hopDong.HopDongId,
                        NguoiThueId = input.NguoiThueId,
                        NgayVao = hopDong.ThoiDiemBatDau,
                        IsDeleted = false
                    });
                }

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
                    await _store.AddMembersAsync(members);
                }

                if (input.DieuKhoanMauIds != null && input.DieuKhoanMauIds.Any())
                {
                    var selectedTerms = await _store.GetActiveTermsByIdsAsync(input.DieuKhoanMauIds);
                    int thutu = 1;
                    var terms = selectedTerms.Select(term => new HopDongDieuKhoan
                    {
                        HopDongId = hopDong.HopDongId,
                        TieuDe = term.TieuDe,
                        NoiDung = term.NoiDung,
                        ThuTu = thutu++
                    }).ToList();

                    await _store.AddTermsAsync(terms);
                }

                phong.TrangThai = TrangThaiPhong.DaThue;
                phong.NgayCapNhat = DateTime.UtcNow;
                _store.UpdatePhongTro(phong);

                var nguoiThueDaiDien = await _store.GetNguoiThueByIdAsync(input.NguoiThueId);
                if (nguoiThueDaiDien != null && !string.IsNullOrWhiteSpace(nguoiThueDaiDien.Email))
                {
                    bool hasAccount = await _store.ExistsActiveAccountForTenantAsync(input.NguoiThueId);
                    if (!hasAccount)
                    {
                        var newTenantUser = new NguoiDung
                        {
                            TenDangNhap = nguoiThueDaiDien.Email,
                            Role = Role.KhachThue,
                            NguoiThueId = input.NguoiThueId,
                            IsActive = true,
                            NgayTao = DateTime.UtcNow,
                            IsDeleted = false
                        };
                        newTenantUser.MatKhauHash = _passwordService.HashPassword(nguoiThueDaiDien.SoDienThoai);
                        await _store.AddNguoiDungAsync(newTenantUser);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
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

        public async Task<(bool IsSuccess, string? ErrorMessage)> UpdateAsync(int id, HopDongReq input)
        {
            var entity = await _store.GetActiveByIdAsync(id);
            if (entity == null) return (false, "Không tìm thấy hợp đồng.");

            var targetStatus = (TrangThaiHopDong)(int)input.TrangThaiHopDong;
            if (targetStatus == TrangThaiHopDong.DaHuy)
            {
                bool hasInvoices = await _store.HasInvoicesAsync(id);
                if (hasInvoices)
                {
                    return (false, "Hợp đồng đã phát sinh giao dịch tài chính/hóa đơn, không thể hủy. Vui lòng chọn trạng thái Đã kết thúc.");
                }
            }

            if (entity.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
            {
                bool isCoreInfoChanged = input.PhongTroId != entity.PhongTroId ||
                                         input.NguoiThueId != entity.NguoiThueId ||
                                         input.TienThuePhong != entity.TienThuePhong ||
                                         input.TienCocPhong != entity.TienCocPhong ||
                                         input.ThoiDiemBatDau.ToUniversalTime().Date != entity.ThoiDiemBatDau.Date;

                if (isCoreInfoChanged)
                {
                    return (false, "Không được phép sửa đổi thông tin cốt lõi (Tiền thuê, Tiền cọc, Ngày bắt đầu, Phòng trọ, Người đại diện) của hợp đồng đang hoạt động. Vui lòng kết thúc hợp đồng này và ký hợp đồng mới nếu muốn thay đổi.");
                }
            }

            if (targetStatus == TrangThaiHopDong.DaKetThuc && !input.ThoiDiemKetThuc.HasValue)
            {
                input.ThoiDiemKetThuc = DateTime.UtcNow;
            }

            if (input.ThoiDiemKetThuc.HasValue && input.ThoiDiemKetThuc.Value.Date < input.ThoiDiemBatDau.Date)
                return (false, "Ngày kết thúc không được trước ngày bắt đầu.");

            if (targetStatus == TrangThaiHopDong.DangHoatDong && entity.TrangThaiHopDong != TrangThaiHopDong.DangHoatDong)
            {
                bool isNguoiThueDaCoHopDong = await _store.IsTenantActiveInAnotherContractAsync(entity.NguoiThueId, id);
                if (isNguoiThueDaCoHopDong)
                    return (false, "Người đại diện của hợp đồng này hiện đang đứng tên một hợp đồng hoạt động khác.");

                bool isPhongDangThue = await _store.IsRoomRentedExcludingContractAsync(entity.PhongTroId, id);
                if (isPhongDangThue)
                    return (false, "Phòng trọ này hiện đang có một hợp đồng hoạt động khác.");
            }

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (entity.TrangThaiHopDong != targetStatus)
                {
                    var phong = await _store.GetPhongTroByIdAsync(entity.PhongTroId);
                    if (phong != null)
                    {
                        if (targetStatus == TrangThaiHopDong.DaKetThuc || targetStatus == TrangThaiHopDong.DaHuy)
                        {
                            phong.TrangThai = TrangThaiPhong.Trong;

                            var activeMembers = await _store.GetActiveMembersByHopDongIdAsync(id);
                            foreach (var member in activeMembers)
                            {
                                member.NgayChuyenDi = DateTime.UtcNow;
                            }
                        }
                        else if (targetStatus == TrangThaiHopDong.DangHoatDong)
                        {
                            phong.TrangThai = TrangThaiPhong.DaThue;
                        }
                        phong.NgayCapNhat = DateTime.UtcNow;
                        _store.UpdatePhongTro(phong);
                    }
                }

                entity.ThoiDiemBatDau = input.ThoiDiemBatDau.ToUniversalTime();
                entity.ThoiDiemKetThuc = input.ThoiDiemKetThuc?.ToUniversalTime();
                entity.TienCocPhong = input.TienCocPhong;
                entity.TienThuePhong = input.TienThuePhong;
                entity.TrangThaiHopDong = targetStatus;
                entity.NgayCapNhat = DateTime.UtcNow;

                var oldTerms = await _store.GetHopDongDieuKhoansByHopDongIdAsync(id);
                _store.RemoveTerms(oldTerms);

                if (input.DieuKhoanMauIds != null && input.DieuKhoanMauIds.Any())
                {
                    var selectedTerms = await _store.GetActiveTermsByIdsAsync(input.DieuKhoanMauIds);
                    int thutu = 1;
                    var newTerms = selectedTerms.Select(term => new HopDongDieuKhoan
                    {
                        HopDongId = id,
                        TieuDe = term.TieuDe,
                        NoiDung = term.NoiDung,
                        ThuTu = thutu++
                    }).ToList();

                    await _store.AddTermsAsync(newTerms);
                }

                _store.Update(entity);
                await _unitOfWork.SaveChangesAsync();
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

        public async Task<(bool IsSuccess, string? ErrorMessage)> DeleteAsync(int id)
        {
            var entity = await _store.GetActiveByIdAsync(id);
            if (entity == null) return (false, "Không tìm thấy hợp đồng.");

            if (entity.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
            {
                return (false, "Không thể xóa hợp đồng đang trong trạng thái Hoạt động. Vui lòng thanh lý hợp đồng trước.");
            }

            entity.IsDeleted = true;
            entity.NgayCapNhat = DateTime.UtcNow;
            _store.Update(entity);

            var thanhViens = await _store.GetActiveMembersByHopDongIdAsync(id);
            foreach (var tv in thanhViens)
            {
                tv.NgayChuyenDi = DateTime.UtcNow;
            }

            bool conHopDongKhac = await _store.IsRoomRentedExcludingContractAsync(entity.PhongTroId, id);
            if (!conHopDongKhac)
            {
                var phong = await _store.GetPhongTroByIdAsync(entity.PhongTroId);
                if (phong != null)
                {
                    phong.TrangThai = TrangThaiPhong.Trong;
                    phong.NgayCapNhat = DateTime.UtcNow;
                    _store.UpdatePhongTro(phong);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task<HopDongPrintRes?> GetPrintDataAsync(int id)
        {
            return await _store.GetPrintDataAsync(id);
        }

        public async Task<IReadOnlyList<HopDongRes>> GetHopDongsByNguoiThueIdAsync(int nguoiThueId)
        {
            var hopDongs = await _store.GetHopDongsByNguoiThueIdAsync(nguoiThueId);
            return hopDongs.Select(h => new HopDongRes
            {
                HopDongId = h.HopDongId,
                MaHopDong = h.MaHopDong,
                PhongTroId = h.PhongTroId,
                SoPhong = h.PhongTro?.SoPhong ?? "",
                TenChiNhanh = h.PhongTro?.ChiNhanh?.TenChiNhanh ?? "",
                NguoiThueId = h.NguoiThueId,
                TenNguoiThue = h.NguoiThue?.HoVaTen ?? "",
                ThoiDiemBatDau = h.ThoiDiemBatDau,
                ThoiDiemKetThuc = h.ThoiDiemKetThuc,
                TienCocPhong = h.TienCocPhong,
                TienThuePhong = h.TienThuePhong,
                TrangThaiHopDong = (QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiHopDong)(int)h.TrangThaiHopDong,
                NgayTao = h.NgayTao,
                NgayCapNhat = h.NgayCapNhat
            }).ToList();
        }

        public async Task<HopDongKhachThueDetailDto?> GetChiTietHopDongKhachThueAsync(int id, int nguoiThueId)
        {
            return await _store.GetChiTietHopDongKhachThueAsync(id, nguoiThueId);
        }
    }
}
