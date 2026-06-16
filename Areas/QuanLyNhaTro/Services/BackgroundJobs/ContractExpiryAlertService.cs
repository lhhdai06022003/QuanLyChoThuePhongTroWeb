using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Emails;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.BackgroundJobs
{
    public class ContractExpiryAlertService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ContractExpiryAlertService> _logger;
        private readonly IHostEnvironment _env;
        private readonly object _fileLock = new object();

        public ContractExpiryAlertService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<ContractExpiryAlertService> logger,
            IHostEnvironment env)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
            _env = env;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ tự động cảnh báo hợp đồng sắp hết hạn (ContractExpiryAlertService) đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiryAlertsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét và gửi cảnh báo hợp đồng sắp hết hạn.");
                }

                var now = DateTime.UtcNow;
                var vnNow = now.AddHours(7);
                var vnNextEightAM = vnNow.Date.AddHours(8);
                if (vnNow >= vnNextEightAM)
                {
                    vnNextEightAM = vnNextEightAM.AddDays(1);
                }
                var timeToWait = vnNextEightAM - vnNow;
                if (timeToWait <= TimeSpan.Zero)
                {
                    timeToWait = TimeSpan.FromDays(1);
                }

                _logger.LogInformation("ContractExpiryAlertService sẽ chạy lại sau: {WaitTime}", timeToWait);
                await Task.Delay(timeToWait, stoppingToken);
            }

            _logger.LogInformation("Dịch vụ tự động cảnh báo hợp đồng sắp hết hạn (ContractExpiryAlertService) đang dừng.");
        }

        private async Task ProcessExpiryAlertsAsync(CancellationToken stoppingToken)
        {
            var enabled = _configuration.GetValue<bool>("ContractAlertSettings:Enabled", true);
            if (!enabled)
            {
                return;
            }

            var alertDays = _configuration.GetSection("ContractAlertSettings:AlertDays").Get<List<int>>() ?? new List<int> { 30, 15 };
            var adminEmail = _configuration["ContractAlertSettings:AdminEmail"];

            var sentAlerts = LoadSentAlerts();

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var activeContracts = await dbContext.HopDongs
                    .Include(hd => hd.NguoiThue)
                    .Include(hd => hd.PhongTro)
                        .ThenInclude(p => p.ChiNhanh)
                    .Where(hd => hd.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong
                        && !hd.IsDeleted
                        && hd.ThoiDiemKetThuc != null)
                    .ToListAsync(stoppingToken);

                foreach (var contract in activeContracts)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (contract.ThoiDiemKetThuc.HasValue)
                    {
                        var daysLeft = (contract.ThoiDiemKetThuc.Value.Date - DateTime.UtcNow.Date).Days;

                        if (alertDays.Contains(daysLeft))
                        {
                            var alertKey = $"{contract.HopDongId}_{daysLeft}";

                            if (sentAlerts.Contains(alertKey))
                            {
                                continue;
                            }

                            var tenantEmail = contract.NguoiThue?.Email;
                            var alertData = new ContractExpiryAlertData
                            {
                                MaHopDong = contract.MaHopDong,
                                SoPhong = contract.PhongTro?.SoPhong ?? "",
                                TenChiNhanh = contract.PhongTro?.ChiNhanh?.TenChiNhanh ?? "",
                                DiaChiChiNhanh = contract.PhongTro?.ChiNhanh?.DiaChi ?? "",
                                SoDienThoaiChiNhanh = contract.PhongTro?.ChiNhanh?.SoDienThoai ?? "",
                                TenNguoiThue = contract.NguoiThue?.HoVaTen ?? "",
                                ThoiDiemKetThuc = contract.ThoiDiemKetThuc.Value,
                                SoNgayConLai = daysLeft,
                                TienThuePhong = contract.TienThuePhong
                            };

                            bool sentSuccessfully = false;

                            if (!string.IsNullOrWhiteSpace(tenantEmail))
                            {
                                var tenantResult = await emailService.SendContractExpiryAlertAsync(tenantEmail, contract.NguoiThue?.HoVaTen ?? "Khách thuê", alertData);
                                if (tenantResult.IsSuccess)
                                {
                                    _logger.LogInformation("Gửi email cảnh báo hết hạn hợp đồng cho người thuê {TenantEmail} thành công.", tenantEmail);
                                    sentSuccessfully = true;
                                }
                                else
                                {
                                    _logger.LogError("Gửi email cảnh báo hết hạn hợp đồng cho người thuê {TenantEmail} thất bại: {Error}", tenantEmail, tenantResult.ErrorMessage);
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(adminEmail))
                            {
                                var adminResult = await emailService.SendContractExpiryAlertAsync(adminEmail, "Ban Quản Lý", alertData);
                                if (adminResult.IsSuccess)
                                {
                                    _logger.LogInformation("Gửi email cảnh báo hết hạn hợp đồng cho Admin {AdminEmail} thành công.", adminEmail);
                                    sentSuccessfully = true;
                                }
                                else
                                {
                                    _logger.LogError("Gửi email cảnh báo hết hạn hợp đồng cho Admin {AdminEmail} thất bại: {Error}", adminEmail, adminResult.ErrorMessage);
                                }
                            }

                            if (sentSuccessfully)
                            {
                                SaveSentAlert(alertKey);
                            }
                        }
                    }
                }
            }
        }

        private string GetSentAlertsFilePath()
        {
            return Path.Combine(_env.ContentRootPath, "sent_contract_alerts.json");
        }

        private HashSet<string> LoadSentAlerts()
        {
            lock (_fileLock)
            {
                var filePath = GetSentAlertsFilePath();
                if (!File.Exists(filePath))
                {
                    return new HashSet<string>();
                }

                try
                {
                    var json = File.ReadAllText(filePath);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    return list != null ? new HashSet<string>(list) : new HashSet<string>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra khi đọc tệp sent_contract_alerts.json. Khởi tạo lại danh sách rỗng.");
                    return new HashSet<string>();
                }
            }
        }

        private void SaveSentAlert(string alertKey)
        {
            lock (_fileLock)
            {
                var filePath = GetSentAlertsFilePath();
                var sentAlerts = LoadSentAlerts();
                if (sentAlerts.Add(alertKey))
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(sentAlerts.ToList(), new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(filePath, json);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi xảy ra khi lưu tệp sent_contract_alerts.json.");
                    }
                }
            }
        }
    }
}
