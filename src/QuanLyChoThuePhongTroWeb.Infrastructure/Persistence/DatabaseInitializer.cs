using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence
{
    public interface IDatabaseInitializer
    {
        Task InitializeAsync();
    }

    public class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly INguoiDungService _nguoiDungService;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(
            ApplicationDbContext context,
            INguoiDungService nguoiDungService,
            ILogger<DatabaseInitializer> logger)
        {
            _context = context;
            _nguoiDungService = nguoiDungService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _context.Database.MigrateAsync();
                await _nguoiDungService.SeedAdminAccountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Một lỗi đã xảy ra khi đang tự động tạo/cập nhật Database.");
                throw;
            }
        }
    }
}
