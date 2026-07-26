using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Cloudinary
{
    public interface ICloudinaryStorageService
    {
        Task<string> UploadImageAsync(IFormFile file, string folderName);
    }
}
