using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.UseCases;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class ReservationContractServiceTests
{
    [Fact]
    public async Task ReversedDatesDoNotCallStore()
    {
        var store = new FakeStore();
        var service = new ChuyenHopDongTuGiuChoUseCase(store);
        var now = DateTimeOffset.UtcNow;
        var result = await service.ExecuteAsync(1, new(now, now.AddDays(-1), []), 3);
        Assert.False(result.Success);
        Assert.False(store.Called);
    }

    [Fact]
    public async Task StoreConflictDoesNotReportContractCreated()
    {
        var store = new FakeStore { Result = null };
        var result = await new ChuyenHopDongTuGiuChoUseCase(store).ExecuteAsync(1,
            new(DateTimeOffset.UtcNow, null, []), 3);
        Assert.False(result.Success);
        Assert.Null(result.HopDongId);
    }

    [Fact]
    public async Task SuccessfulConversionReturnsPersistedContractId()
    {
        var result = await new ChuyenHopDongTuGiuChoUseCase(new FakeStore { Result = 51 })
            .ExecuteAsync(1, new(DateTimeOffset.UtcNow, null, [2]), 3);
        Assert.True(result.Success);
        Assert.Equal(51, result.HopDongId);
    }

    private sealed class FakeStore : IChuyenHopDongGiuChoStore
    {
        public bool Called { get; private set; }
        public int? Result { get; set; }
        public Task<ContractCandidateDto?> GetCandidateAsync(int reservationId, int actorId) =>
            Task.FromResult<ContractCandidateDto?>(null);
        public Task<int?> ConvertAsync(int reservationId, ReservationContractRequest request, int actorId)
        {
            Called = true;
            return Task.FromResult(Result);
        }
    }
}
