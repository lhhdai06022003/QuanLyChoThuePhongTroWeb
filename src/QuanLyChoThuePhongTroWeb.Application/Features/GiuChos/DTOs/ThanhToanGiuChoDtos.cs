namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

public sealed record HoldPaymentDto(int Id, int ReservationId, string TenPhong,
    decimal SoTien, DateTime HanThanhToanUtc, string MaYeuCau,
    string NoiDungChuyenKhoan, string TrangThai, int? MinhChungId, string? TrangThaiMinhChung);

public sealed record ProofReviewDto(int Id, int PaymentId, int ReservationId,
    string TenPhong, string TenChiNhanh, string KhachHang,
    decimal SoTienPhaiTra, decimal SoTienKhaiBao, DateTime NgayChuyenTienUtc,
    string? MaGiaoDichKhaiBao, string TrangThai);

public sealed record SubmitProofRequest(decimal SoTienKhaiBao, DateTimeOffset NgayChuyenTien,
    string? MaGiaoDichNganHang, string? GhiChu);

public sealed record PaymentActionResult(bool Success, string Message);
