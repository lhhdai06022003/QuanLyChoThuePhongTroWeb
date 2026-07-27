using QuanLyChoThuePhongTroWeb.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ThongBaos
{
    public interface IThongBaoService
    {
        Task<List<ThongBao>> LayDanhSachTheoNguoiDungAsync(int nguoiDungId);
        Task<bool> GuiChoNguoiDungAsync(int nguoiDungId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null);
        Task<bool> GuiChoKhachThueAsync(int nguoiThueId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null);
        Task<bool> GuiChoQuyenAsync(Role role, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null);
        Task<bool> DanhDauDaDocAsync(int thongBaoId);
        Task<bool> DanhDauTatCaDaDocAsync(int nguoiDungId);
        Task<int> LaySoLuongChuaDocAsync(int nguoiDungId);
        Task<QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableResponse<ThongBao>> LayDanhSachPhanTrangAsync(QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableRequest request, int nguoiDungId, bool? chuaDoc);
        Task<bool> XoaThongBaoAsync(int thongBaoId, int nguoiDungId);
        Task<bool> XoaTatCaThongBaoAsync(int nguoiDungId);
    }
}
