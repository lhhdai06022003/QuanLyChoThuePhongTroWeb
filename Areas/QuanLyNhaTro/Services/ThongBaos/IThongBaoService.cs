using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ThongBaos
{
    public interface IThongBaoService
    {
        Task<List<ThongBao>> LayDanhSachTheoNguoiDungAsync(int nguoiDungId, CancellationToken cancellationToken = default);
        Task<ServiceResult> GuiChoNguoiDungAsync(int nguoiDungId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default);
        Task<ServiceResult> GuiChoKhachThueAsync(int nguoiThueId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default);
        Task<ServiceResult> GuiChoQuyenAsync(Role role, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default);
        Task<ServiceResult> DanhDauDaDocAsync(int thongBaoId, CancellationToken cancellationToken = default);
        Task<ServiceResult> DanhDauTatCaDaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default);
        Task<int> LaySoLuongChuaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default);
        Task<QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableResponse<ThongBao>> LayDanhSachPhanTrangAsync(QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableRequest request, int nguoiDungId, bool? chuaDoc, CancellationToken cancellationToken = default);
        Task<ServiceResult> XoaThongBaoAsync(int thongBaoId, int nguoiDungId, CancellationToken cancellationToken = default);
        Task<ServiceResult> XoaTatCaThongBaoAsync(int nguoiDungId, CancellationToken cancellationToken = default);
    }
}
