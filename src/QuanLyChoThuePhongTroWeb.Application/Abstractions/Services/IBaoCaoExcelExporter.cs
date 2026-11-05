using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public interface IBaoCaoExcelExporter
    {
        byte[] XuatDoanhThu(BaoCaoDoanhThuDto data);
        byte[] XuatCongNo(BaoCaoCongNoDto data);
        byte[] XuatCongSuatPhong(BaoCaoCongSuatDto data);
        byte[] XuatHopDong(BaoCaoHopDongDto data);
        byte[] XuatDienNuoc(BaoCaoDienNuocDto data);
    }
}
