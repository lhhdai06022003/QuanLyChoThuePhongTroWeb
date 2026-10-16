using System;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class IncomingTransferClassifierTests
    {
        private const string Account = "0123456789";

        private sealed class FakeHandler : IIncomingTransferHandler
        {
            public FakeHandler(string prefix) => Prefix = prefix;

            public string Prefix { get; }

            public Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, string code, CancellationToken ct = default)
                => throw new NotSupportedException("Bộ phân loại không được gọi bộ xử lý.");
        }

        private static IncomingBankTransfer Transfer(
            string? type = "in", string? account = Account, decimal amount = 2_000_000m,
            string? code = null, string? content = "TTPTHD105") => new(
                92704, "MBBank", new DateTime(2026, 10, 16, 3, 0, 0, DateTimeKind.Utc), account, null, code,
                content, type, amount, "FT26ABC", null);

        private static IncomingTransferClassifier NewClassifier(string account = Account, params IIncomingTransferHandler[] handlers)
            => new(handlers.Length == 0 ? new IIncomingTransferHandler[] { new FakeHandler("TTPTHD") } : handlers,
                new VietQrSettings { AccountNumber = account });

        [Theory]
        [InlineData("out")]
        [InlineData("IN")]
        [InlineData("")]
        [InlineData(null)]
        public void NotIncomingMoney_IsIgnored_BeforeAnyOtherCheck(string? type)
        {
            var result = NewClassifier().Classify(Transfer(type: type, account: "999"));

            Assert.Equal(TransferClassificationKind.Ignored, result.Kind);
            Assert.Null(result.Handler);
        }

        [Theory]
        [InlineData("0987654321")]
        [InlineData("")]
        [InlineData(null)]
        public void WrongAccount_IsWrongAccount_EvenWhenAmountIsZero(string? account)
        {
            var result = NewClassifier().Classify(Transfer(account: account, amount: 0));

            Assert.Equal(TransferClassificationKind.WrongAccount, result.Kind);
        }

        [Fact]
        public void AccountNumber_IgnoresWhitespaceOnBothSides()
        {
            var classifier = NewClassifier(account: "0123 456 789");

            var result = classifier.Classify(Transfer(account: " 01234 56789 "));

            Assert.Equal(TransferClassificationKind.Matched, result.Kind);
        }

        [Fact]
        public void AccountNumberNotConfigured_RejectsEverything()
        {
            var classifier = NewClassifier(account: string.Empty);

            Assert.Equal(TransferClassificationKind.WrongAccount, classifier.Classify(Transfer(account: string.Empty)).Kind);
            Assert.Equal(TransferClassificationKind.WrongAccount, classifier.Classify(Transfer(account: null)).Kind);
            Assert.Equal(TransferClassificationKind.WrongAccount, classifier.Classify(Transfer()).Kind);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5000)]
        public void NonPositiveAmount_IsIgnored(int amount)
        {
            var result = NewClassifier().Classify(Transfer(amount: amount));

            Assert.Equal(TransferClassificationKind.Ignored, result.Kind);
        }

        [Fact]
        public void NoCode_IsUnmatched()
        {
            var result = NewClassifier().Classify(Transfer(content: "chuyen tien tro thang 9"));

            Assert.Equal(TransferClassificationKind.Unmatched, result.Kind);
            Assert.Null(result.Code);
            Assert.Null(result.Handler);
        }

        [Fact]
        public void CodeWithUnknownPrefix_IsUnmatched_ButKeepsTheCode()
        {
            var result = NewClassifier().Classify(Transfer(code: "dh 42"));

            Assert.Equal(TransferClassificationKind.Unmatched, result.Kind);
            Assert.Equal("DH42", result.Code);
        }

        [Fact]
        public void KnownCode_IsMatched_WithTheRightHandlerAndNormalizedCode()
        {
            var handler = new FakeHandler("TTPTHD");
            var classifier = NewClassifier(Account, new FakeHandler("TTPTGC"), handler);

            var result = classifier.Classify(Transfer(content: "ttpt hd 105 tien phong"));

            Assert.Equal(TransferClassificationKind.Matched, result.Kind);
            Assert.Same(handler, result.Handler);
            Assert.Equal("TTPTHD105", result.Code);
        }

        [Fact]
        public void LongerPrefix_IsTriedFirst()
        {
            var shortHandler = new FakeHandler("TTPT");
            var longHandler = new FakeHandler("TTPTHD");
            var classifier = NewClassifier(Account, shortHandler, longHandler);

            var result = classifier.Classify(Transfer(code: "TTPTHD105"));

            Assert.Same(longHandler, result.Handler);
        }

        [Fact]
        public void DuplicatePrefix_IsRejectedAtConstruction()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                NewClassifier(Account, new FakeHandler("TTPTHD"), new FakeHandler("ttpthd")));

            Assert.Contains("TTPTHD", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
