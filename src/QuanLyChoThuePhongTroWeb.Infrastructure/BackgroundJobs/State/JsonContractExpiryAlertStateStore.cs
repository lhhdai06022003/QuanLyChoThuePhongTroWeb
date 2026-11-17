using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs.State
{
    public class JsonContractExpiryAlertStateStore : IContractExpiryAlertStateStore
    {
        private readonly string _filePath;
        private readonly ILogger<JsonContractExpiryAlertStateStore> _logger;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public JsonContractExpiryAlertStateStore(
            IHostEnvironment env,
            ILogger<JsonContractExpiryAlertStateStore> logger,
            IConfiguration? configuration = null)
        {
            _filePath = BackgroundJobStatePath.Resolve(env, configuration, "sent_contract_alerts.json");
            _logger = logger;
        }

        private string GetFilePath()
        {
            return _filePath;
        }

        public async Task<IReadOnlySet<string>> GetSentAlertKeysAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                return LoadInternal(cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> HasBeenSentAsync(string alertKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var set = LoadInternal(cancellationToken);
                return set.Contains(alertKey);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task MarkAsSentAsync(string alertKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(cancellationToken);
            string? tempFilePath = null;
            try
            {
                var filePath = GetFilePath();
                var set = LoadInternal(cancellationToken);
                if (set.Add(alertKey))
                {
                    var json = JsonSerializer.Serialize(set.ToList(), new JsonSerializerOptions { WriteIndented = true });
                    tempFilePath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    await File.WriteAllTextAsync(tempFilePath, json, cancellationToken);
                    File.Move(tempFilePath, filePath, overwrite: true);
                    tempFilePath = null;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi lưu trạng thái gửi cảnh báo hết hạn hợp đồng vào file JSON.");
                throw;
            }
            finally
            {
                if (tempFilePath != null && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Không thể dọn dẹp file tạm {TempFilePath}", tempFilePath);
                    }
                }
                _semaphore.Release();
            }
        }

        private HashSet<string> LoadInternal(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = GetFilePath();
            if (!File.Exists(filePath))
            {
                return new HashSet<string>();
            }

            try
            {
                var json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new HashSet<string>();
                }

                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list != null ? new HashSet<string>(list) : new HashSet<string>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi đọc tệp sent_contract_alerts.json.");
                throw;
            }
        }
    }
}
