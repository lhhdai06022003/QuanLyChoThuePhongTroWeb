using QuanLyChoThuePhongTroWeb.Application.Common.Files;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

public interface IReservationProofStorage
{
    Task<string> SaveAsync(UploadFile file);
    Task<StoredReservationProof?> ReadAsync(string key);
    Task DeleteAsync(string key);
}

public sealed record StoredReservationProof(byte[] Bytes, string ContentType);
