namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

public sealed record ReservationContractRequest(DateTimeOffset NgayBatDau,
    DateTimeOffset? NgayKetThuc, IReadOnlyList<int> DieuKhoanMauIds, bool XacNhanKyQuaHan = false);
public sealed record ContractCandidateDto(int ReservationId, string TenPhong,
    string TenChiNhanh, string KhachHang, string? Email, string? CCCD,
    decimal GiaThueDaChot, decimal TienCoc, DateTime? HanKyHopDongUtc,
    IReadOnlyList<ContractTermDto> DieuKhoans);
public sealed record ContractTermDto(int Id, string TieuDe, string NoiDung);
public sealed record ReservationContractResult(bool Success, int? HopDongId, string Message);
