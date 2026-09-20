using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.Services
{
    public class ChiNhanhService : IChiNhanhService
    {
        private readonly IChiNhanhStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ChiNhanhService> _logger;

        public ChiNhanhService(
            IChiNhanhStore store,
            IUnitOfWork unitOfWork,
            ILogger<ChiNhanhService> logger)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        private static ChiNhanhRes MapToRes(ChiNhanh entity)
        {
            return new ChiNhanhRes
            {
                ChiNhanhId = entity.ChiNhanhId,
                MaChiNhanh = entity.MaChiNhanh,
                TenChiNhanh = entity.TenChiNhanh,
                DiaChi = entity.DiaChi,
                MoTa = entity.MoTa,
                SoDienThoai = entity.SoDienThoai,
                NgayTao = entity.NgayTao
            };
        }

        public async Task<IEnumerable<ChiNhanhRes>> DanhSachChiNhanh()
        {
            var list = await _store.GetAllActiveAsync();
            return list.Select(MapToRes);
        }

        public async Task<ChiNhanhRes?> GetChiNhanh(int id)
        {
            var entity = await _store.GetByIdAsync(id);
            return entity == null ? null : MapToRes(entity);
        }

        public async Task<(bool IsSuccess, string Message)> ThemChiNhanhMoi(ChiNhanhReq req)
        {
            try
            {
                string normMa = (req.MaChiNhanh ?? string.Empty).Trim().ToUpper();
                bool isDuplicateMa = await _store.ExistsMaAsync(normMa, req.ChiNhanhId);
                if (isDuplicateMa)
                {
                    return (false, "Mã chi nhánh đã tồn tại trong hệ thống!");
                }

                if (req.ChiNhanhId == 0)
                {
                    var entity = new ChiNhanh
                    {
                        MaChiNhanh = normMa,
                        TenChiNhanh = req.TenChiNhanh,
                        DiaChi = req.DiaChi,
                        SoDienThoai = req.SoDienThoai,
                        MoTa = req.MoTa,
                        NgayTao = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    _store.Add(entity);
                    await _unitOfWork.SaveChangesAsync();
                    return (true, "Thêm chi nhánh thành công!");
                }
                else
                {
                    var existing = await _store.GetByIdAsync(req.ChiNhanhId);
                    if (existing == null || existing.IsDeleted)
                    {
                        return (false, "Lỗi: Không tìm thấy chi nhánh hoặc đã bị xóa!");
                    }

                    existing.MaChiNhanh = normMa;
                    existing.TenChiNhanh = req.TenChiNhanh;
                    existing.DiaChi = req.DiaChi;
                    existing.SoDienThoai = req.SoDienThoai;
                    existing.MoTa = req.MoTa;
                    existing.NgayCapNhat = DateTime.UtcNow;

                    _store.Update(existing);
                    await _unitOfWork.SaveChangesAsync();
                    return (true, "Cập nhật chi nhánh thành công!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi thêm/cập nhật chi nhánh. Mã: {MaChiNhanh}, Tên: {TenChiNhanh}", req.MaChiNhanh, req.TenChiNhanh);
                return (false, "Lỗi hệ thống khi lưu thông tin chi nhánh.");
            }
        }

        public async Task<(bool IsSuccess, string Message)> XoaChiNhanh(int id)
        {
            try
            {
                var chiNhanh = await _store.GetByIdAsync(id);
                if (chiNhanh == null || chiNhanh.IsDeleted)
                {
                    return (false, "Không tìm thấy chi nhánh để xóa!");
                }

                chiNhanh.IsDeleted = true;
                chiNhanh.NgayCapNhat = DateTime.UtcNow;

                _store.Update(chiNhanh);
                await _unitOfWork.SaveChangesAsync();
                return (true, "Đã xóa chi nhánh thành công!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi xóa chi nhánh ID: {Id}", id);
                return (false, "Lỗi hệ thống khi xóa chi nhánh.");
            }
        }
    }
}
