using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services
{
    public interface IThongBaoService
    {
        Task<List<ThongBaoRes>> LayDanhSachTheoNguoiDungAsync(int nguoiDungId, CancellationToken cancellationToken = default);
        Task<ServiceResult> GuiChoNguoiDungAsync(int nguoiDungId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default);
        Task<ServiceResult> GuiChoKhachThueAsync(int nguoiThueId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default);
        Task<ServiceResult> GuiChoQuyenAsync(AppRole role, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default);
        Task<ServiceResult> DanhDauDaDocAsync(int thongBaoId, CancellationToken cancellationToken = default);
        Task<ServiceResult> DanhDauTatCaDaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default);
        Task<int> LaySoLuongChuaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default);
        Task<DataTableResponse<ThongBaoRes>> LayDanhSachPhanTrangAsync(DataTableRequest request, int nguoiDungId, bool? chuaDoc, CancellationToken cancellationToken = default);
        Task<ServiceResult> XoaThongBaoAsync(int thongBaoId, int nguoiDungId, CancellationToken cancellationToken = default);
        Task<ServiceResult> XoaTatCaThongBaoAsync(int nguoiDungId, CancellationToken cancellationToken = default);
    }
}
