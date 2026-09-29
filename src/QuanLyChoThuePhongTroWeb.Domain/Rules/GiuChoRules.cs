using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Rules;

public static class GiuChoRules
{
    public static decimal TinhTienGiuCho(decimal giaThueThang)
    {
        if (giaThueThang <= 0) throw new ArgumentOutOfRangeException(nameof(giaThueThang));
        return decimal.Round(giaThueThang / 2m, 0, MidpointRounding.AwayFromZero);
    }

    public static decimal CanTruVaoHoaDon(HoaDon invoice, int applicationId, decimal availableCredit)
    {
        if (invoice.CanTruGiuCho is not null)
            throw new InvalidOperationException("Hóa đơn đã cấn trừ cọc giữ chỗ.");
        if (invoice.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap ||
            invoice.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
            throw new InvalidOperationException("Chỉ cấn trừ khi tạo hóa đơn nháp.");
        if (availableCredit <= 0 || invoice.TongTien <= 0) return 0;
        var amount = Math.Min(availableCredit, invoice.TongTien);
        invoice.CanTruGiuCho = new CanTruTienGiuChoHoaDon
        { ApDungTienGiuChoVaoTienCocId = applicationId, SoTienCanTru = amount };
        invoice.ChiTietHoaDonDichVus.Add(new ChiTietHoaDon
        {
            TenDichVu = "Cấn trừ tiền cọc giữ chỗ", DonGia = -amount,
            SoLuong = 1, TongTien = -amount
        });
        invoice.TongTien -= amount;
        if (invoice.TongTien == 0) invoice.TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan;
        return amount;
    }
}
