namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

// Dữ liệu dựng màn Giao dịch SePay (spec thanh toán trực tuyến §4.3). Ngày mặc định là giờ Việt Nam, dạng yyyy-MM-dd cho ô nhập ngày.
public sealed record GiaoDichSePayViewModel(
    bool DaCauHinhApiToken,
    string TuNgayMacDinh,
    string DenNgayMacDinh,
    int SoNgayToiDa,
    bool ChiDoiChieu);
