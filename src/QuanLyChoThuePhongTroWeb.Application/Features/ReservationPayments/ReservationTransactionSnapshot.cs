using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

public sealed record ReservationTransactionSnapshot(
    int GiaoDichGiuChoId,
    int YeuCauThanhToanGiuChoId,
    string MaGiaoDich,
    decimal SoTienThucNhan,
    string? GhiChu,
    DateTime NgayThucNhanUtc
);
