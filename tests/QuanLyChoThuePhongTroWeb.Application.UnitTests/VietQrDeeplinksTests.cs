using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Link mở app ngân hàng trên panel thanh toán của khách thuê (spec thanh toán trực tuyến §3.2 mục 3).
    public class VietQrDeeplinksTests
    {
        [Fact]
        public void Banks_AreThe12ChosenBanks_InTheAgreedOrder_WithTheVietQrAppIds()
        {
            var expected = new[]
            {
                ("MBBank", "mb"), ("Vietcombank", "vcb"), ("Techcombank", "tcb"), ("BIDV", "bidv"),
                ("ACB", "acb"), ("VietinBank", "icb"), ("VPBank", "vpb"), ("TPBank", "tpb"),
                ("Agribank", "vba"), ("OCB", "ocb"), ("HDBank", "hdb"), ("VIB", "vib")
            };

            Assert.Equal(expected, VietQrDeeplinks.Banks.Select(b => (b.TenNganHang, b.AppId)).ToArray());
        }

        [Fact]
        public void Banks_DoNotIncludeSacombankOrMsb_WhichVietQrDoesNotList()
        {
            Assert.DoesNotContain(VietQrDeeplinks.Banks, b => b.AppId is "stb" or "msb");
        }

        [Fact]
        public void Build_ProducesTheDocumentedUrl()
        {
            var url = VietQrDeeplinks.Build("mb", "0123456789", "MB", 1_500_000m, "TTPTHD7", "NHA TRO");

            Assert.Equal("https://dl.vietqr.io/pay?app=mb&ba=0123456789@MB&am=1500000&tn=TTPTHD7&bn=NHA%20TRO", url);
        }

        [Fact]
        public void Build_EscapesReservedCharactersInEveryValue_ButKeepsTheAtSignOfBa()
        {
            var url = VietQrDeeplinks.Build("mb", "01 23", "M&B", 1_500_000m, "TT PT&?", "A&B?C D");

            Assert.Equal("https://dl.vietqr.io/pay?app=mb&ba=01%2023@M%26B&am=1500000&tn=TT%20PT%26%3F&bn=A%26B%3FC%20D", url);
        }

        [Fact]
        public void Build_PrintsTheAmountAsAWholeNumberWithoutSeparators()
        {
            var url = VietQrDeeplinks.Build("mb", "1", "MB", 2_000_000.00m, "TTPTHD7", "A");

            Assert.Contains("&am=2000000&", url);
        }

        [Fact]
        public void BuildAll_ReturnsOneLinkPerBank_WithTheSameAccountAmountAndMemo()
        {
            var settings = new VietQrSettings { BankId = "MB", AccountNumber = "0123456789", AccountName = "NHA TRO" };

            var links = VietQrDeeplinks.BuildAll(settings, 1_500_000m, "TTPTHD7");

            Assert.Equal(VietQrDeeplinks.Banks.Select(b => b.TenNganHang).ToArray(), links.Select(l => l.TenNganHang).ToArray());
            Assert.All(links, link =>
            {
                Assert.StartsWith("https://dl.vietqr.io/pay?app=", link.Url);
                Assert.Contains("&ba=0123456789@MB&am=1500000&tn=TTPTHD7&bn=NHA%20TRO", link.Url);
            });
            Assert.Contains("?app=vcb&", links.Single(l => l.TenNganHang == "Vietcombank").Url);
        }
    }
}
