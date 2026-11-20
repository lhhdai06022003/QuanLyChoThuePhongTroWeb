using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases
{
    public class ContractExpiryAlertUseCase : IContractExpiryAlertUseCase
    {
        private readonly IHopDongStore _store;
        private readonly IEmailService _emailService;
        private readonly ContractAlertSettings _settings;
        private readonly IThongBaoService _thongBao;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ContractExpiryAlertUseCase> _logger;

        public ContractExpiryAlertUseCase(
            IHopDongStore store,
            IEmailService emailService,
            ContractAlertSettings settings,
            IThongBaoService thongBao,
            TimeProvider timeProvider,
            ILogger<ContractExpiryAlertUseCase> logger)
        {
            _store = store;
            _emailService = emailService;
            _settings = settings;
            _thongBao = thongBao;
            _timeProvider = timeProvider;
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

            // Không có sổ "đã gửi": job chạy mỗi ngày đúng một lượt (DailyJobSchedule), email chỉ gửi đúng ngày còn
            // AlertDays ngày và thông báo app chỉ gửi đúng ngày hết hạn, nên mỗi mốc tự nhiên chỉ gửi một lần.
            // Đọc hết dữ liệu trước khi gửi: lỗi đọc DB làm cả lượt được thử lại mà chưa gửi gì, không gây gửi trùng.
            var activeContracts = await _store.GetActiveExpiringContractsAsync(cancellationToken);
            var expiredToday = await GetContractsExpiringTodayVnAsync(cancellationToken);

            int sentCount = 0;
            // Lấy giờ qua TimeProvider (không đọc đồng hồ máy) để test cố định được ngày; giữ cách đếm theo ngày UTC như cũ.
            var homNayUtc = _timeProvider.GetUtcNow().UtcDateTime.Date;

            foreach (var contract in activeContracts)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (contract.ThoiDiemKetThuc.HasValue)
                {
                    var daysLeft = (contract.ThoiDiemKetThuc.Value.Date - homNayUtc).Days;

                    if (alertDays.Contains(daysLeft))
                    {
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
                                sentCount++;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý gửi cảnh báo cho hợp đồng {MaHopDong}.", contract.MaHopDong);
                        }
                    }
                }
            }

            sentCount += await SendExpiredAppNoticesAsync(expiredToday, cancellationToken);

            return sentCount;
        }

        // Hợp đồng còn hiệu lực có ngày kết thúc (giờ VN) là hôm nay. Hợp đồng hết hạn từ những ngày trước không báo lại:
        // mục KH4 "Hợp đồng đã quá hạn chưa xử lý" của Việc cần làm giữ lời nhắc tới khi gia hạn hoặc thanh lý.
        private async Task<IReadOnlyList<HopDong>> GetContractsExpiringTodayVnAsync(CancellationToken cancellationToken)
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var homNayVnBatDauUtc = DateTime.SpecifyKind(nowUtc.AddHours(7).Date, DateTimeKind.Utc).AddHours(-7);
            var ngayMaiVnBatDauUtc = homNayVnBatDauUtc.AddDays(1);

            var expired = await _store.GetExpiredActiveContractsAsync(ngayMaiVnBatDauUtc, cancellationToken);
            return expired
                .Where(hd => hd.ThoiDiemKetThuc.HasValue && hd.ThoiDiemKetThuc.Value >= homNayVnBatDauUtc)
                .ToList();
        }

        private async Task<int> SendExpiredAppNoticesAsync(IReadOnlyList<HopDong> expired, CancellationToken cancellationToken)
        {
            var count = 0;

            foreach (var hd in expired)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var ngayHetHan = hd.ThoiDiemKetThuc!.Value.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                    var tieuDe = $"Hợp đồng {hd.MaHopDong} đã hết hạn";
                    var noiDung = $"Phòng {hd.PhongTro?.SoPhong} - {hd.PhongTro?.ChiNhanh?.TenChiNhanh}, người đại diện {hd.NguoiThue?.HoVaTen}, hết hạn ngày {ngayHetHan}. Hãy gia hạn hoặc thanh lý hợp đồng.";

                    var result = await _thongBao.GuiChoNguoiPhuTrachPhongAsync(
                        hd.PhongTroId, tieuDe, noiDung, "HopDong", $"/QuanLyNhaTro/HopDong/ChiTiet/{hd.HopDongId}", cancellationToken);

                    if (result.Success)
                    {
                        count++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi gửi thông báo hết hạn trong app cho hợp đồng {MaHopDong}.", hd.MaHopDong);
                }
            }

            return count;
        }
    }
}
