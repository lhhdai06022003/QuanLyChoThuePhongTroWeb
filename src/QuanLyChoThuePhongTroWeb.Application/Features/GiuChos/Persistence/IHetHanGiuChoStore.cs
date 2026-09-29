namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

public interface IHetHanGiuChoStore
{
    Task<IReadOnlyList<int>> GetCandidatesAsync(DateTime nowUtc);
    Task<bool> TryExpireAsync(int id, DateTime nowUtc);
}
