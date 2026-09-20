using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public interface IImageStorageService
    {
        Task<string> UploadImageAsync(UploadFile file, string folderName);
    }
}
