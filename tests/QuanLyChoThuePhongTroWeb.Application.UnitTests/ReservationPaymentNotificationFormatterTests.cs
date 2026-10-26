using System;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Câu chữ thông báo trong app, Telegram và email cho tiền giữ chỗ (spec giữ chỗ §8, X6).
    public class ReservationPaymentNotificationFormatterTests
    {
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private static TransferOutcome Outcome(
            SystemReservationRecordResult? giuCho = null, OnlinePaymentChannel channel = OnlinePaymentChannel.SePay,
            decimal soTien = 1_000_000m, decimal conLai = 0m, decimal tienThua = 0m)
        {
            var transfer = new IncomingBankTransfer(92704, "MBBank", TransferTime, "0123456789", null, null, "TTPTGC5", "in", soTien, "FT26ABC", "TTPTGC5")
            {
                Kenh = channel
            };
            if (channel == OnlinePaymentChannel.VnPay)
            {
                transfer = new IncomingBankTransfer(0, "NCB", TransferTime, null, null, null, "Thanh toan giu cho", "in", soTien, "14123456", "GC5_1")
                {
                    Kenh = OnlinePaymentChannel.VnPay,
                    CardType = "ATM"
                };
            }

            var result = giuCho ?? FakeReservationAutoPaymentStore.Recorded();
            return new TransferOutcome
            {
                Kind = TransferOutcomeKind.Recorded,
                Transfer = transfer,
                Code = "TTPTGC5",
                SoTienGhiNhan = soTien,
                TongTien = result.SoTienGiuCho,
                DaThu = result.DaNhanSau,
                ConLai = conLai,
                TienThua = tienThua,
                GiuCho = result
            };
        }

        // ---------- Tin Telegram ----------

        [Fact]
        public void Telegram_HoldBecameEffective_UsesTheSuccessTitle()
        {
            var text = ReservationPaymentNotificationFormatter.FormatTelegram(Outcome());

            Assert.StartsWith("✅ THU TIỀN GIỮ CHỖ — GIỮ CHỖ ĐÃ CÓ HIỆU LỰC", text);
            Assert.DoesNotContain("Lý do:", text);
            Assert.DoesNotContain("Gợi ý:", text);
            Assert.Contains("Chi nhánh: Chi nhánh A", text);
            Assert.Contains("Phòng: P101", text);
            Assert.Contains("Khách: Nguyễn Văn A", text);
            Assert.Contains("Vừa nhận: +1.000.000 VNĐ", text);
            Assert.Contains("Đã nhận: 1.000.000/1.000.000 VNĐ", text);
            Assert.Contains("Còn lại: 0 VNĐ", text);
            Assert.Contains("Mã GD: FT26ABC", text);
        }

        [Fact]
        public void Telegram_Partial_UsesThePartialTitle()
        {
            var giuCho = FakeReservationAutoPaymentStore.Recorded(daNhanSau: 400_000m, vuaCoHieuLuc: false, trangThai: "ChoXacNhanTien");

            var text = ReservationPaymentNotificationFormatter.FormatTelegram(Outcome(giuCho, soTien: 400_000m, conLai: 600_000m));

            Assert.StartsWith("⚠️ THU TIỀN GIỮ CHỖ (MỘT PHẦN)", text);
            Assert.Contains("Còn lại: 600.000 VNĐ", text);
            Assert.DoesNotContain("Gợi ý:", text);
        }

        [Fact]
        public void Telegram_LateRefund_ShowsTheRefundTitle_TheReason_AndTheHint()
        {
            var giuCho = FakeReservationAutoPaymentStore.Recorded(canHoanTien: true, vuaCoHieuLuc: false, trangThai: "HetHan");

            var text = ReservationPaymentNotificationFormatter.FormatTelegram(Outcome(giuCho));

            var lines = text.Split('\n');
            Assert.Equal("🔔 TIỀN GIỮ CHỖ CẦN HOÀN", lines[0]);
            Assert.Equal("Lý do: Giữ chỗ #5 đã quá hạn thanh toán hoặc không còn hiệu lực (Hết hạn)", lines[1]);
            Assert.Equal("Gợi ý: Vào Giữ chỗ → Chi tiết #5 → Quyết định hoàn tiền", lines[^1]);
        }

        [Fact]
        public void Telegram_Surplus_ShowsTheRefundTitle_AndTheSurplusReason()
        {
            var giuCho = FakeReservationAutoPaymentStore.Recorded(daNhanSau: 1_300_000m);

            var text = ReservationPaymentNotificationFormatter.FormatTelegram(Outcome(giuCho, tienThua: 300_000m));

            var lines = text.Split('\n');
            Assert.Equal("🔔 TIỀN GIỮ CHỖ CẦN HOÀN", lines[0]);
            Assert.Equal("Lý do: Khách chuyển dư 300.000 VNĐ so với số tiền giữ chỗ", lines[1]);
            Assert.StartsWith("Gợi ý:", lines[^1]);
        }

        [Fact]
        public void Telegram_ChannelLine_ShowsSePayOrVnPay()
        {
            var sePay = ReservationPaymentNotificationFormatter.FormatTelegram(Outcome());
            var vnPay = ReservationPaymentNotificationFormatter.FormatTelegram(Outcome(channel: OnlinePaymentChannel.VnPay));

            Assert.Contains("Kênh: Chuyển khoản VietQR (MBBank)", sePay);
            Assert.Contains("Kênh: Thẻ ATM / Visa / MasterCard (VNPay, NCB)", vnPay);
            Assert.Contains("Mã GD: 14123456", vnPay);
        }

        [Fact]
        public void Telegram_EscapesHtmlInTheGuestAndBranchNames()
        {
            var giuCho = FakeReservationAutoPaymentStore.Recorded() with { TenKhach = "A <b>&</b>", TenChiNhanh = "X & Y", SoPhong = "<P1>" };

            var text = ReservationPaymentNotificationFormatter.FormatTelegram(Outcome(giuCho));

            Assert.Contains("Khách: A &lt;b&gt;&amp;&lt;/b&gt;", text);
            Assert.Contains("Chi nhánh: X &amp; Y", text);
            Assert.Contains("Phòng: &lt;P1&gt;", text);
            Assert.DoesNotContain("<b>", text);
        }

        [Fact]
        public void Telegram_ShowsTheTransferTimeInVietnamTime()
        {
            var text = ReservationPaymentNotificationFormatter.FormatTelegram(Outcome());

            Assert.Contains("03/10/2026 09:30", text);
        }

        // ---------- Thông báo trong app ----------

        [Fact]
        public void StaffTitle_IsCutAt255Characters_AndNeverInsideASurrogatePair()
        {
            var giuCho = FakeReservationAutoPaymentStore.Recorded() with { SoPhong = new string('P', 240) + "😀😀😀😀😀" };

            var title = ReservationPaymentNotificationFormatter.StaffTitle(Outcome(giuCho));

            Assert.True(title.Length <= 255);
            Assert.False(char.IsHighSurrogate(title[^1]));
        }

        [Fact]
        public void StaffTitle_NamesTheAmountChannelAndRoom()
        {
            Assert.Equal("Đã nhận tiền giữ chỗ 1.000.000đ qua SePay — phòng P101",
                ReservationPaymentNotificationFormatter.StaffTitle(Outcome()));
            Assert.Equal("Đã nhận tiền giữ chỗ 1.000.000đ qua VNPay — phòng P101",
                ReservationPaymentNotificationFormatter.StaffTitle(Outcome(channel: OnlinePaymentChannel.VnPay)));
        }

        // ---------- Email ----------

        [Fact]
        public void Email_HoldBecameEffective_SaysUntilWhen()
        {
            var email = ReservationPaymentNotificationFormatter.BuildEmail(Outcome());

            Assert.Equal("Nguyễn Văn A", email.TenNguoiNhan);
            Assert.Equal("Giữ chỗ phòng P101 đã có hiệu lực", email.TieuDe);
            Assert.Contains("Xin chào Nguyễn Văn A,", email.NoiDung);
            Assert.Contains("Hệ thống đã nhận 1.000.000đ qua SePay cho giữ chỗ phòng P101 (Chi nhánh A) lúc 03/10/2026 09:30.", email.NoiDung);
            Assert.Contains("Giữ chỗ đã có hiệu lực tới 10/10/2026 10:00.", email.NoiDung);
            Assert.Contains("Mã giao dịch: FT26ABC", email.NoiDung);
            Assert.EndsWith("Trân trọng,\nBan quản lý nhà trọ", email.NoiDung);
        }

        [Fact]
        public void Email_HoldBecameEffective_WithoutAContractDeadline_OmitsTheUntilPart()
        {
            var giuCho = FakeReservationAutoPaymentStore.Recorded() with { HanKyHopDongUtc = null };

            var email = ReservationPaymentNotificationFormatter.BuildEmail(Outcome(giuCho));

            Assert.Contains("Giữ chỗ đã có hiệu lực.", email.NoiDung);
            Assert.DoesNotContain(" tới ", email.NoiDung);
        }

        [Fact]
        public void Email_Surplus_AddsTheRefundSentence()
        {
            var email = ReservationPaymentNotificationFormatter.BuildEmail(Outcome(tienThua: 300_000m));

            Assert.Contains("Số tiền chuyển dư 300.000đ sẽ được nhân viên liên hệ hoàn lại.", email.NoiDung);
        }

        [Fact]
        public void Email_Partial_AsksForTheRemainderBeforeTheDeadline()
        {
            var giuCho = FakeReservationAutoPaymentStore.Recorded(daNhanSau: 400_000m, vuaCoHieuLuc: false, trangThai: "ChoXacNhanTien");

            var email = ReservationPaymentNotificationFormatter.BuildEmail(Outcome(giuCho, soTien: 400_000m, conLai: 600_000m));

            Assert.Equal("Đã nhận 400.000đ tiền giữ chỗ phòng P101", email.TieuDe);
            Assert.Contains("Đã nhận 400.000đ / 1.000.000đ; còn lại 600.000đ. Vui lòng chuyển nốt trước 04/10/2026 10:00.", email.NoiDung);
        }

        [Fact]
        public void Email_LateRefund_SaysTheMoneyWillBeRefunded()
        {
            var giuCho = FakeReservationAutoPaymentStore.Recorded(canHoanTien: true, vuaCoHieuLuc: false, trangThai: "HetHan");

            var email = ReservationPaymentNotificationFormatter.BuildEmail(Outcome(giuCho));

            Assert.Equal("Đã nhận 1.000.000đ cho giữ chỗ phòng P101 — khoản này sẽ được hoàn", email.TieuDe);
            Assert.Contains("Giữ chỗ đã quá hạn thanh toán hoặc không còn hiệu lực nên khoản này sẽ được hoàn lại. Nhân viên sẽ liên hệ với bạn.", email.NoiDung);
        }

        [Fact]
        public void Email_VnPay_NamesTheChannel()
        {
            var email = ReservationPaymentNotificationFormatter.BuildEmail(Outcome(channel: OnlinePaymentChannel.VnPay));

            Assert.Contains("qua VNPay", email.NoiDung);
            Assert.Contains("Mã giao dịch: 14123456", email.NoiDung);
        }

        [Fact]
        public void OutcomeWithoutReservationData_IsRejected()
        {
            var outcome = Outcome() with { GiuCho = null };

            Assert.Throws<ArgumentException>(() => ReservationPaymentNotificationFormatter.StaffTitle(outcome));
            Assert.Throws<ArgumentException>(() => ReservationPaymentNotificationFormatter.FormatTelegram(outcome));
            Assert.Throws<ArgumentException>(() => ReservationPaymentNotificationFormatter.BuildEmail(outcome));
        }
    }
}
