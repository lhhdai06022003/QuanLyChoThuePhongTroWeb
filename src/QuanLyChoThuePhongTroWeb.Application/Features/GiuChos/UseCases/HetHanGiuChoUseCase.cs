using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.UseCases;

public sealed class HetHanGiuChoUseCase(IHetHanGiuChoStore store) : IHetHanGiuChoUseCase
{
    public async Task<int> ExecuteAsync(DateTime nowUtc)
    {
        var ids = await store.GetCandidatesAsync(nowUtc);
        var count = 0;
        foreach (var id in ids)
            if (await store.TryExpireAsync(id, nowUtc)) count++;
        return count;
    }
}
