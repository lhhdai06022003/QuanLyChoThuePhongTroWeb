using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.BackgroundJobs
{
    public class ContractAutoCloseService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ContractAutoCloseService> _logger;

        public ContractAutoCloseService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<ContractAutoCloseService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ tự động đóng hợp đồng hết hạn (ContractAutoCloseService) đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAutoCloseAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình tự động đóng hợp đồng.");
                }

                var now = DateTime.UtcNow;
                var vnNow = now.AddHours(7);
                var vnNextMidnight = vnNow.Date.AddDays(1);
                var timeToWait = vnNextMidnight - vnNow;
                if (timeToWait <= TimeSpan.Zero)
                {
                    timeToWait = TimeSpan.FromDays(1);
                }

                _logger.LogInformation("ContractAutoCloseService sẽ chạy lại sau: {WaitTime}", timeToWait);
                await Task.Delay(timeToWait, stoppingToken);
            }

            _logger.LogInformation("Dịch vụ tự động đóng hợp đồng hết hạn (ContractAutoCloseService) đang dừng.");
        }

        private async Task ProcessAutoCloseAsync(CancellationToken stoppingToken)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var expiredContracts = await dbContext.HopDongs
                    .Include(hd => hd.PhongTro)
                    .Include(hd => hd.ChiTietThanhVienHopDongs)
                    .Where(hd => hd.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong
                        && !hd.IsDeleted
                        && hd.ThoiDiemKetThuc != null
                        && hd.ThoiDiemKetThuc < DateTime.UtcNow)
                    .ToListAsync(stoppingToken);

                if (expiredContracts.Count == 0)
                {
                    return;
                }

                _logger.LogInformation("Tìm thấy {Count} hợp đồng đã quá hạn cần đóng tự động.", expiredContracts.Count);

                using (var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken))
                {
                    try
                    {
                        foreach (var contract in expiredContracts)
                        {
                            contract.TrangThaiHopDong = TrangThaiHopDong.DaKetThuc;
                            contract.NgayCapNhat = DateTime.UtcNow;

                            if (contract.PhongTro != null)
                            {
                                contract.PhongTro.TrangThai = TrangThaiPhong.Trong;
                                contract.PhongTro.NgayCapNhat = DateTime.UtcNow;
                            }

                            if (contract.ChiTietThanhVienHopDongs != null)
                            {
                                var activeMembers = contract.ChiTietThanhVienHopDongs
                                    .Where(x => x.NgayChuyenDi == null && !x.IsDeleted)
                                    .ToList();

                                foreach (var member in activeMembers)
                                {
                                    member.NgayChuyenDi = DateTime.UtcNow;
                                }
                            }
                        }

                        await dbContext.SaveChangesAsync(stoppingToken);
                        await transaction.CommitAsync(stoppingToken);

                        _logger.LogInformation("Đóng tự động {Count} hợp đồng hết hạn thành công.", expiredContracts.Count);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync(stoppingToken);
                        _logger.LogError(ex, "Lỗi xảy ra trong quá trình thực hiện đóng hợp đồng trong Transaction.");
                        throw;
                    }
                }
            }
        }
    }
}
