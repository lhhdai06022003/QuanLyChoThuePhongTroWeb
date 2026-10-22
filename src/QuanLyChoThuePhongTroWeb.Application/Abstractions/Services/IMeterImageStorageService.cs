using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public sealed record MeterImageUploadResult(string Url, string PublicId);
    public sealed record MeterImageReadResult(byte[] Bytes, string ContentType);

    public interface IMeterImageStorageService
    {
        Task<MeterImageUploadResult> UploadAsync(UploadFile file, string folder, CancellationToken cancellationToken = default);
        Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default);
        Task DeleteAsync(string publicId, CancellationToken cancellationToken = default);
    }
}
