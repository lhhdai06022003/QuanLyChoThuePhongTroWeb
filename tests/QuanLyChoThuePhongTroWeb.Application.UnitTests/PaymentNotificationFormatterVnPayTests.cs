using System;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Tin Telegram và email biên lai của kênh VNPay (spec VNPay §7.1, §7.2). Hàm thuần, không cần fake.
    public class PaymentNotificationFormatterVnPayTests
    {
        // 2026-10-03 02:30:00Z = 09:30:00 giờ Việt Nam.
        private static readonly DateTime PayTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private static IncomingBankTransfer Transfer(
            decimal amount = 2_000_000m, string? transactionNo = "14123456", string? bankCode = "NCB", string? txnRef = "HD7_1") => new(
                0, bankCode, PayTime, null, null, null, "Thanh toan hoa don HD-7", "in", amount, transactionNo, txnRef)
        {
            Kenh = OnlinePaymentChannel.VnPay,
            CardType = "ATM"
        };

        private static IncomingBankTransfer SePayTransfer(decimal amount = 2_000_000m) => new(
            92704, "NCB", PayTime, "0123456789", null, null, "TTPTHD7", "in", amount, "14123456", null);

        private static PaymentReceiptSnapshot Receipt(string maGiaoDich = "VNPAY-14123456") => new()
        {
            LichSuThanhToanId = 77,
            HoaDonId = 7,
            MaHoaDon = "HD-7",
            ChiNhanhId = 1,
            TenChiNhanh = "Chi nhánh 1",
            DiaChiChiNhanh = "1 Đường A",
            TenPhong = "P101",
            Thang = 9,
            Nam = 2026,
            TenKhachThue = "Nguyễn Văn A",
            EmailKhachThue = "khach@test.vn",
            TongTien = 2_000_000m,
            SoTienThanhToan = 2_000_000m,
            NgayThanhToanUtc = PayTime,
            MaGiaoDich = maGiaoDich,
            PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan,
            GhiChu = "[VNPAY] NCB · ATM · TXN 14123456 · REF HD7_1",
            DotThu = 1,
            LuyKeDaThanhToan = 2_000_000m
        };

        private static TransferOutcome Recorded(IncomingBankTransfer transfer) => new()
        {
            Kind = TransferOutcomeKind.Recorded,
            Transfer = transfer,
            Code = "TTPTHD7",
            HoaDonId = 7,
            MaHoaDon = "HD-7",
            LichSuThanhToanId = 77,
            TongTien = 2_000_000m,
            DaThu = 2_000_000m,
            ConLai = 0m
        };

        private static TransferOutcome Alert(TransferOutcomeKind kind, IncomingBankTransfer transfer, string? lyDo = null) => new()
        {
            Kind = kind,
            Transfer = transfer,
            Code = "TTPTHD7",
            HoaDonId = 7,
            MaHoaDon = "HD-7",
            LyDo = lyDo
        };

        private static string Lines(params string[] lines) => string.Join("\n", lines);

        // ---------- Tin thu tiền ----------

        [Fact]
        public void Recorded_DiffersFromTheSePayMessage_OnlyInTheChannelLine()
        {
            var vnPay = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(Transfer()), Receipt());
            var sePay = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(SePayTransfer()), Receipt("SEPAY-14123456"));

            Assert.Equal(
                sePay.Replace("Chuyển khoản VietQR (NCB)", "Thẻ ATM / Visa / MasterCard (VNPay, NCB)"),
                vnPay);
            Assert.Equal(Lines(
                "✅ THU TIỀN TRỌ THÀNH CÔNG",
                "\U0001F3E2 Chi nhánh: Chi nhánh 1",
                "\U0001F3E0 Phòng: P101",
                "\U0001F4C5 Kỳ: Tháng 09/2026",
                "\U0001F464 Khách thuê: Nguyễn Văn A",
                "\U0001F4B0 Vừa nhận: +2.000.000 VNĐ",
                "\U0001F4CA Tiến độ HD-7: 2.000.000/2.000.000 VNĐ",
                "\U0001F4CC Trạng thái: ĐÃ THANH TOÁN ĐỦ",
                "\U0001F4B3 Kênh: Thẻ ATM / Visa / MasterCard (VNPay, NCB)",
                "\U0001F9FE Mã GD: 14123456",
                "\U0001F552 03/10/2026 09:30:00"), vnPay);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Recorded_WithoutABankCode_DropsTheBankPart(string? bankCode)
        {
            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(Transfer(bankCode: bankCode)), Receipt());

            Assert.Contains("\U0001F4B3 Kênh: Thẻ ATM / Visa / MasterCard (VNPay)\n", text);
        }

        [Fact]
        public void Recorded_EscapesTheBankCodeAndTheTransactionNumber()
        {
            var text = PaymentNotificationFormatter.FormatRecordedTelegram(
                Recorded(Transfer(bankCode: "N<b>&CB", transactionNo: "14<i>1")), Receipt());

            Assert.Contains("(VNPay, N&lt;b&gt;&amp;CB)", text);
            Assert.Contains("Mã GD: 14&lt;i&gt;1", text);
            Assert.DoesNotContain("<b>", text);
            Assert.DoesNotContain("<i>", text);
        }

        // ---------- Cảnh báo cần hoàn tiền ----------

        [Fact]
        public void Alert_AlreadyPaid_UsesTheRefundTemplate()
        {
            var text = PaymentNotificationFormatter.FormatAlertTelegram(Alert(TransferOutcomeKind.AlreadyPaid, Transfer()), null);

            Assert.Equal(Lines(
                "\U0001F514 THANH TOÁN VNPAY CẦN HOÀN TIỀN",
                "Lý do: Hóa đơn HD-7 đã trả đủ",
                "Số tiền: +2.000.000 VNĐ",
                "Mã GD VNPay: 14123456 · Mã đơn: HD7_1",
                "Thời gian: 03/10/2026 09:30:00",
                "Gợi ý: Hoàn tiền cho khách trên trang merchant VNPay"), text);
        }

        [Fact]
        public void Alert_InvoiceNotPayable_NamesTheReason()
        {
            var text = PaymentNotificationFormatter.FormatAlertTelegram(
                Alert(TransferOutcomeKind.InvoiceNotPayable, Transfer(), "Hóa đơn chưa công bố"), null);

            Assert.Equal(Lines(
                "\U0001F514 THANH TOÁN VNPAY CẦN HOÀN TIỀN",
                "Lý do: Hóa đơn HD-7 không nhận tiền được: Hóa đơn chưa công bố",
                "Số tiền: +2.000.000 VNĐ",
                "Mã GD VNPay: 14123456 · Mã đơn: HD7_1",
                "Thời gian: 03/10/2026 09:30:00",
                "Gợi ý: Hoàn tiền cho khách trên trang merchant VNPay"), text);
        }

        [Fact]
        public void Alert_InvoiceNotPayable_WithAReplacement_NamesTheReplacementInvoice()
        {
            var context = new TransferAlertContext
            {
                HoaDonId = 7, MaHoaDon = "HD-7", ChiNhanhId = 1, HoaDonThayTheId = 8, MaHoaDonThayThe = "HD-7-R1"
            };

            var text = PaymentNotificationFormatter.FormatAlertTelegram(
                Alert(TransferOutcomeKind.InvoiceNotPayable, Transfer(), "Hóa đơn đã hủy"), context);

            Assert.Contains("Lý do: Hóa đơn HD-7 không nhận tiền được: Hóa đơn đã hủy; hóa đơn thay thế: HD-7-R1 (#HD8)\n", text);
        }

        [Fact]
        public void Alert_EscapesTheTransactionNumberAndTheOrderReference()
        {
            var text = PaymentNotificationFormatter.FormatAlertTelegram(
                Alert(TransferOutcomeKind.AlreadyPaid, Transfer(transactionNo: "1<b>&2", txnRef: "HD7_<i>")), null);

            Assert.Contains("Mã GD VNPay: 1&lt;b&gt;&amp;2 · Mã đơn: HD7_&lt;i&gt;\n", text);
            Assert.DoesNotContain("<b>", text);
            Assert.DoesNotContain("<i>", text);
        }

        [Fact]
        public void Alert_ForSePay_IsUnchanged()
        {
            var text = PaymentNotificationFormatter.FormatAlertTelegram(Alert(TransferOutcomeKind.AlreadyPaid, SePayTransfer(500_000m)), null);

            Assert.Contains("CHUYỂN KHOẢN CẦN XỬ LÝ TAY", text);
            Assert.Contains("Lý do: Khách chuyển thêm 500.000đ cho hóa đơn HD-7 đã trả đủ\n", text);
        }

        // ---------- Email biên lai ----------

        [Theory]
        [InlineData("vnpay-1")]
        [InlineData("VNPAY-14123456")]
        public void ReceiptEmail_ForAVnPayRow_UsesTheVnPayMethodLabel(string maGiaoDich)
        {
            var data = PaymentNotificationFormatter.BuildReceiptEmail(Receipt(maGiaoDich), "14123456", null);

            Assert.Equal("Thẻ ATM / Visa / MasterCard (VNPay)", data.PhuongThuc);
            Assert.Equal("14123456", data.MaThamChieu);
        }

        [Fact]
        public void ReceiptEmail_ForASePayRow_KeepsItsMethodLabel()
        {
            var data = PaymentNotificationFormatter.BuildReceiptEmail(Receipt("SEPAY-FT1"), "FT1", null);

            Assert.Equal("Chuyển khoản 24/7 (VietQR, tự động)", data.PhuongThuc);
        }
    }
}
