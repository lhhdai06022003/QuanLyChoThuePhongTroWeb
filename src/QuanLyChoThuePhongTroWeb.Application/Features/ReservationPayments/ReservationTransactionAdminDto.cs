using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

// Dữ liệu dòng giao dịch giữ chỗ cho màn quản lý (spec giữ chỗ §15, A7)
public sealed record ReservationTransactionAdminDto(
    int Id,
    int PaymentRequestId,
    string MaGiaoDich,
    decimal SoTien,
    DateTime ReceivedAtUtc,
    string? NguoiXacNhan,
    string? GhiChu,
    bool LaSePay,
    bool LaVnPay,
    string? MaThamChieu,
    bool CanHoan
);
