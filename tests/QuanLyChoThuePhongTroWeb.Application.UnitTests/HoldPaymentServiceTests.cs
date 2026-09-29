using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public class HoldPaymentServiceTests
{
    [Fact]
    public async Task SubmitProofAsync_DoesNotUploadWhenPaymentIsNotOwnedOrOpen()
    {
        var store = new FakeStore { CanSubmit = false };
        var storage = new FakeStorage();
        var result = await new ThanhToanGiuChoService(store, storage)
            .SubmitProofAsync(4, 2, Request(), Image());

        Assert.False(result.Success);
        Assert.False(storage.Saved);
    }

    [Fact]
    public async Task SubmitProofAsync_DeletesImageIfStateChangesAfterUpload()
    {
        var store = new FakeStore { CanSubmit = true, Saved = false };
        var storage = new FakeStorage();
        var result = await new ThanhToanGiuChoService(store, storage)
            .SubmitProofAsync(4, 2, Request(), Image());

        Assert.False(result.Success);
        Assert.True(storage.Saved);
        Assert.True(storage.Deleted);
    }

    [Fact]
    public async Task ReadProofAsync_DoesNotReadWithoutAuthorizedKey()
    {
        var storage = new FakeStorage();
        var result = await new ThanhToanGiuChoService(new FakeStore(), storage)
            .ReadProofAsync(3, 99);

        Assert.Null(result);
        Assert.False(storage.Read);
    }

    private static SubmitProofRequest Request() =>
        new(1000000m, DateTimeOffset.UtcNow, null, null);
    private static UploadFile Image() =>
        new(new MemoryStream(new byte[] { 1, 2, 3 }), "proof.png", "image/png", 3);

    private sealed class FakeStorage : IReservationProofStorage
    {
        public bool Saved { get; private set; }
        public bool Deleted { get; private set; }
        public bool Read { get; private set; }
        public Task<string> SaveAsync(UploadFile file)
        {
            Saved = true;
            return Task.FromResult("opaque.png");
        }
        public Task<StoredReservationProof?> ReadAsync(string key)
        {
            Read = true;
            return Task.FromResult<StoredReservationProof?>(null);
        }
        public Task DeleteAsync(string key)
        {
            Deleted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStore : IThanhToanGiuChoStore
    {
        public bool CanSubmit { get; set; }
        public bool Saved { get; set; }
        public Task<HoldPaymentDto?> GetForGuestAsync(int reservationId, int actorId) =>
            Task.FromResult<HoldPaymentDto?>(null);
        public Task<bool> CanSubmitAsync(int paymentId, int actorId, DateTime nowUtc) => Task.FromResult(CanSubmit);
        public Task<bool> TrySubmitAsync(int paymentId, int actorId, string key,
            SubmitProofRequest request, DateTime nowUtc) => Task.FromResult(Saved);
        public Task<IReadOnlyList<ProofReviewDto>> GetReviewQueueAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<ProofReviewDto>>(Array.Empty<ProofReviewDto>());
        public Task<string?> GetAuthorizedProofKeyAsync(int proofId, int actorId) => Task.FromResult<string?>(null);
        public Task<bool> ConfirmAsync(int proofId, int actorId, string bankReference,
            decimal actualAmount, DateTime receivedUtc) => Task.FromResult(false);
        public Task<bool> RejectAsync(int proofId, int actorId, string reason,
            decimal actualAmount, string? bankReference, DateTime? receivedUtc) => Task.FromResult(false);
    }
}
