using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Services
{
    public class LichSuThanhToanService : ILichSuThanhToanService
    {
        private readonly ILichSuThanhToanStore _store;

        public LichSuThanhToanService(ILichSuThanhToanStore store)
        {
            _store = store;
        }

        public async Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
            DataTableRequest request, int chiNhanhId, int phuongThuc, DateTime? tuNgay, DateTime? denNgay, bool chiCoTienThua = false)
        {
            var page = await _store.GetDanhSachThanhToanAsync(request, chiNhanhId, phuongThuc, tuNgay, denNgay, chiCoTienThua);
            foreach (var row in page.data)
            {
                var info = PaymentNoteTags.Parse(row.GhiChu);
                row.TienThua = info.TienThua;
                row.ChenhLech = info.ChenhLech;
                row.NhanGhiChu = PaymentNoteTags.Labels(info).ToList();
                row.NoiDungGhiChu = info.NoiDung;
            }

            return page;
        }

        public async Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int chiNhanhId, DateTime? tuNgay, DateTime? denNgay)
        {
            return await _store.GetThongKeThanhToanAsync(chiNhanhId, tuNgay, denNgay);
        }

        public async Task<IReadOnlyList<LichSuThanhToanKhachThueDto>> GetLichSuByNguoiThueIdAsync(int nguoiThueId)
        {
            var list = await _store.GetLichSuByNguoiThueIdAsync(nguoiThueId);
            var result = new List<LichSuThanhToanKhachThueDto>();
            foreach (var l in list)
            {
                result.Add(new LichSuThanhToanKhachThueDto
                {
                    LichSuThanhToanId = l.LichSuThanhToanId,
                    MaGiaoDich = l.MaGiaoDich,
                    MaHoaDon = l.HoaDon?.MaHoaDon ?? string.Empty,
                    SoPhong = l.HoaDon?.HopDong?.PhongTro?.SoPhong ?? string.Empty,
                    SoTienThanhToan = l.SoTienThanhToan,
                    PhuongThucThanhToan = (int)l.PhuongThucThanhToan,
                    NgayThanhToan = l.NgayThanhToan,
                    GhiChu = PaymentNoteTags.ToDisplayText(l.GhiChu)
                });
            }
            return result;
        }
    }
}
