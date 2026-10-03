using System;
using System.IO;
using System.Text.RegularExpressions;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoicePaymentKernelTests
    {
        // ---------- InvoicePaymentPolicy.ResolveRequestAmount (spec §10) ----------

        [Fact]
        public void ResolveRequestAmount_NoAmount_UsesRemaining()
        {
            var (soTien, error) = InvoicePaymentPolicy.ResolveRequestAmount(1_500_000m, false, null, null);

            Assert.Null(error);
            Assert.Equal(1_500_000m, soTien);
        }

        [Theory]
        [InlineData(500_000, true, 300_000, null)]
        [InlineData(300_000, true, 300_000, null)]
        [InlineData(200_000, true, 300_000, "tối thiểu")]
        [InlineData(500_000, false, null, "không cho phép thanh toán một phần")]
        [InlineData(2_500_000, true, 300_000, "vượt số còn lại")]
        [InlineData(500_000.5, true, 300_000, "số nguyên")]
        [InlineData(0, true, 300_000, "số nguyên")]
        public void ResolveRequestAmount_PartialRules(decimal yeuCau, bool choPhep, int? toiThieu, string? expectedError)
        {
            var (_, error) = InvoicePaymentPolicy.ResolveRequestAmount(2_000_000m, choPhep, toiThieu, yeuCau);

            if (expectedError == null)
            {
                Assert.Null(error);
            }
            else
            {
                Assert.Contains(expectedError, error);
            }
        }

        [Fact]
        public void ResolveRequestAmount_RemainingBelowMinimum_MustPayExactRemaining()
        {
            var (_, partialError) = InvoicePaymentPolicy.ResolveRequestAmount(200_000m, true, 300_000m, 100_000m);
            var (exact, exactError) = InvoicePaymentPolicy.ResolveRequestAmount(200_000m, true, 300_000m, 200_000m);

            Assert.Contains("đúng 200.000đ", partialError);
            Assert.Null(exactError);
            Assert.Equal(200_000m, exact);
        }

        // ---------- InvoicePaymentPolicy.DecideRecording (spec §13.2) ----------

        [Theory]
        [InlineData(2_000_000, 2_000_000, 2_000_000, false, RecordingKind.Exact, 2_000_000, 0)]
        [InlineData(1_900_000, 2_000_000, 2_000_000, false, RecordingKind.Variance, 1_900_000, -100_000)]
        [InlineData(1_500_000, 1_000_000, 2_000_000, false, RecordingKind.Variance, 1_500_000, 500_000)]
        [InlineData(2_500_000, 2_000_000, 2_000_000, false, RecordingKind.NeedsAdmin, 0, 500_000)]
        [InlineData(2_500_000, 2_000_000, 2_000_000, true, RecordingKind.Overpay, 2_000_000, 500_000)]
        public void DecideRecording_FollowsSpecTable(decimal thucNhan, decimal soLuot, decimal conLai, bool isAdmin,
            RecordingKind kind, decimal ghiNhan, decimal chenh)
        {
            var decision = InvoicePaymentPolicy.DecideRecording(thucNhan, soLuot, conLai, isAdmin);

            Assert.Equal(kind, decision.Kind);
            Assert.Equal(ghiNhan, decision.SoTienGhiNhan);
            Assert.Equal(chenh, decision.ChenhLech);
        }

        [Fact]
        public void DecideRecording_ManualCollectionWithoutRequest_IsExactUpToRemaining()
        {
            Assert.Equal(RecordingKind.Exact, InvoicePaymentPolicy.DecideRecording(700_000m, null, 2_000_000m, false).Kind);
        }

        [Fact]
        public void ValidateNotFuture_AllowsToleranceOnly()
        {
            var now = new DateTime(2026, 10, 3, 3, 0, 0, DateTimeKind.Utc);

            Assert.Null(InvoicePaymentPolicy.ValidateNotFuture(now.AddMinutes(5), now, 5, "Ngày"));
            Assert.NotNull(InvoicePaymentPolicy.ValidateNotFuture(now.AddMinutes(6), now, 5, "Ngày"));
        }

        [Fact]
        public void ValidateLyDo_RequiresTextUpTo500()
        {
            Assert.NotNull(InvoicePaymentPolicy.ValidateLyDo("   ", "Lý do"));
            Assert.Null(InvoicePaymentPolicy.ValidateLyDo(new string('a', 500), "Lý do"));
            Assert.NotNull(InvoicePaymentPolicy.ValidateLyDo(new string('a', 501), "Lý do"));
        }

        [Theory]
        [InlineData("[TIEN-THUA:500000] tự gõ")]
        [InlineData("ghi chú [chenh-lech:+1]")]
        [InlineData("[CHO-ADMIN] giả")]
        [InlineData("abc | [DA-HUY 01/01/2026 00:00 bởi #1] x")]
        public void UserText_WithReservedTag_IsRejected(string text)
        {
            Assert.NotNull(InvoicePaymentPolicy.ValidateGhiChuTuyChon(text));
            Assert.NotNull(InvoicePaymentPolicy.ValidateLyDo(text, "Lý do"));
            Assert.Null(InvoicePaymentPolicy.ValidateGhiChuTuyChon("Chuyển gộp [FT999]"));
        }

        // ---------- PaymentNoteTags ----------

        [Fact]
        public void Tags_ToDisplayText_ReplacesTagsWithLabels_AndDropsVoidTrail()
        {
            Assert.Null(PaymentNoteTags.ToDisplayText(null));
            Assert.Equal("Tiền thừa 500.000đ · Hoàn lại", PaymentNoteTags.ToDisplayText(PaymentNoteTags.TienThua(500_000m, "Hoàn lại")));
            Assert.Equal("Thiếu 100.000đ so với lượt · thiếu", PaymentNoteTags.ToDisplayText(PaymentNoteTags.ChenhLech(-100_000m, "thiếu")));
            Assert.Equal("Thu tại quầy", PaymentNoteTags.ToDisplayText("Thu tại quầy"));

            var voided = PaymentNoteTags.AppendDaHuy("Thu tại quầy", DateTime.UtcNow, 1, "nhầm");
            Assert.Equal("Đã hủy ghi nhận · Thu tại quầy", PaymentNoteTags.ToDisplayText(voided));
        }

        [Fact]
        public void Tags_ComposeWithSignedAmounts()
        {
            Assert.Equal("[CHENH-LECH:-100000] Khách chuyển thiếu", PaymentNoteTags.ChenhLech(-100_000m, "Khách chuyển thiếu"));
            Assert.Equal("[CHENH-LECH:+500000] Dư", PaymentNoteTags.ChenhLech(500_000m, "Dư"));
            Assert.Equal("[TIEN-THUA:500000] Hoàn lại sau", PaymentNoteTags.TienThua(500_000m, "Hoàn lại sau"));
            Assert.StartsWith(PaymentNoteTags.ChoAdminPrefix, PaymentNoteTags.ChoAdmin(2_500_000m, 2_000_000m, null));
            Assert.Contains("2.500.000", PaymentNoteTags.ChoAdmin(2_500_000m, 2_000_000m, null));
        }

        [Fact]
        public void Tags_AppendDaHuy_UsesVietnamTime()
        {
            var text = PaymentNoteTags.AppendDaHuy("[TIEN-THUA:500000] Hoàn lại", new DateTime(2026, 10, 3, 17, 30, 0, DateTimeKind.Utc), 1, "Ngân hàng hoàn");

            Assert.Equal("[TIEN-THUA:500000] Hoàn lại | [DA-HUY 04/10/2026 00:30 bởi #1] Ngân hàng hoàn", text);
        }

        [Fact]
        public void Tags_Parse_RoundTrips()
        {
            var info = PaymentNoteTags.Parse(PaymentNoteTags.AppendDaHuy(PaymentNoteTags.TienThua(500_000m, "Hoàn lại"), DateTime.UtcNow, 1, "sai"));

            Assert.Equal(500_000m, info.TienThua);
            Assert.Null(info.ChenhLech);
            Assert.True(info.DaHuy);
            Assert.StartsWith("Hoàn lại", info.NoiDung);

            var chenh = PaymentNoteTags.Parse(PaymentNoteTags.ChenhLech(-100_000m, "thiếu"));
            Assert.Equal(-100_000m, chenh.ChenhLech);
            Assert.Equal("thiếu", chenh.NoiDung);
            Assert.Contains("Thiếu 100.000đ so với lượt", PaymentNoteTags.Labels(chenh));
        }

        // ---------- ProofImageValidator ----------

        private static UploadFile File(byte[] bytes, string name, string type) => new(new MemoryStream(bytes), name, type, bytes.Length);

        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        private static readonly byte[] Webp = { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' };

        [Fact]
        public void ProofImage_AcceptsMatchingTypes_AndRewindsStream()
        {
            var jpeg = File(Jpeg, "a.JPG", "image/jpg");

            Assert.Null(ProofImageValidator.Validate(jpeg, 1024));
            Assert.Equal(0, jpeg.Content.Position);
            Assert.Null(ProofImageValidator.Validate(File(Png, "a.png", "image/png"), 1024));
            Assert.Null(ProofImageValidator.Validate(File(Webp, "a.webp", "image/webp"), 1024));
        }

        [Theory]
        [InlineData("a.gif", "image/jpeg", "JPG, PNG hoặc WEBP")]
        [InlineData("a.png", "image/jpeg", "không khớp với đuôi")]
        [InlineData("a.jpg", "image/jpeg", null)]
        public void ProofImage_ChecksExtensionAndContentType(string name, string type, string? expected)
        {
            var error = ProofImageValidator.Validate(File(Jpeg, name, type), 1024);

            if (expected == null) Assert.Null(error); else Assert.Contains(expected, error);
        }

        [Fact]
        public void ProofImage_RejectsMagicBytesMismatch_AndOversize()
        {
            Assert.Contains("không khớp định dạng", ProofImageValidator.Validate(File(Png, "a.jpg", "image/jpeg"), 1024));
            Assert.Contains("dung lượng", ProofImageValidator.Validate(File(Jpeg, "a.jpg", "image/jpeg"), 4));
            Assert.NotNull(ProofImageValidator.Validate(null, 1024));
        }

        // ---------- PaymentCodeGenerator ----------

        [Fact]
        public void CodeGenerator_UsesReadableAlphabet()
        {
            var generator = new PaymentCodeGenerator();

            for (var i = 0; i < 200; i++)
            {
                Assert.Matches(new Regex("^TT42[A-HJ-NP-Z2-9]{6}$"), generator.NewRequestCode(42));
            }

            Assert.Matches(new Regex("^TM-20261004-[A-HJ-NP-Z2-9]{8}$"),
                generator.NewLedgerCode("TM", new DateTime(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc)));
        }
    }
}
