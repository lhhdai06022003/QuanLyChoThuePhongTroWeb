using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Tin Telegram và dữ liệu email biên lai (spec thanh toán trực tuyến §6.2, §6.3). Hàm thuần, không cần fake.
    public class PaymentNotificationFormatterTests
    {
        // 2026-10-03 02:30:00Z = 09:30:00 giờ Việt Nam.
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private static IncomingBankTransfer Transfer(
            decimal amount = 2_000_000m, string? reference = "FT26ABC", string? content = "TTPTHD7", string? description = null,
            string? gateway = "MBBank", string? account = "0123456789") => new(
                92704, gateway, TransferTime, account, null, null, content, "in", amount, reference, description);

        private static PaymentReceiptSnapshot Receipt(Action<ReceiptBuilder>? configure = null)
        {
            var b = new ReceiptBuilder();
            configure?.Invoke(b);
            return new PaymentReceiptSnapshot
            {
                LichSuThanhToanId = 77,
                HoaDonId = 7,
                MaHoaDon = "HD-7",
                ChiNhanhId = 1,
                TenChiNhanh = b.TenChiNhanh,
                DiaChiChiNhanh = "1 Đường A",
                TenPhong = b.TenPhong,
                Thang = b.Thang,
                Nam = 2026,
                TenKhachThue = b.TenKhachThue,
                EmailKhachThue = "khach@test.vn",
                TongTien = b.TongTien,
                SoTienThanhToan = b.SoTienThanhToan,
                NgayThanhToanUtc = TransferTime,
                MaGiaoDich = b.MaGiaoDich,
                PhuongThucThanhToan = b.PhuongThuc,
                GhiChu = b.GhiChu,
                DotThu = b.DotThu,
                LuyKeDaThanhToan = b.LuyKe
            };
        }

        private sealed class ReceiptBuilder
        {
            public string TenChiNhanh { get; set; } = "Chi nhánh 1";
            public string TenPhong { get; set; } = "P101";
            public string TenKhachThue { get; set; } = "Nguyễn Văn A";
            public int Thang { get; set; } = 9;
            public decimal TongTien { get; set; } = 2_000_000m;
            public decimal SoTienThanhToan { get; set; } = 2_000_000m;
            public string MaGiaoDich { get; set; } = "SEPAY-FT26ABC";
            public PhuongThucThanhToan PhuongThuc { get; set; } = PhuongThucThanhToan.ChuyenKhoan;
            public string GhiChu { get; set; } = "[SEPAY] MBBank · REF FT26ABC · ND: TTPTHD7";
            public int DotThu { get; set; } = 1;
            public decimal LuyKe { get; set; } = 2_000_000m;
        }

        private static TransferOutcome Recorded(IncomingBankTransfer? transfer = null, decimal daThu = 2_000_000m, decimal tienThua = 0m) => new()
        {
            Kind = TransferOutcomeKind.Recorded,
            Transfer = transfer ?? Transfer(),
            Code = "TTPTHD7",
            HoaDonId = 7,
            MaHoaDon = "HD-7",
            LichSuThanhToanId = 77,
            TongTien = 2_000_000m,
            DaThu = daThu,
            ConLai = 2_000_000m - daThu,
            TienThua = tienThua
        };

        private static TransferOutcome Alert(TransferOutcomeKind kind, IncomingBankTransfer? transfer = null, string? code = "TTPTHD7",
            int? hoaDonId = 7, string? maHoaDon = "HD-7", string? lyDo = null) => new()
        {
            Kind = kind,
            Transfer = transfer ?? Transfer(),
            Code = code,
            HoaDonId = hoaDonId,
            MaHoaDon = maHoaDon,
            LyDo = lyDo
        };

        private static string Lines(params string[] lines) => string.Join("\n", lines);

        // ---------- EscapeHtml ----------

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("Nguyễn Văn A", "Nguyễn Văn A")]
        [InlineData("<b>&", "&lt;b&gt;&amp;")]
        [InlineData("a & b < c > d", "a &amp; b &lt; c &gt; d")]
        [InlineData("&lt;", "&amp;lt;")]
        [InlineData("\"quote\" 'single'", "\"quote\" 'single'")]
        public void EscapeHtml_ReplacesOnlyAmpersandAndAngleBrackets(string? input, string expected)
        {
            Assert.Equal(expected, PaymentNotificationFormatter.EscapeHtml(input));
        }

        // ---------- Tin thu tiền ----------

        [Fact]
        public void Recorded_PaidInFull_UsesTheFullTemplate()
        {
            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(), Receipt());

            Assert.Equal(Lines(
                "✅ THU TIỀN TRỌ THÀNH CÔNG",
                "\U0001F3E2 Chi nhánh: Chi nhánh 1",
                "\U0001F3E0 Phòng: P101",
                "\U0001F4C5 Kỳ: Tháng 09/2026",
                "\U0001F464 Khách thuê: Nguyễn Văn A",
                "\U0001F4B0 Vừa nhận: +2.000.000 VNĐ",
                "\U0001F4CA Tiến độ HD-7: 2.000.000/2.000.000 VNĐ",
                "\U0001F4CC Trạng thái: ĐÃ THANH TOÁN ĐỦ",
                "\U0001F4B3 Kênh: Chuyển khoản VietQR (MBBank)",
                "\U0001F9FE Mã GD: FT26ABC",
                "\U0001F552 03/10/2026 09:30:00"), text);
        }

        [Fact]
        public void Recorded_Partial_UsesThePartialTemplate()
        {
            var transfer = Transfer(amount: 1_500_000m);

            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(transfer, daThu: 1_500_000m), Receipt());

            Assert.Equal(Lines(
                "⚠️ THU TIỀN TRỌ (ĐÓNG MỘT PHẦN)",
                "\U0001F3E2 Chi nhánh: Chi nhánh 1",
                "\U0001F3E0 Phòng: P101",
                "\U0001F4C5 Kỳ: Tháng 09/2026",
                "\U0001F464 Khách thuê: Nguyễn Văn A",
                "\U0001F4B0 Vừa nhận: +1.500.000 VNĐ",
                "\U0001F4CA Tiến độ HD-7: 1.500.000/2.000.000 VNĐ",
                "❗ Còn thiếu: 500.000 VNĐ",
                "\U0001F4B3 Kênh: Chuyển khoản VietQR (MBBank)",
                "\U0001F9FE Mã GD: FT26ABC",
                "\U0001F552 03/10/2026 09:30:00"), text);
        }

        [Fact]
        public void Recorded_Overpaid_UsesTheFullTemplate_PlusTheSurplusLine_AndShowsTheAmountReceived()
        {
            var transfer = Transfer(amount: 2_500_000m);

            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(transfer, tienThua: 500_000m), Receipt());

            Assert.Equal(Lines(
                "✅ THU TIỀN TRỌ THÀNH CÔNG",
                "\U0001F3E2 Chi nhánh: Chi nhánh 1",
                "\U0001F3E0 Phòng: P101",
                "\U0001F4C5 Kỳ: Tháng 09/2026",
                "\U0001F464 Khách thuê: Nguyễn Văn A",
                "\U0001F4B0 Vừa nhận: +2.500.000 VNĐ",
                "\U0001F4CA Tiến độ HD-7: 2.000.000/2.000.000 VNĐ",
                "\U0001F4CC Trạng thái: ĐÃ THANH TOÁN ĐỦ",
                "\U0001F4B3 Kênh: Chuyển khoản VietQR (MBBank)",
                "\U0001F9FE Mã GD: FT26ABC",
                "\U0001F552 03/10/2026 09:30:00",
                "\U0001F4A1 Khách đóng dư +500.000 VNĐ (đã ghi chú, cần xử lý ngoài hệ thống)"), text);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Recorded_WithoutAGateway_DropsTheBracketedPart(string? gateway)
        {
            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(Transfer(gateway: gateway)), Receipt());

            Assert.Contains("\U0001F4B3 Kênh: Chuyển khoản VietQR\n", text);
            Assert.DoesNotContain("(", text);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Recorded_WithoutAReference_UsesTheSePayId(string? reference)
        {
            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(Transfer(reference: reference)), Receipt());

            Assert.Contains("\U0001F9FE Mã GD: ID 92704\n", text);
        }

        [Fact]
        public void Recorded_EscapesEveryDynamicString()
        {
            var transfer = Transfer(reference: "FT<b>&1", gateway: "MB<i>Bank");
            var receipt = Receipt(b =>
            {
                b.TenChiNhanh = "CN <1> & Co";
                b.TenPhong = "P<2>";
                b.TenKhachThue = "<b>Tèo</b> & Tí";
            });

            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(transfer), receipt);

            Assert.Contains("Chi nhánh: CN &lt;1&gt; &amp; Co", text);
            Assert.Contains("Phòng: P&lt;2&gt;", text);
            Assert.Contains("Khách thuê: &lt;b&gt;Tèo&lt;/b&gt; &amp; Tí", text);
            Assert.Contains("(MB&lt;i&gt;Bank)", text);
            Assert.Contains("Mã GD: FT&lt;b&gt;&amp;1", text);
            Assert.DoesNotContain("<b>", text);
            Assert.DoesNotContain("<i>", text);
        }

        [Fact]
        public void Recorded_FormatsTimeInVietnamTime_AndMoneyWithDots()
        {
            var transfer = Transfer(amount: 1_900_000m);

            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(transfer, daThu: 1_900_000m), Receipt());

            Assert.Contains("Vừa nhận: +1.900.000 VNĐ", text);
            Assert.EndsWith("\U0001F552 03/10/2026 09:30:00", text);
        }

        [Fact]
        public void Recorded_MonthIsZeroPadded()
        {
            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(), Receipt(b => b.Thang = 3));

            Assert.Contains("Kỳ: Tháng 03/2026", text);
        }

        // ---------- Cảnh báo ----------

        [Fact]
        public void Alert_WrongAccount_UsesTheAlertTemplate()
        {
            var outcome = Alert(TransferOutcomeKind.WrongAccount, Transfer(content: "Nguyen Van A chuyen tien", account: "9999999999"), code: null, hoaDonId: null, maHoaDon: null);

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Equal(Lines(
                "\U0001F514 CHUYỂN KHOẢN CẦN XỬ LÝ TAY",
                "Lý do: Tiền vào tài khoản 9999999999 khác tài khoản nhận tiền của hệ thống",
                "Số tiền: +2.000.000 VNĐ",
                "Nội dung: Nguyen Van A chuyen tien",
                "Mã GD: FT26ABC",
                "Thời gian: 03/10/2026 09:30:00",
                "Gợi ý: Vào Hóa đơn → Thu thủ công"), text);
        }

        [Fact]
        public void Alert_Unmatched_WithoutACode_SaysThereIsNoCode()
        {
            var outcome = Alert(TransferOutcomeKind.Unmatched, Transfer(content: "chuyen tien tro"), code: null, hoaDonId: null, maHoaDon: null);

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains("Lý do: Nội dung chuyển khoản không có mã TTPTHD\n", text);
            Assert.Contains("Nội dung: chuyen tien tro\n", text);
        }

        [Fact]
        public void Alert_Unmatched_WithAForeignCode_NamesTheCode()
        {
            var outcome = Alert(TransferOutcomeKind.Unmatched, code: "DH42", hoaDonId: null, maHoaDon: null);

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains("Lý do: Không nhận ra mã thanh toán hợp lệ (DH42)\n", text);
        }

        [Fact]
        public void Alert_InvoiceNotPayable_NamesTheInvoiceAndTheReason()
        {
            var outcome = Alert(TransferOutcomeKind.InvoiceNotPayable, lyDo: "Hóa đơn chưa công bố");

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains("Lý do: Hóa đơn HD-7 không nhận tiền được: Hóa đơn chưa công bố\n", text);
        }

        [Fact]
        public void Alert_InvoiceNotPayable_ForAMissingInvoice_UsesTheInvoiceId()
        {
            var outcome = Alert(TransferOutcomeKind.InvoiceNotPayable, hoaDonId: 99, maHoaDon: null, lyDo: "Không tìm thấy hóa đơn");

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains("Lý do: Hóa đơn #HD99 không nhận tiền được: Không tìm thấy hóa đơn\n", text);
        }

        [Fact]
        public void Alert_InvoiceNotPayable_NamesTheReplacementInvoice_WhenThereIsOne()
        {
            var outcome = Alert(TransferOutcomeKind.InvoiceNotPayable, lyDo: "Hóa đơn đã hủy");
            var context = new TransferAlertContext
            {
                HoaDonId = 7, MaHoaDon = "HD-7", ChiNhanhId = 1, HoaDonThayTheId = 8, MaHoaDonThayThe = "HD-7-R1"
            };

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, context);

            Assert.Contains("Lý do: Hóa đơn HD-7 không nhận tiền được: Hóa đơn đã hủy; hóa đơn thay thế: HD-7-R1 (#HD8)\n", text);
        }

        [Fact]
        public void Alert_InvoiceNotPayable_WithoutAReplacement_HasNoReplacementPart()
        {
            var outcome = Alert(TransferOutcomeKind.InvoiceNotPayable, lyDo: "Hóa đơn đã hủy");
            var context = new TransferAlertContext { HoaDonId = 7, MaHoaDon = "HD-7", ChiNhanhId = 1 };

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, context);

            Assert.DoesNotContain("thay thế", text);
        }

        [Fact]
        public void Alert_AlreadyPaid_SaysTheCustomerPaidAgain()
        {
            var outcome = Alert(TransferOutcomeKind.AlreadyPaid, Transfer(amount: 500_000m));

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains("Lý do: Khách chuyển thêm 500.000đ cho hóa đơn HD-7 đã trả đủ\n", text);
            Assert.Contains("Số tiền: +500.000 VNĐ\n", text);
        }

        [Fact]
        public void Alert_EscapesEveryDynamicString()
        {
            var outcome = Alert(
                TransferOutcomeKind.InvoiceNotPayable,
                Transfer(content: "<script>&", reference: "FT<1>"),
                lyDo: "lý do <x>");

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains("Nội dung: &lt;script&gt;&amp;\n", text);
            Assert.Contains("Mã GD: FT&lt;1&gt;\n", text);
            Assert.Contains("không nhận tiền được: lý do &lt;x&gt;\n", text);
            Assert.DoesNotContain("<script>", text);
        }

        [Fact]
        public void Alert_EscapesTheCodeTheAccountNumberAndTheInvoiceCode()
        {
            var unmatched = Alert(TransferOutcomeKind.Unmatched, code: "<X>&1", hoaDonId: null, maHoaDon: null);
            var wrongAccount = Alert(TransferOutcomeKind.WrongAccount, Transfer(account: "<9>&9"), code: null, hoaDonId: null, maHoaDon: null);
            var alreadyPaid = Alert(TransferOutcomeKind.AlreadyPaid, maHoaDon: "HD<7>&");

            Assert.Contains("Không nhận ra mã thanh toán hợp lệ (&lt;X&gt;&amp;1)", PaymentNotificationFormatter.FormatAlertTelegram(unmatched, null));
            Assert.Contains("Tiền vào tài khoản &lt;9&gt;&amp;9 khác", PaymentNotificationFormatter.FormatAlertTelegram(wrongAccount, null));
            Assert.Contains("hóa đơn HD&lt;7&gt;&amp; đã trả đủ", PaymentNotificationFormatter.FormatAlertTelegram(alreadyPaid, null));
        }

        [Fact]
        public void Alert_EscapesTheReplacementInvoiceCode()
        {
            var outcome = Alert(TransferOutcomeKind.InvoiceNotPayable, lyDo: "Hóa đơn đã hủy");
            var context = new TransferAlertContext
            {
                HoaDonId = 7, MaHoaDon = "HD-7", ChiNhanhId = 1, HoaDonThayTheId = 8, MaHoaDonThayThe = "HD<7>-R1"
            };

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, context);

            Assert.Contains("hóa đơn thay thế: HD&lt;7&gt;-R1 (#HD8)", text);
            Assert.DoesNotContain("<7>", text);
        }

        [Fact]
        public void Recorded_EscapesTheInvoiceCode_InTheProgressLine()
        {
            var receipt = Receipt();
            var withMarkup = new PaymentReceiptSnapshot
            {
                LichSuThanhToanId = receipt.LichSuThanhToanId, HoaDonId = receipt.HoaDonId, MaHoaDon = "HD<7>&", ChiNhanhId = receipt.ChiNhanhId,
                TenChiNhanh = receipt.TenChiNhanh, DiaChiChiNhanh = receipt.DiaChiChiNhanh, TenPhong = receipt.TenPhong, Thang = receipt.Thang,
                Nam = receipt.Nam, TenKhachThue = receipt.TenKhachThue, TongTien = receipt.TongTien, SoTienThanhToan = receipt.SoTienThanhToan,
                NgayThanhToanUtc = receipt.NgayThanhToanUtc, MaGiaoDich = receipt.MaGiaoDich, PhuongThucThanhToan = receipt.PhuongThucThanhToan,
                GhiChu = receipt.GhiChu, DotThu = receipt.DotThu, LuyKeDaThanhToan = receipt.LuyKeDaThanhToan
            };

            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(), withMarkup);

            Assert.Contains("Tiến độ HD&lt;7&gt;&amp;: 2.000.000/2.000.000 VNĐ", text);
        }

        [Fact]
        public void Recorded_ZeroOwedWithoutSurplus_IsTheFullTemplate_AndNeverShowsAMissingLine()
        {
            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(daThu: 2_000_000m), Receipt());

            Assert.DoesNotContain("Còn thiếu", text);
            Assert.DoesNotContain("Khách đóng dư", text);
            Assert.Contains("ĐÃ THANH TOÁN ĐỦ", text);
        }

        [Fact]
        public void Recorded_SmallestPartialPayment_ShowsWhatIsMissing()
        {
            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(Transfer(amount: 1_000m), daThu: 1_000m), Receipt());

            Assert.Contains("Vừa nhận: +1.000 VNĐ", text);
            Assert.Contains("Còn thiếu: 1.999.000 VNĐ", text);
            Assert.DoesNotContain("ĐÃ THANH TOÁN ĐỦ", text);
        }

        [Fact]
        public void Recorded_TheMessageNeverExceedsTelegramsLimit_EvenWithLongNames()
        {
            var receipt = Receipt(b =>
            {
                b.TenChiNhanh = new string('C', 300);
                b.TenPhong = new string('P', 100);
                b.TenKhachThue = new string('K', 300);
            });

            var text = PaymentNotificationFormatter.FormatRecordedTelegram(Recorded(), receipt);

            Assert.True(text.Length < 4096, $"Tin dài {text.Length} ký tự, vượt giới hạn 4096 của Telegram.");
        }

        [Fact]
        public void Alert_TheMessageNeverExceedsTelegramsLimit_EvenWithLongInput()
        {
            var outcome = Alert(TransferOutcomeKind.Unmatched, Transfer(content: new string('<', 5000)), code: null);

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            // 200 ký tự '<' thành 200 x "&lt;" = 800 ký tự sau khi escape.
            Assert.True(text.Length < 4096, $"Tin dài {text.Length} ký tự, vượt giới hạn 4096 của Telegram.");
        }

        [Fact]
        public void Alert_UsesTheDescription_WhenTheContentIsBlank()
        {
            var outcome = Alert(TransferOutcomeKind.Unmatched, Transfer(content: "  ", description: "mo ta thay the"), code: null);

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains("Nội dung: mo ta thay the\n", text);
        }

        [Fact]
        public void Alert_WithoutAReference_UsesTheSePayId()
        {
            var outcome = Alert(TransferOutcomeKind.Unmatched, Transfer(reference: null), code: null);

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains("Mã GD: ID 92704\n", text);
        }

        [Fact]
        public void Alert_LongContent_IsCutAt200Characters()
        {
            var outcome = Alert(TransferOutcomeKind.Unmatched, Transfer(content: new string('x', 300)), code: null);

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains($"Nội dung: {new string('x', 200)}\n", text);
            Assert.DoesNotContain(new string('x', 201), text);
        }

        [Fact]
        public void Alert_LongContent_IsNotCutInTheMiddleOfASurrogatePair()
        {
            var content = new string('a', 199) + "\U0001F600" + "tail";
            var outcome = Alert(TransferOutcomeKind.Unmatched, Transfer(content: content), code: null);

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains($"Nội dung: {new string('a', 199)}\n", text);
            Assert.DoesNotContain("\U0001F600", text);
        }

        [Fact]
        public void Alert_ContentShorterThanTheLimit_IsKeptWhole()
        {
            var outcome = Alert(TransferOutcomeKind.Unmatched, Transfer(content: new string('y', 200)), code: null);

            var text = PaymentNotificationFormatter.FormatAlertTelegram(outcome, null);

            Assert.Contains($"Nội dung: {new string('y', 200)}\n", text);
        }

        // ---------- Dữ liệu email biên lai ----------

        [Fact]
        public void ReceiptEmail_CopiesTheReceiptFields()
        {
            var data = PaymentNotificationFormatter.BuildReceiptEmail(Receipt(), "FT26ABC", "https://tro.test/KhachThue/HoaDon?hoaDonId=7");

            Assert.Equal(7, data.HoaDonId);
            Assert.Equal("HD-7", data.MaHoaDon);
            Assert.Equal(77, data.LichSuThanhToanId);
            Assert.Equal("Chi nhánh 1", data.TenChiNhanh);
            Assert.Equal("1 Đường A", data.DiaChiChiNhanh);
            Assert.Equal("P101", data.TenPhong);
            Assert.Equal("Nguyễn Văn A", data.TenNguoiThanhToan);
            Assert.Equal(9, data.Thang);
            Assert.Equal(2026, data.Nam);
            Assert.Equal("03/10/2026 09:30:00", data.NgayThanhToanVn);
            Assert.Equal("FT26ABC", data.MaThamChieu);
            Assert.Equal(2_000_000m, data.TongTien);
            Assert.Equal(2_000_000m, data.SoTienLanNay);
            Assert.Equal("https://tro.test/KhachThue/HoaDon?hoaDonId=7", data.LinkXemBienLai);
        }

        [Fact]
        public void ReceiptEmail_FullyPaid_HasNoDebtLeft()
        {
            var data = PaymentNotificationFormatter.BuildReceiptEmail(Receipt(), null, null);

            Assert.True(data.DaThuDu);
            Assert.Equal(0m, data.ConNo);
            Assert.Equal(2_000_000m, data.LuyKeDaThanhToan);
            Assert.Equal(0m, data.TienThua);
        }

        [Fact]
        public void ReceiptEmail_SecondInstalment_CarriesTheRunningTotalAndTheDebtLeft()
        {
            var receipt = Receipt(b =>
            {
                b.SoTienThanhToan = 800_000m;
                b.DotThu = 2;
                b.LuyKe = 1_300_000m;
            });

            var data = PaymentNotificationFormatter.BuildReceiptEmail(receipt, null, null);

            Assert.False(data.DaThuDu);
            Assert.Equal(2, data.DotThu);
            Assert.Equal(800_000m, data.SoTienLanNay);
            Assert.Equal(1_300_000m, data.LuyKeDaThanhToan);
            Assert.Equal(700_000m, data.ConNo);
        }

        [Fact]
        public void ReceiptEmail_ReadsTheSurplusFromTheLedgerTag()
        {
            var receipt = Receipt(b => b.GhiChu = "[TIEN-THUA:500000] [SEPAY] MBBank · REF FT26ABC · ND: TTPTHD7");

            var data = PaymentNotificationFormatter.BuildReceiptEmail(receipt, null, null);

            Assert.Equal(500_000m, data.TienThua);
            Assert.True(data.DaThuDu);
        }

        [Fact]
        public void ReceiptEmail_DebtIsNeverNegative()
        {
            var receipt = Receipt(b => b.LuyKe = 2_500_000m);

            var data = PaymentNotificationFormatter.BuildReceiptEmail(receipt, null, null);

            Assert.Equal(0m, data.ConNo);
            Assert.True(data.DaThuDu);
        }

        [Theory]
        [InlineData("SEPAY-FT26ABC", PhuongThucThanhToan.ChuyenKhoan, "Chuyển khoản 24/7 (VietQR, tự động)")]
        [InlineData("sepay-ft26abc", PhuongThucThanhToan.ChuyenKhoan, "Chuyển khoản 24/7 (VietQR, tự động)")]
        [InlineData("FT-TAY", PhuongThucThanhToan.ChuyenKhoan, "Chuyển khoản")]
        [InlineData("TM-20261003", PhuongThucThanhToan.TienMat, "Tiền mặt")]
        public void ReceiptEmail_PaymentMethodLabel(string maGiaoDich, PhuongThucThanhToan phuongThuc, string expected)
        {
            var receipt = Receipt(b =>
            {
                b.MaGiaoDich = maGiaoDich;
                b.PhuongThuc = phuongThuc;
            });

            var data = PaymentNotificationFormatter.BuildReceiptEmail(receipt, null, null);

            Assert.Equal(expected, data.PhuongThuc);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ReceiptEmail_BlankReferenceAndLink_BecomeNull(string? blank)
        {
            var data = PaymentNotificationFormatter.BuildReceiptEmail(Receipt(), blank, blank);

            Assert.Null(data.MaThamChieu);
            Assert.Null(data.LinkXemBienLai);
        }

        [Fact]
        public void ReceiptEmail_TrimsTheReference_AndDoesNotEscapeAnything()
        {
            var receipt = Receipt(b => b.TenKhachThue = "<b>Tèo</b> & Tí");

            var data = PaymentNotificationFormatter.BuildReceiptEmail(receipt, "  FT<1>  ", null);

            Assert.Equal("FT<1>", data.MaThamChieu);
            Assert.Equal("<b>Tèo</b> & Tí", data.TenNguoiThanhToan);
        }
    }
}
