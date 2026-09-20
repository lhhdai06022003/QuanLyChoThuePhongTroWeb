using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases
{
    public class ContractExpiryAlertUseCase : IContractExpiryAlertUseCase
    {
        private readonly IHopDongStore _store;
        private readonly IEmailService _emailService;
        private readonly ContractAlertSettings _settings;
        private readonly IContractExpiryAlertStateStore _stateStore;
        private readonly ILogger<ContractExpiryAlertUseCase> _logger;

        public ContractExpiryAlertUseCase(
            IHopDongStore store,
            IEmailService emailService,
            ContractAlertSettings settings,
            IContractExpiryAlertStateStore stateStore,
            ILogger<ContractExpiryAlertUseCase> logger)
        {
            _store = store;
            _emailService = emailService;
            _settings = settings;
            _stateStore = stateStore;
            _logger = logger;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings.Enabled)
            {
                return 0;
            }

            var alertDays = _settings.AlertDays ?? new List<int> { 30, 15 };
            var adminEmail = _settings.AdminEmail;

            var sentAlerts = await _stateStore.GetSentAlertKeysAsync(cancellationToken);

            var activeContracts = await _store.GetActiveExpiringContractsAsync(cancellationToken);

            int sentCount = 0;

            foreach (var contract in activeContracts)
            {
                if (cancellationToken.IsCancellationRequested)
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

                        try
                        {
                            if (!string.IsNullOrWhiteSpace(tenantEmail))
                            {
                                var tenantResult = await _emailService.SendContractExpiryAlertAsync(tenantEmail, contract.NguoiThue?.HoVaTen ?? "Khách thuê", alertData);
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
                                var adminResult = await _emailService.SendContractExpiryAlertAsync(adminEmail, "Ban Quản Lý", alertData);
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
                                await _stateStore.MarkAsSentAsync(alertKey, cancellationToken);
                                sentCount++;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý gửi cảnh báo hoặc lưu trạng thái cho hợp đồng {MaHopDong}.", contract.MaHopDong);
                        }
                    }
                }
            }

            return sentCount;
        }
    }
}
