using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Điểm vào chung của webhook/đồng bộ bù: phân loại rồi giao cho bộ xử lý (spec thanh toán trực tuyến §4.2, §9.1).
    public class BankTransferIngestionServiceTests
    {
        private const string Account = "0123456789";

        private sealed class RecordingHandler : IIncomingTransferHandler
        {
            private readonly Func<IncomingBankTransfer, string, TransferOutcome> _result;
            public RecordingHandler(Func<IncomingBankTransfer, string, TransferOutcome> result) => _result = result;

            public string Prefix => "TTPTHD";
            public List<(IncomingBankTransfer Transfer, string Code)> Calls { get; } = new();

            public Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, string code, CancellationToken ct = default)
            {
                Calls.Add((transfer, code));
                return Task.FromResult(_result(transfer, code));
            }
        }

        private static IncomingBankTransfer Transfer(
            string type = "in", string? account = Account, decimal amount = 2_000_000m, string? content = "Nguyen Van A TTPTHD105")
            => new(92704, "MBBank", new DateTime(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc), account, null, null, content, type, amount, "FT26ABC", null);

        private static (BankTransferIngestionService Service, RecordingHandler Handler, CapturingLogger<BankTransferIngestionService> Log) Create(
            TransferOutcomeKind handlerKind = TransferOutcomeKind.Recorded)
        {
            var handler = new RecordingHandler((t, c) => new TransferOutcome { Kind = handlerKind, Transfer = t, Code = c, HoaDonId = 105, LyDo = handlerKind == TransferOutcomeKind.Failed ? "Đã có lỗi hệ thống, vui lòng thử lại." : null });
            var classifier = new IncomingTransferClassifier(new IIncomingTransferHandler[] { handler }, new VietQrSettings { AccountNumber = Account });
            var log = new CapturingLogger<BankTransferIngestionService>();
            return (new BankTransferIngestionService(classifier, log), handler, log);
        }

        [Fact]
        public async Task MatchedTransfer_IsHandedToTheHandler_WithTheNormalizedCode()
        {
            var (service, handler, _) = Create();
            var transfer = Transfer();

            var outcome = await service.IngestAsync(transfer);

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            var call = Assert.Single(handler.Calls);
            Assert.Same(transfer, call.Transfer);
            Assert.Equal("TTPTHD105", call.Code);
            Assert.Equal(105, outcome.HoaDonId);
        }

        [Theory]
        [InlineData("out", Account, 2_000_000, "TTPTHD105", TransferOutcomeKind.Ignored)]
        [InlineData("in", Account, 0, "TTPTHD105", TransferOutcomeKind.Ignored)]
        [InlineData("in", "9999999999", 2_000_000, "TTPTHD105", TransferOutcomeKind.WrongAccount)]
        [InlineData("in", Account, 2_000_000, "chuyen tien tro", TransferOutcomeKind.Unmatched)]
        [InlineData("in", Account, 2_000_000, "TTPTHD1234567890", TransferOutcomeKind.Unmatched)]
        [InlineData("in", Account, 2_000_000, "DH42 tien phong", TransferOutcomeKind.Unmatched)]
        public async Task NotMatched_ReturnsTheSameKind_WithoutCallingTheHandler(string type, string account, int amount, string content, TransferOutcomeKind expected)
        {
            var (service, handler, _) = Create();

            var outcome = await service.IngestAsync(Transfer(type, account, amount, content));

            Assert.Equal(expected, outcome.Kind);
            Assert.Empty(handler.Calls);
            Assert.Null(outcome.HoaDonId);
            Assert.Equal(0m, outcome.SoTienGhiNhan);
            Assert.Null(outcome.LichSuThanhToanId);
        }

        [Fact]
        public async Task Outcome_CarriesTheTransferAndDedupKey()
        {
            var (service, _, _) = Create();

            var outcome = await service.IngestAsync(Transfer(type: "out"));

            Assert.Equal("SEPAY-FT26ABC", outcome.DedupKey);
            Assert.Equal(92704, outcome.Transfer.SePayId);
        }

        [Fact]
        public async Task UnmatchedWithForeignPrefix_KeepsTheExtractedCode()
        {
            var (service, _, _) = Create();

            var foreign = Transfer(content: "tien phong") with { Code = "dh 42" };

            var outcome = await service.IngestAsync(foreign);

            Assert.Equal(TransferOutcomeKind.Unmatched, outcome.Kind);
            Assert.Equal("DH42", outcome.Code);
        }

        [Theory]
        [InlineData(TransferOutcomeKind.Recorded)]
        [InlineData(TransferOutcomeKind.Duplicate)]
        [InlineData(TransferOutcomeKind.InvoiceNotPayable)]
        [InlineData(TransferOutcomeKind.AlreadyPaid)]
        public async Task HandlerOutcomes_AreReturnedAsIs_AndLoggedAsInformation(TransferOutcomeKind kind)
        {
            var (service, _, log) = Create(kind);

            var outcome = await service.IngestAsync(Transfer());

            Assert.Equal(kind, outcome.Kind);
            var entry = Assert.Single(log.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Contains(kind.ToString(), entry.Message);
        }

        [Fact]
        public async Task FailedOutcome_IsLoggedAsWarning_WithTheReason()
        {
            var (service, _, log) = Create(TransferOutcomeKind.Failed);

            var outcome = await service.IngestAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Failed, outcome.Kind);
            var entry = Assert.Single(log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("Đã có lỗi hệ thống", entry.Message);
        }

        [Fact]
        public async Task Logs_NeverContainTheBankTransferText()
        {
            var (service, _, log) = Create();

            await service.IngestAsync(Transfer(content: "BI-MAT-NOI-DUNG TTPTHD105"));
            await service.IngestAsync(Transfer(content: "BI-MAT-NOI-DUNG khong ma"));
            await service.IngestAsync(Transfer(type: "out", content: "BI-MAT-NOI-DUNG"));

            Assert.Equal(3, log.Entries.Count);
            Assert.All(log.Entries, e => Assert.DoesNotContain("BI-MAT-NOI-DUNG", e.Message));
        }

        [Fact]
        public async Task HandlerException_IsNotSwallowed()
        {
            var handler = new RecordingHandler((_, _) => throw new InvalidOperationException("boom"));
            var classifier = new IncomingTransferClassifier(new IIncomingTransferHandler[] { handler }, new VietQrSettings { AccountNumber = Account });
            var service = new BankTransferIngestionService(classifier, new CapturingLogger<BankTransferIngestionService>());

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.IngestAsync(Transfer()));
        }
    }
}
