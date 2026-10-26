using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Store giả cho ghi tiền giữ chỗ tự động: cấu hình kết quả trả về, ghi lại tham số mỗi lần gọi.
    public class FakeReservationAutoPaymentStore : IReservationAutoPaymentStore
    {
        public bool Exists { get; set; } = true;
        public bool ThrowOnRecord { get; set; }
        public SystemReservationRecordResult NextResult { get; set; } = Recorded();

        public List<string> ExistsQueries { get; } = new();
        public List<RecordCall> RecordCalls { get; } = new();

        public sealed record RecordCall(string MaYeuCau, string MaGiaoDich, decimal SoTien, DateTime NgayThucNhanUtc, string LyDo, DateTime NowUtc);

        public Task<bool> PaymentRequestExistsAsync(string maYeuCau, CancellationToken cancellationToken = default)
        {
            ExistsQueries.Add(maYeuCau);
            return Task.FromResult(Exists);
        }

        public Task<SystemReservationRecordResult> RecordSystemTransferAsync(string maYeuCau, string maGiaoDich,
            decimal soTienThucNhan, DateTime ngayThucNhanUtc, string lyDoLichSu, DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            RecordCalls.Add(new RecordCall(maYeuCau, maGiaoDich, soTienThucNhan, ngayThucNhanUtc, lyDoLichSu, nowUtc));
            if (ThrowOnRecord)
            {
                throw new InvalidOperationException("DB down");
            }

            return Task.FromResult(NextResult);
        }

        // Kết quả Recorded mặc định: giữ chỗ #5 của phòng P101, giữ chỗ 1.000.000đ, đã nhận đủ trong lần này.
        public static SystemReservationRecordResult Recorded(
            decimal soTienGiuCho = 1_000_000m, decimal daNhanTruoc = 0m, decimal daNhanSau = 1_000_000m,
            bool canHoanTien = false, bool vuaCoHieuLuc = true, string trangThai = "DangGiuCho",
            bool conMinhChung = false, string? email = "khach@example.com") =>
            new(SystemReservationRecordStatus.Recorded)
            {
                YeuCauGiuChoId = 5,
                YeuCauThanhToanGiuChoId = 9,
                GiaoDichGiuChoId = 33,
                MaYeuCau = "TTPTGC5",
                PhongTroId = 12,
                ChiNhanhId = 1,
                TenChiNhanh = "Chi nhánh A",
                SoPhong = "P101",
                GuestUserId = 700,
                TenKhach = "Nguyễn Văn A",
                EmailKhach = email,
                SoTienGiuCho = soTienGiuCho,
                DaNhanTruoc = daNhanTruoc,
                DaNhanSau = daNhanSau,
                CanHoanTien = canHoanTien,
                VuaCoHieuLuc = vuaCoHieuLuc,
                TrangThaiGiuCho = trangThai,
                HanThanhToanUtc = new DateTime(2026, 10, 4, 3, 0, 0, DateTimeKind.Utc),
                HanKyHopDongUtc = new DateTime(2026, 10, 10, 3, 0, 0, DateTimeKind.Utc),
                ConMinhChungChoXacNhan = conMinhChung
            };
    }
}
