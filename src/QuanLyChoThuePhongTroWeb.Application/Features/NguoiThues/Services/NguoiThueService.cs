using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Services
{
    public class NguoiThueService : INguoiThueService
    {
        private readonly INguoiThueStore _store;
        private readonly IUnitOfWork _unitOfWork;

        public NguoiThueService(INguoiThueStore store, IUnitOfWork unitOfWork)
        {
            _store = store;
            _unitOfWork = unitOfWork;
        }

        private static NguoiThueRes MapToRes(NguoiThue entity)
        {
            return new NguoiThueRes
            {
                NguoiThueId = entity.NguoiThueId,
                HoVaTen = entity.HoVaTen,
                Email = entity.Email,
                SoDienThoai = entity.SoDienThoai,
                CCCD = entity.CCCD,
                NgayCapCCCD = entity.NgayCapCCCD,
                NoiCapCCCD = entity.NoiCapCCCD,
                NgaySinh = entity.NgaySinh,
                QueQuan = entity.QueQuan,
                GhiChu = entity.GhiChu,
                NgayTao = entity.NgayTao
            };
        }

        public async Task<IEnumerable<NguoiThueRes>> GetAllAsync()
        {
            var list = await _store.GetAllAsync();
            var result = new List<NguoiThueRes>();
            foreach (var item in list)
            {
                result.Add(MapToRes(item));
            }
            return result;
        }

        public async Task<IEnumerable<NguoiThueRes>> GetAvailableAsync()
        {
            var list = await _store.GetAvailableAsync();
            var result = new List<NguoiThueRes>();
            foreach (var item in list)
            {
                result.Add(MapToRes(item));
            }
            return result;
        }

        public async Task<NguoiThueRes?> GetByIdAsync(int id)
        {
            var entity = await _store.GetByIdAsync(id);
            return entity == null ? null : MapToRes(entity);
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> CreateAsync(NguoiThueReq input)
        {
            try
            {
                bool isDuplicate = await _store.ExistsDuplicateAsync(input.CCCD, input.SoDienThoai, input.Email);
                if (isDuplicate) return (false, "CCCD, Số điện thoại hoặc Email đã tồn tại trong hệ thống.");

                var entity = new NguoiThue
                {
                    HoVaTen = input.HoVaTen,
                    Email = input.Email,
                    SoDienThoai = input.SoDienThoai,
                    CCCD = input.CCCD,
                    NoiCapCCCD = input.NoiCapCCCD,
                    QueQuan = input.QueQuan,
                    GhiChu = input.GhiChu,
                    NgaySinh = input.NgaySinh?.ToUniversalTime(),
                    NgayCapCCCD = input.NgayCapCCCD?.ToUniversalTime(),
                    NgayTao = DateTime.UtcNow,
                    IsDeleted = false
                };

                _store.Add(entity);
                await _unitOfWork.SaveChangesAsync();
                return (true, string.Empty);
            }
            catch (Exception e)
            {
                string errorDetails = e.InnerException != null ? e.InnerException.Message : e.Message;
                return (false, $"Lỗi hệ thống: {errorDetails}");
            }
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> UpdateAsync(int id, NguoiThueUpdateDto input)
        {
            var entity = await _store.GetByIdAsync(id);
            if (entity == null) return (false, "Không tìm thấy người thuê.");

            bool isDuplicate = await _store.ExistsDuplicateAsync(input.CCCD, input.SoDienThoai, input.Email, id);
            if (isDuplicate) return (false, "CCCD, Số điện thoại hoặc Email bị trùng với khách khác.");

            entity.HoVaTen = input.HoVaTen;
            entity.Email = input.Email;
            entity.SoDienThoai = input.SoDienThoai;
            entity.CCCD = input.CCCD;
            entity.NoiCapCCCD = input.NoiCapCCCD;
            entity.QueQuan = input.QueQuan;
            entity.GhiChu = input.GhiChu;
            entity.NgaySinh = input.NgaySinh?.ToUniversalTime();
            entity.NgayCapCCCD = input.NgayCapCCCD?.ToUniversalTime();
            entity.NgayCapNhat = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> DeleteAsync(int id)
        {
            var entity = await _store.GetByIdAsync(id);
            if (entity == null) return (false, "Không tìm thấy người thuê.");

            bool laDaiDienHopDongHoatDong = await _store.IsDaiDienHopDongHoatDongAsync(id);
            if (laDaiDienHopDongHoatDong)
            {
                return (false, "Không thể xóa do người thuê đang là đại diện ký hợp đồng còn hiệu lực.");
            }

            bool laThanhVienHopDongHoatDong = await _store.IsThanhVienHopDongHoatDongAsync(id);
            if (laThanhVienHopDongHoatDong)
            {
                return (false, "Không thể xóa do người thuê đang là thành viên ở chung trong một hợp đồng còn hiệu lực.");
            }

            entity.IsDeleted = true;
            entity.NgayCapNhat = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            return (true, string.Empty);
        }

        public async Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThue()
        {
            return await _store.GetDropdownListAsync();
        }

        public async Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThueChuaCoPhong()
        {
            return await _store.GetDropdownChuaCoPhongAsync();
        }

        public async Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThueCoHopDongAsync()
        {
            return await _store.GetDropdownCoHopDongAsync();
        }

        public async Task<IReadOnlyList<NguoiThueAutocompleteDto>> SearchAutocompleteAsync(string searchTerm)
        {
            return await _store.SearchAutocompleteAsync(searchTerm);
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> PhatSinhNgauNhienAsync()
        {
            var random = new Random();

            string[] hoList = { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng" };
            string[] demList = { "Văn", "Thị", "Hữu", "Minh", "Anh", "Đức", "Ngọc", "Tuấn", "Hoàng", "Quốc" };
            string[] tenList = { "Anh", "Dũng", "Hùng", "Cường", "Trang", "Vy", "Hải", "Tuấn", "Nam", "Lan", "Hương", "Long", "Minh", "Khánh", "Đức" };
            string[] tinhList = { "Hà Nội", "TP. Hồ Chí Minh", "Đà Nẵng", "Cần Thơ", "Hải Phòng", "Đồng Nai", "Bình Dương", "Long An", "Tiền Giang", "Lâm Đồng" };

            var addedTenants = new List<string>();

            for (int i = 0; i < 10; i++)
            {
                string hoTen = $"{hoList[random.Next(hoList.Length)]} {demList[random.Next(demList.Length)]} {tenList[random.Next(tenList.Length)]}";

                var nguoiThue = new NguoiThue
                {
                    HoVaTen = hoTen,
                    Email = $"tenant.{random.Next(1000, 9999)}@example.com",
                    SoDienThoai = $"09{random.Next(10000000, 99999999)}",
                    CCCD = $"{random.Next(100000000, 999999999)}{random.Next(100, 999)}",
                    QueQuan = tinhList[random.Next(tinhList.Length)],
                    NgayTao = DateTime.UtcNow,
                    IsDeleted = false
                };
                _store.Add(nguoiThue);
                addedTenants.Add(hoTen);
            }

            await _unitOfWork.SaveChangesAsync();
            return (true, $"Đã thêm 10 người thuê ngẫu nhiên thành công: {string.Join(", ", addedTenants)}");
        }
    }
}