using System;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Hai bộ xử lý (hóa đơn TTPTHD, giữ chỗ TTPTGC) cùng đăng ký trong bộ phân loại.
    public class IncomingTransferClassifierReservationTests
    {
        private const string Account = "0123456789";

        private sealed class FakeHandler(string prefix) : IIncomingTransferHandler
        {
            public string Prefix { get; } = prefix;

            public Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, string code, CancellationToken ct = default)
                => throw new NotSupportedException("Bộ phân loại không được gọi bộ xử lý.");
        }

        private static IncomingBankTransfer Transfer(string content) => new(
            92704, "MBBank", new DateTime(2026, 10, 16, 3, 0, 0, DateTimeKind.Utc), Account, null, null,
            content, "in", 1_000_000m, "FT26ABC", null);

        private static IncomingTransferClassifier NewClassifier() => new(
            new IIncomingTransferHandler[] { new FakeHandler("TTPTHD"), new FakeHandler("TTPTGC") },
            new VietQrSettings { AccountNumber = Account });

        [Fact]
        public void ReservationCode_GoesToTheReservationHandler()
        {
            var result = NewClassifier().Classify(Transfer("TTPTGC5"));

            Assert.Equal(TransferClassificationKind.Matched, result.Kind);
            Assert.Equal("TTPTGC", result.Handler!.Prefix);
            Assert.Equal("TTPTGC5", result.Code);
        }

        [Fact]
        public void InvoiceCode_StillGoesToTheInvoiceHandler()
        {
            var result = NewClassifier().Classify(Transfer("TTPTHD5"));

            Assert.Equal(TransferClassificationKind.Matched, result.Kind);
            Assert.Equal("TTPTHD", result.Handler!.Prefix);
            Assert.Equal("TTPTHD5", result.Code);
        }

        [Fact]
        public void RealReservationAndInvoiceHandlers_DoNotConflict()
        {
            var ex = Record.Exception(() => new IncomingTransferClassifier(
                new IIncomingTransferHandler[] { new FakeHandler(InvoiceTransferHandler.InvoicePrefix), new FakeHandler(ReservationTransferHandler.ReservationPrefix) },
                new VietQrSettings { AccountNumber = Account }));

            Assert.Null(ex);
        }
    }
}
