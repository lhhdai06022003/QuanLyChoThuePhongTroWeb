using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.Services
{
    public class DieuKhoanMauService : IDieuKhoanMauService
    {
        private readonly IDieuKhoanMauStore _store;
        private readonly IUnitOfWork _unitOfWork;

        public DieuKhoanMauService(IDieuKhoanMauStore store, IUnitOfWork unitOfWork)
        {
            _store = store;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<DieuKhoanMauRes>> GetAllAsync()
        {
            return await _store.GetAllAsync();
        }

        public async Task<DieuKhoanMauRes?> GetByIdAsync(int id)
        {
            var entity = await _store.GetByIdAsync(id);
            if (entity == null) return null;

            return new DieuKhoanMauRes
            {
                DieuKhoanMauId = entity.DieuKhoanMauId,
                TieuDe = entity.TieuDe,
                NoiDung = entity.NoiDung,
                NgayTao = entity.NgayTao,
                NgayCapNhat = entity.NgayCapNhat
            };
        }

        public async Task<ServiceResult> CreateAsync(DieuKhoanMauReq request)
        {
            try
            {
                var entity = new DieuKhoanMau
                {
                    TieuDe = request.TieuDe,
                    NoiDung = request.NoiDung,
                    NgayTao = DateTime.UtcNow
                };
                _store.Add(entity);
                await _unitOfWork.SaveChangesAsync();
                return new ServiceResult { Success = true, Message = "Thêm điều khoản mẫu thành công" };
            }
            catch (Exception ex)
            {
                return new ServiceResult { Success = false, Message = "Có lỗi xảy ra: " + ex.Message };
            }
        }

        public async Task<ServiceResult> UpdateAsync(DieuKhoanMauReq request)
        {
            try
            {
                var entity = await _store.GetByIdAsync(request.DieuKhoanMauId);
                if (entity == null || entity.IsDeleted)
                    return new ServiceResult { Success = false, Message = "Không tìm thấy điều khoản" };

                entity.TieuDe = request.TieuDe;
                entity.NoiDung = request.NoiDung;
                entity.NgayCapNhat = DateTime.UtcNow;

                _store.Update(entity);
                await _unitOfWork.SaveChangesAsync();
                return new ServiceResult { Success = true, Message = "Cập nhật điều khoản mẫu thành công" };
            }
            catch (Exception ex)
            {
                return new ServiceResult { Success = false, Message = "Có lỗi xảy ra: " + ex.Message };
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                var entity = await _store.GetByIdAsync(id);
                if (entity == null || entity.IsDeleted)
                    return new ServiceResult { Success = false, Message = "Không tìm thấy điều khoản" };

                entity.IsDeleted = true;
                _store.Update(entity);
                await _unitOfWork.SaveChangesAsync();
                return new ServiceResult { Success = true, Message = "Xóa điều khoản mẫu thành công" };
            }
            catch (Exception ex)
            {
                return new ServiceResult { Success = false, Message = "Có lỗi xảy ra: " + ex.Message };
            }
        }
    }
}
