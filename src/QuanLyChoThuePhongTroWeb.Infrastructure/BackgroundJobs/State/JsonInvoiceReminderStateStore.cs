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
    public class JsonInvoiceReminderStateStore : IInvoiceReminderStateStore
    {
        private readonly string _filePath;
        private readonly ILogger<JsonInvoiceReminderStateStore> _logger;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public JsonInvoiceReminderStateStore(
            IHostEnvironment env,
            ILogger<JsonInvoiceReminderStateStore> logger,
            IConfiguration? configuration = null)
        {
            _filePath = BackgroundJobStatePath.Resolve(env, configuration, "sent_invoice_reminders.json");
            _logger = logger;
        }

        private string GetFilePath()
        {
            return _filePath;
        }

        public async Task<IReadOnlySet<int>> GetSentInvoiceIdsAsync(CancellationToken cancellationToken = default)
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

        public async Task<bool> HasBeenSentAsync(int hoaDonId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var set = LoadInternal(cancellationToken);
                return set.Contains(hoaDonId);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task MarkAsSentAsync(int hoaDonId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(cancellationToken);
            string? tempFilePath = null;
            try
            {
                var filePath = GetFilePath();
                var set = LoadInternal(cancellationToken);
                if (set.Add(hoaDonId))
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
                _logger.LogError(ex, "Lỗi xảy ra khi lưu trạng thái gửi email nhắc nợ vào file JSON.");
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

        private HashSet<int> LoadInternal(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = GetFilePath();
            if (!File.Exists(filePath))
            {
                return new HashSet<int>();
            }

            try
            {
                var json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new HashSet<int>();
                }

                var list = JsonSerializer.Deserialize<List<int>>(json);
                return list != null ? new HashSet<int>(list) : new HashSet<int>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi đọc tệp sent_invoice_reminders.json.");
                throw;
            }
        }
    }
}
