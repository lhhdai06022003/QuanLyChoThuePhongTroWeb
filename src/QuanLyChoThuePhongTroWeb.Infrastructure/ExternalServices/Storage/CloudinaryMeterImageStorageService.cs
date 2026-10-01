using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage
{
    public class CloudinaryMeterImageStorageService : IMeterImageStorageService
    {
        private readonly CloudinaryDotNet.Cloudinary? _cloudinary;
        private readonly HttpClient _httpClient;
        private readonly MeterImageOptions _options;
        private readonly ILogger<CloudinaryMeterImageStorageService> _logger;
        private readonly string _cloudName;

        public CloudinaryMeterImageStorageService(
            IConfiguration configuration,
            HttpClient httpClient,
            MeterImageOptions options,
            ILogger<CloudinaryMeterImageStorageService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _cloudName = configuration["Cloudinary:CloudName"] ?? string.Empty;
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            if (!string.IsNullOrEmpty(_cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
            {
                var account = new Account(_cloudName, apiKey, apiSecret);
                _cloudinary = new CloudinaryDotNet.Cloudinary(account);
                _cloudinary.Api.Secure = true;
            }
        }

        public async Task<MeterImageUploadResult> UploadAsync(UploadFile file, string folder, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("Tệp tải lên không hợp lệ.", nameof(file));
            }

            if (_cloudinary == null)
            {
                throw new InvalidOperationException("Cloudinary credentials are not configured in appsettings.json");
            }

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, file.Content),
                Folder = folder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);

            if (uploadResult.Error != null)
            {
                throw new InvalidOperationException("Không thể tải ảnh lên dịch vụ lưu trữ đám mây.");
            }

            return new MeterImageUploadResult(uploadResult.SecureUrl.ToString(), uploadResult.PublicId);
        }

        public async Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                if (!string.IsNullOrWhiteSpace(publicId) && _cloudinary != null)
                {
                    url = _cloudinary.Api.UrlImgUp.BuildUrl(publicId);
                }
                else
                {
                    throw new ArgumentException("URL ảnh không được để trống.", nameof(url));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUri) ||
                parsedUri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(parsedUri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(_cloudName) ||
                !parsedUri.AbsolutePath.StartsWith("/" + _cloudName + "/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("URL ảnh không an toàn hoặc không hợp lệ.");
            }

            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Không thể tải ảnh từ dịch vụ lưu trữ (HTTP {(int)response.StatusCode}).");
                }

                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > _options.MaxFileSizeBytes)
                {
                    throw new InvalidOperationException($"Kích thước tệp ảnh tải về vượt quá giới hạn tối đa {_options.MaxFileSizeBytes / 1024 / 1024}MB.");
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
                if (!string.IsNullOrEmpty(mediaType) && !_options.AllowedContentTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Định dạng tệp ảnh tải về không được hỗ trợ.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var ms = new MemoryStream();
                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    totalRead += bytesRead;
                    if (totalRead > _options.MaxFileSizeBytes)
                    {
                        throw new InvalidOperationException($"Dung lượng tệp ảnh tải về vượt quá giới hạn {_options.MaxFileSizeBytes / 1024 / 1024}MB.");
                    }
                    ms.Write(buffer, 0, bytesRead);
                }

                var contentType = !string.IsNullOrEmpty(mediaType) ? mediaType : "image/jpeg";
                return new MeterImageReadResult(ms.ToArray(), contentType);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("Hết thời gian chờ khi tải ảnh từ dịch vụ lưu trữ.");
            }
            catch (HttpRequestException)
            {
                throw new InvalidOperationException("Lỗi kết nối khi tải ảnh chỉ số.");
            }
        }

        public async Task DeleteAsync(string publicId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(publicId) || _cloudinary == null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var deleteParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deleteParams);
                var resStatus = result.Result?.ToLowerInvariant();
                if (resStatus != "ok" && resStatus != "not found")
                {
                    _logger.LogWarning("Xóa ảnh Cloudinary {PublicId} không thành công. Kết quả: {Result}", publicId, result.Result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi kết nối khi xóa ảnh Cloudinary {PublicId}", publicId);
                throw;
            }
        }
    }
}
