using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

// Nhân viên không nhập tay mã SEPAY-/VNPAY- (khóa hệ thống tự ghi) khi đối soát tiền giữ chỗ (spec giữ chỗ §6.3, X7).
public sealed class ReservationPaymentServiceSystemGuardTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("SEPAY-ABC")]
    [InlineData(" vnpay-1 ")]
    [InlineData("VNPAY-14123456")]
    public async Task ConfirmReceived_SystemPrefixedReference_IsRejected_WithoutCallingTheStore(string reference)
    {
        var store = new GuardStore { BranchId = 3 };
        var service = CreateService(store);

        var result = await service.ConfirmReceivedAsync(20, 15, null, reference, 200000m, Now.AddHours(-1));

        Assert.False(result.Success);
        Assert.Equal(InvoicePaymentPolicy.SystemPrefixMessage, result.Message);
        Assert.Equal(0, store.BranchCalls);
        Assert.Equal(0, store.ConfirmCalls);
    }

    [Fact]
    public async Task ConfirmReceived_OrdinaryReference_StillReachesTheStore()
    {
        var store = new GuardStore { BranchId = 3, ConfirmResult = PaymentError.None };
        var service = CreateService(store);

        var result = await service.ConfirmReceivedAsync(20, 15, null, "FT26ABC", 200000m, Now.AddHours(-1));

        Assert.True(result.Success);
        Assert.Equal(1, store.ConfirmCalls);
    }

    [Fact]
    public async Task ConfirmReceived_StoreReportsSystemRecorded_ExplainsTheAutomaticRecord()
    {
        var store = new GuardStore { BranchId = 3, ConfirmResult = PaymentError.SystemRecorded };
        var service = CreateService(store);

        var result = await service.ConfirmReceivedAsync(20, 15, null, "ref1", 200000m, Now.AddHours(-1));

        Assert.False(result.Success);
        Assert.Equal("Mã giao dịch này đã được hệ thống ghi nhận tự động.", result.Message);
    }

    private static ReservationPaymentService CreateService(GuardStore store) =>
        new(store, new FakeEmployeeAccessService { ScopeToReturn = new EmployeeAccessScope(20, false, [3]) },
            new FixedTimeProvider(new DateTimeOffset(Now)), new FakeProofStorage(), new InvoicePaymentOptions(),
            new CapturingLogger<ReservationPaymentService>());

    private sealed class GuardStore : IReservationPaymentStore
    {
        public int? BranchId { get; set; }
        public PaymentError ConfirmResult { get; set; } = PaymentError.NotFound;
        public int BranchCalls { get; private set; }
        public int ConfirmCalls { get; private set; }

        public Task<int?> GetPaymentBranchIdAsync(int paymentRequestId, CancellationToken cancellationToken = default)
        {
            BranchCalls++;
            return Task.FromResult(BranchId);
        }

        public Task<PaymentError> ConfirmReceivedAsync(int actorId, int paymentRequestId, int? evidenceId,
            string bankReference, decimal actualAmount, DateTime receivedAtUtc, DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            ConfirmCalls++;
            return Task.FromResult(ConfirmResult);
        }

        public Task<int?> AddEvidenceAsync(int actorId, EvidenceRecord evidence, DateTime nowUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<PaymentEvidenceDto>> ListMineAsync(int actorId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<PaymentEvidenceDto>> ListForReservationAsync(int reservationId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PaymentEvidenceAccess?> GetEvidenceAccessAsync(int evidenceId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<ReservationTransactionAdminDto>> ListTransactionsForReservationAsync(int reservationId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
