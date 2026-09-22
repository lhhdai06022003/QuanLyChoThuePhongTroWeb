using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Services
{
    public class YeuCauSuCoService : IYeuCauSuCoService
    {
        private readonly IYeuCauSuCoStore _store;
        private readonly IUnitOfWork _unitOfWork;

        public YeuCauSuCoService(IYeuCauSuCoStore store, IUnitOfWork unitOfWork)
        {
            _store = store;
            _unitOfWork = unitOfWork;
        }

        private static YeuCauSuCoRes MapToRes(YeuCauSuCo entity)
        {
            return new YeuCauSuCoRes
            {
                Id = entity.Id,
                PhongTroId = entity.PhongTroId,
                NguoiThueId = entity.NguoiThueId,
                TieuDe = entity.TieuDe,
                MoTa = entity.MoTa,
                HinhAnhUrl = entity.HinhAnhUrl,
                TrangThai = (AppTrangThaiSuCo)(int)entity.TrangThai,
                ChiPhiSuaChua = entity.ChiPhiSuaChua,
                CongVaoHoaDon = entity.CongVaoHoaDon,
                LyDoTuChoi = entity.LyDoTuChoi,
                GhiChuAdmin = entity.GhiChuAdmin,
                NgayGui = entity.NgayGui,
                NgayXuLy = entity.NgayXuLy,
                PhongTro = new PhongTroInfo
                {
                    SoPhong = entity.PhongTro?.SoPhong ?? "",
                    ChiNhanh = new ChiNhanhInfo
                    {
                        TenChiNhanh = entity.PhongTro?.ChiNhanh?.TenChiNhanh ?? ""
                    }
                },
                NguoiThue = new NguoiThueInfo
                {
                    HoVaTen = entity.NguoiThue?.HoVaTen ?? "",
                    SoDienThoai = entity.NguoiThue?.SoDienThoai ?? ""
                }
            };
        }

        public async Task<List<YeuCauSuCoRes>> GetAllAsync(int? chiNhanhId, AppTrangThaiSuCo? trangThai, int? soThang = 6)
        {
            var domainTrangThai = trangThai.HasValue ? (TrangThaiSuCo?)(int)trangThai.Value : null;
            var list = await _store.GetAllAsync(chiNhanhId, domainTrangThai, soThang);
            return list.Select(MapToRes).ToList();
        }

        public async Task<List<YeuCauSuCoRes>> GetByNguoiThueAsync(int nguoiThueId)
        {
            var list = await _store.GetByNguoiThueAsync(nguoiThueId);
            return list.Select(MapToRes).ToList();
        }

        public async Task<YeuCauSuCoRes?> GetByIdAsync(int id)
        {
            var entity = await _store.GetByIdAsync(id);
            return entity == null ? null : MapToRes(entity);
        }

        public async Task<bool> CreateAsync(CreateYeuCauSuCoReq req)
        {
            var entity = new YeuCauSuCo
            {
                PhongTroId = req.PhongTroId,
                NguoiThueId = req.NguoiThueId,
                TieuDe = req.TieuDe,
                MoTa = req.MoTa ?? string.Empty,
                HinhAnhUrl = req.HinhAnhUrl,
                TrangThai = (TrangThaiSuCo)(int)req.TrangThai,
                NgayGui = DateTime.UtcNow,
                IsDeleted = false
            };
            _store.Add(entity);
            return await _unitOfWork.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateStatusAsync(int id, AppTrangThaiSuCo trangThai, decimal chiPhi, bool congVaoHoaDon, string? lyDoTuChoi, string? ghiChuAdmin)
        {
            var suco = await _store.GetByIdAsync(id);
            if (suco == null || suco.IsDeleted) return false;

            var domainTrangThai = (TrangThaiSuCo)(int)trangThai;
            suco.TrangThai = domainTrangThai;
            suco.ChiPhiSuaChua = chiPhi;
            suco.CongVaoHoaDon = congVaoHoaDon;
            suco.GhiChuAdmin = ghiChuAdmin;
            
            if (domainTrangThai == TrangThaiSuCo.DaHuy || domainTrangThai == TrangThaiSuCo.DaHoanThanh)
            {
                suco.NgayXuLy = DateTime.UtcNow;
            }

            if (domainTrangThai == TrangThaiSuCo.DaHuy)
            {
                suco.LyDoTuChoi = lyDoTuChoi;
            }
            else
            {
                suco.LyDoTuChoi = null;
            }

            _store.Update(suco);
            return await _unitOfWork.SaveChangesAsync() > 0;
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            var suco = await _store.GetByIdAsync(id);
            if (suco == null) return false;

            suco.IsDeleted = true;
            _store.Update(suco);
            return await _unitOfWork.SaveChangesAsync() > 0;
        }
    }
}
