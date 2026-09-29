namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.UseCases;

public interface IHetHanGiuChoUseCase
{
    Task<int> ExecuteAsync(DateTime nowUtc);
}
