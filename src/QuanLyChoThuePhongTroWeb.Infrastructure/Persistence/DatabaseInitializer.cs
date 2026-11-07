using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
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
                var seedResult = await _nguoiDungService.SeedAdminAccountAsync();
                if (seedResult == SeedAdminResult.ThieuMatKhau)
                {
                    // Warning, không phải Error: thiếu admin không chặn ứng dụng chạy (spec bảo mật xác thực §2.1).
                    _logger.LogWarning("Chưa có tài khoản admin và chưa cấu hình AdminSeed:Password (biến môi trường AdminSeed__Password). Đặt giá trị rồi khởi động lại để tạo admin.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Một lỗi đã xảy ra khi đang tự động tạo/cập nhật Database.");
                throw;
            }
        }
    }
}
