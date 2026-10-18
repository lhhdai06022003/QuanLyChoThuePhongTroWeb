using System;
using System.Net;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Emails;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Hàm thuần, không cần DB: thân email biên lai thanh toán (spec thanh toán trực tuyến §3.4, §6.3).
    public class MailKitPaymentReceiptHtmlTests
    {
        private static PaymentReceiptEmail Receipt() => new()
        {
            HoaDonId = 7,
            MaHoaDon = "HD-7",
            LichSuThanhToanId = 77,
            TenChiNhanh = "Chi nhánh 1",
            DiaChiChiNhanh = "1 Đường A, Quận 1",
            TenPhong = "P101",
            TenNguoiThanhToan = "Nguyễn Văn A",
            Thang = 9,
            Nam = 2026,
            NgayThanhToanVn = "03/10/2026 09:30:00",
            PhuongThuc = "Chuyển khoản 24/7 (VietQR, tự động)",
            MaThamChieu = "FT26ABC",
            TongTien = 2_000_000m,
            SoTienLanNay = 2_000_000m,
            LuyKeDaThanhToan = 2_000_000m,
            ConNo = 0m,
            TienThua = 0m,
            DaThuDu = true,
            DotThu = 1,
            LinkXemBienLai = "https://tro.test/KhachThue/HoaDon?hoaDonId=7"
        };

        [Fact]
        public void HasTheReceiptTitle_AndTheBranchName()
        {
            var html = MailKitEmailService.BuildPaymentReceiptHtml(Receipt());

            Assert.Contains("BIÊN LAI XÁC NHẬN THU TIỀN PHÒNG TRỌ", html);
            // WebUtility.HtmlEncode đổi ký tự Latin-1 có dấu (như "á") thành thực thể số; email hiển thị đúng chữ gốc.
            Assert.Contains($"<p>{WebUtility.HtmlEncode("Chi nhánh 1")}</p>", html);
        }

        [Fact]
        public void PaidInFull_ShowsTheGreenPaidBadge()
        {
            var html = MailKitEmailService.BuildPaymentReceiptHtml(Receipt());

            Assert.Contains("<span class='badge'>ĐÃ THU TIỀN</span>", html);
            Assert.DoesNotContain("XÁC NHẬN THU ĐỢT", html);
        }

        [Fact]
        public void PartialPayment_ShowsTheInstalmentBadge()
        {
            var data = Receipt() with
            {
                DaThuDu = false,
                DotThu = 2,
                SoTienLanNay = 800_000m,
                LuyKeDaThanhToan = 1_300_000m,
                ConNo = 700_000m
            };

            var html = MailKitEmailService.BuildPaymentReceiptHtml(data);

            Assert.Contains("<span class='badge badge-partial'>XÁC NHẬN THU ĐỢT 2</span>", html);
            Assert.DoesNotContain("ĐÃ THU TIỀN", html);
            Assert.DoesNotContain("<span class='badge'>", html);
        }

        [Fact]
        public void ShowsTheInvoiceReferenceAndPaymentDetails()
        {
            var html = MailKitEmailService.BuildPaymentReceiptHtml(Receipt());

            Assert.Contains("<td class='label'>Mã hóa đơn:</td><td class='value'><strong>HD-7</strong> (#HD7)</td>", html);
            Assert.Contains("<td class='label'>Mã tham chiếu:</td><td class='value'>FT26ABC</td>", html);
            Assert.Contains("<td class='label'>Ngày giờ thanh toán:</td><td class='value'>03/10/2026 09:30:00</td>", html);
            Assert.Contains("<td class='label'>Phương thức:</td><td class='value'>Chuyển khoản 24/7 (VietQR, tự động)</td>", html);
            Assert.Contains($"<td class='label'>Chi nhánh:</td><td class='value'>{WebUtility.HtmlEncode("Chi nhánh 1")}</td>", html);
            Assert.Contains("<td class='label'>Địa chỉ:</td><td class='value'>1 Đường A, Quận 1</td>", html);
            Assert.Contains("<td class='label'>Phòng:</td><td class='value'>P101</td>", html);
            Assert.Contains("<td class='label'>Người thanh toán:</td><td class='value'>Nguyễn Văn A</td>", html);
            Assert.Contains("<td class='label'>Kỳ thanh toán:</td><td class='value'>Tháng 9/2026</td>", html);
        }

        [Fact]
        public void WithoutAReferenceCode_DropsTheReferenceRow()
        {
            foreach (var blank in new string?[] { null, "", "   " })
            {
                var html = MailKitEmailService.BuildPaymentReceiptHtml(Receipt() with { MaThamChieu = blank });

                Assert.DoesNotContain("Mã tham chiếu", html);
            }
        }

        [Fact]
        public void ShowsTheAmountsWithDotsAndTheDongSign()
        {
            var data = Receipt() with
            {
                DaThuDu = false,
                DotThu = 2,
                TongTien = 3_000_000m,
                SoTienLanNay = 800_000m,
                LuyKeDaThanhToan = 1_300_000m,
                ConNo = 1_700_000m
            };

            var html = MailKitEmailService.BuildPaymentReceiptHtml(data);

            Assert.Contains("<td>Tổng hóa đơn</td><td class='amount'>3.000.000đ</td>", html);
            Assert.Contains("<td>Số tiền lần này</td><td class='amount'>800.000đ</td>", html);
            Assert.Contains("<td>Lũy kế đã thanh toán</td><td class='amount'>1.300.000đ</td>", html);
            Assert.Contains("<td>Còn nợ sau lần này</td><td class='amount'>1.700.000đ</td>", html);
        }

        [Fact]
        public void Surplus_ShowsTheNoteThatTheLandlordWillContact()
        {
            var html = MailKitEmailService.BuildPaymentReceiptHtml(Receipt() with { TienThua = 500_000m });

            Assert.Contains("Tiền thừa 500.000đ — chủ trọ sẽ liên hệ", html);
        }

        [Fact]
        public void WithoutSurplus_HasNoSurplusNote()
        {
            var html = MailKitEmailService.BuildPaymentReceiptHtml(Receipt());

            Assert.DoesNotContain("Tiền thừa", html);
        }

        [Fact]
        public void WithALink_ShowsTheViewReceiptButton()
        {
            var html = MailKitEmailService.BuildPaymentReceiptHtml(Receipt());

            Assert.Contains("<a class='button' href='https://tro.test/KhachThue/HoaDon?hoaDonId=7'>Xem biên lai</a>", html);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithoutALink_HasNoButton(string? link)
        {
            var html = MailKitEmailService.BuildPaymentReceiptHtml(Receipt() with { LinkXemBienLai = link });

            Assert.DoesNotContain("Xem biên lai", html);
            Assert.DoesNotContain("<a ", html);
        }

        [Fact]
        public void EncodesEveryDynamicString()
        {
            var data = Receipt() with
            {
                MaHoaDon = "HD<7>",
                TenChiNhanh = "CN <b>1</b>",
                DiaChiChiNhanh = "1 & 2 <street>",
                TenPhong = "P<101>",
                TenNguoiThanhToan = "<script>alert(1)</script>",
                NgayThanhToanVn = "03/10/2026 <09:30>",
                PhuongThuc = "CK <tự động>",
                MaThamChieu = "FT<1>&2"
            };

            var html = MailKitEmailService.BuildPaymentReceiptHtml(data);

            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
            Assert.Contains("HD&lt;7&gt;", html);
            Assert.Contains("CN &lt;b&gt;1&lt;/b&gt;", html);
            Assert.Contains("1 &amp; 2 &lt;street&gt;", html);
            Assert.Contains("P&lt;101&gt;", html);
            Assert.Contains("03/10/2026 &lt;09:30&gt;", html);
            Assert.Contains("CK &lt;tự động&gt;", html);
            Assert.Contains("FT&lt;1&gt;&amp;2", html);
            Assert.DoesNotContain("<script>", html);
            Assert.DoesNotContain("<street>", html);
            Assert.DoesNotContain("<b>1</b>", html);
        }

        [Fact]
        public void EncodesTheLinkInTheHrefAttribute()
        {
            var html = MailKitEmailService.BuildPaymentReceiptHtml(Receipt() with { LinkXemBienLai = "https://tro.test/a?x=1&y=2'onclick='alert(1)" });

            Assert.Contains("href='https://tro.test/a?x=1&amp;y=2&#39;onclick=&#39;alert(1)'", html);
            Assert.DoesNotContain("y=2'onclick", html);
        }

        [Fact]
        public void IsAWellFormedHtmlDocument()
        {
            var html = MailKitEmailService.BuildPaymentReceiptHtml(Receipt());

            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("<meta charset='utf-8' />", html);
            Assert.EndsWith("</html>", html);
            Assert.Contains("@media (prefers-color-scheme: dark)", html);
        }
    }
}
