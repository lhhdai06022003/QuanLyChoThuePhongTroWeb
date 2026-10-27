using System;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

public sealed record ReservationPaymentSnapshot(
    int YeuCauGiuChoId,
    int? NguoiDungId,
    string MaYeuCau,
    decimal SoTienGiuCho,
    decimal DaNhan,
    DateTime? HanThanhToanUtc,
    TrangThaiYeuCauGiuCho TrangThaiGiuCho,
    string SoPhong
);
