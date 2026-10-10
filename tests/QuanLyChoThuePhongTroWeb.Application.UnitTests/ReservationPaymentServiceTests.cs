using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class ReservationPaymentServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SubmitEvidence_UploadsToCloudStorageAndStoresUrl()
    {
        var store = new FakeStore { NextEvidenceId = 41 };
        var storage = new FakeProofStorage();
        var service = CreateService(store, storage);

        var result = await service.SubmitEvidenceAsync(7, Submission(PaymentTestHarness.JpegFile()));

        Assert.True(result.Success);
        Assert.Equal(41, result.Data);
        var upload = Assert.Single(storage.Uploads);
        Assert.Equal(("giu-cho-15.jpg", "hold-payment-proofs"), upload);
        var saved = Assert.Single(store.Added);
        Assert.StartsWith("https://res.cloudinary.com/", saved.ImageUrl);
        Assert.Equal("hold-payment-proofs/giu-cho-15_abc", saved.ImagePublicId);
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task SubmitEvidence_InvalidImage_DoesNotUpload()
    {
        var store = new FakeStore();
        var storage = new FakeProofStorage();
        var service = CreateService(store, storage);
        var bytes = "not an image"u8.ToArray();
        var fake = new UploadFile(new MemoryStream(bytes), "bienlai.jpg", "image/jpeg", bytes.Length);

        var result = await service.SubmitEvidenceAsync(7, Submission(fake));

        Assert.False(result.Success);
        Assert.Empty(storage.Uploads);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task SubmitEvidence_RequestNotOpen_DeletesUploadedImage()
    {
        var store = new FakeStore { NextEvidenceId = null };
        var storage = new FakeProofStorage();
        var service = CreateService(store, storage);

        var result = await service.SubmitEvidenceAsync(7, Submission(PaymentTestHarness.JpegFile()));

        Assert.False(result.Success);
        Assert.Equal(["hold-payment-proofs/giu-cho-15_abc"], storage.Deleted);
    }

    [Fact]
    public async Task SubmitEvidence_UploadFails_ReturnsErrorWithoutWriting()
    {
        var store = new FakeStore();
        var storage = new FakeProofStorage { ThrowOnUpload = true };
        var service = CreateService(store, storage);

        var result = await service.SubmitEvidenceAsync(7, Submission(PaymentTestHarness.JpegFile()));

        Assert.False(result.Success);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task GetEvidenceImage_OwnerGuest_ReadsFromStorage()
    {
        var store = new FakeStore { Access = Access(guestUserId: 7, branchId: 3) };
        var service = CreateService(store, new FakeProofStorage());

        var result = await service.GetEvidenceImageAsync(7, 41);

        Assert.True(result.Success);
        Assert.Equal("image/jpeg", result.Data!.ContentType);
    }

    [Fact]
    public async Task GetEvidenceImage_EmployeeOutsideBranch_IsNotFound()
    {
        var store = new FakeStore { Access = Access(guestUserId: 7, branchId: 3) };
        var access = new FakeEmployeeAccessService
        {
            ScopeToReturn = new EmployeeAccessScope(20, false, [4])
        };
        var service = CreateService(store, new FakeProofStorage(), access);

        var result = await service.GetEvidenceImageAsync(20, 41);

        Assert.False(result.Success);
    }

    private static EvidenceSubmission Submission(UploadFile image) =>
        new(15, image, 200000m, Now.AddHours(-1), "BANK-1", null);

    private static PaymentEvidenceAccess Access(int guestUserId, int branchId) =>
        new(41, guestUserId, branchId,
            "https://res.cloudinary.com/demo/hold-payment-proofs/a.jpg", "hold-payment-proofs/a");

    private static ReservationPaymentService CreateService(FakeStore store,
        FakeProofStorage storage, FakeEmployeeAccessService? access = null) =>
        new(store, access ?? new FakeEmployeeAccessService(),
            new FixedTimeProvider(new DateTimeOffset(Now)), storage,
            new InvoicePaymentOptions(),
            new CapturingLogger<ReservationPaymentService>());

    private sealed class FakeStore : IReservationPaymentStore
    {
        public int? NextEvidenceId { get; set; } = 1;
        public PaymentEvidenceAccess? Access { get; set; }
        public List<EvidenceRecord> Added { get; } = [];

        public Task<int?> AddEvidenceAsync(int actorId, EvidenceRecord evidence,
            DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            Added.Add(evidence);
            return Task.FromResult(NextEvidenceId);
        }

        public Task<PaymentEvidenceAccess?> GetEvidenceAccessAsync(int evidenceId,
            CancellationToken cancellationToken = default) => Task.FromResult(Access);

        public Task<IReadOnlyList<PaymentEvidenceDto>> ListMineAsync(int actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentEvidenceDto>>([]);

        public Task<IReadOnlyList<PaymentEvidenceDto>> ListForReservationAsync(int reservationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentEvidenceDto>>([]);

        public Task<int?> GetPaymentBranchIdAsync(int paymentRequestId,
            CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

        public Task<PaymentError> ConfirmReceivedAsync(int actorId, int paymentRequestId,
            int? evidenceId, string bankReference, decimal actualAmount,
            DateTime receivedAtUtc, DateTime nowUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PaymentError.NotFound);
    }
}
