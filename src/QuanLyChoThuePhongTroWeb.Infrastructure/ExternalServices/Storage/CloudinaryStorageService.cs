using System;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage
{
    public class CloudinaryStorageService : IImageStorageService
    {
        private readonly CloudinaryDotNet.Cloudinary? _cloudinary;

        public CloudinaryStorageService(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
            {
                var account = new Account(cloudName, apiKey, apiSecret);
                _cloudinary = new CloudinaryDotNet.Cloudinary(account);
                _cloudinary.Api.Secure = true;
            }
        }

        public async Task<string> UploadImageAsync(UploadFile file, string folderName)
        {
            if (file == null || file.Length == 0) return string.Empty;
            if (_cloudinary == null) throw new InvalidOperationException("Cloudinary credentials are not configured in appsettings.json");

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, file.Content),
                Folder = folderName,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new InvalidOperationException("Không thể tải ảnh lên. Vui lòng thử lại sau.");
            }

            return uploadResult.SecureUrl.ToString();
        }
    }
}
