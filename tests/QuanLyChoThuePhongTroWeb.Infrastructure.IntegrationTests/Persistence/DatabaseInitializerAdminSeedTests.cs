using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using QuanLyChoThuePhongTroWeb.Infrastructure.Security;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Tạo admin lần đầu qua DatabaseInitializer (spec bảo mật xác thực §2.1). DB test có thể đã có "admin" từ các lượt cũ:
    // test tạm xóa mềm các bản ghi đó rồi khôi phục, và xóa hẳn bản ghi admin do chính nó tạo.
    [Collection("PostgreSqlCollection")]
    public class DatabaseInitializerAdminSeedTests
    {
        private readonly PostgreSqlFixture _fixture;

        public DatabaseInitializerAdminSeedTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task NoAdmin_WithoutPassword_DoesNotCreateAdmin_AndLogsWarning()
        {
            await WithoutExistingAdminsAsync(async context =>
            {
                var logger = new ListLogger();

                await NewInitializer(context, null, logger).InitializeAsync();

                Assert.False(await context.NguoiDungs.AnyAsync(x => x.TenDangNhap == "admin" && !x.IsDeleted));
                var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
                Assert.Contains("AdminSeed__Password", warning.Message);
                Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);
            });
        }

        [Fact]
        public async Task NoAdmin_WithPassword_CreatesAdmin_ThatCanLogIn()
        {
            const string password = "Admin seed test 2026";

            await WithoutExistingAdminsAsync(async context =>
            {
                var logger = new ListLogger();

                await NewInitializer(context, password, logger).InitializeAsync();

                var admin = await context.NguoiDungs.AsNoTracking().SingleAsync(x => x.TenDangNhap == "admin" && !x.IsDeleted);
                Assert.Equal(Role.Admin, admin.Role);
                Assert.True(admin.IsActive);
                Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);

                var login = await NewService(context, password).LoginAsync("admin", password, null);
                Assert.Equal(LoginStatus.ThanhCong, login.Status);
                Assert.False(login.User!.BatBuocDoiMatKhau);
            });
        }

        [Fact]
        public async Task ExistingAdmin_IsLeftUntouched()
        {
            await WithoutExistingAdminsAsync(async context =>
            {
                var hasher = new AspNetPasswordService(new PasswordHasher<NguoiDung>());
                var existing = new NguoiDung
                {
                    TenDangNhap = "admin",
                    MatKhauHash = hasher.HashPassword("Mat khau cu 2025"),
                    Role = Role.Admin,
                    IsActive = true,
                    NgayTao = DateTime.UtcNow
                };
                context.NguoiDungs.Add(existing);
                await context.SaveChangesAsync();
                var hashBefore = existing.MatKhauHash;

                await NewInitializer(context, "Mat khau moi 2026", new ListLogger()).InitializeAsync();

                var admins = await context.NguoiDungs.AsNoTracking().Where(x => x.TenDangNhap == "admin" && !x.IsDeleted).ToListAsync();
                var admin = Assert.Single(admins);
                Assert.Equal(existing.NguoiDungId, admin.NguoiDungId);
                Assert.Equal(hashBefore, admin.MatKhauHash);
            });
        }

        private static NguoiDungService NewService(ApplicationDbContext context, string? adminSeedPassword) =>
            new(
                new NguoiDungStore(context),
                new EfUnitOfWork(context),
                new AspNetPasswordService(new PasswordHasher<NguoiDung>()),
                new AdminSeedOptions { Password = adminSeedPassword },
                new InMemoryLoginAttemptThrottle(new LoginThrottleOptions(), TimeProvider.System, NullLogger<InMemoryLoginAttemptThrottle>.Instance));

        private static DatabaseInitializer NewInitializer(ApplicationDbContext context, string? adminSeedPassword, ILogger<DatabaseInitializer> logger) =>
            new(context, NewService(context, adminSeedPassword), logger);

        private async Task WithoutExistingAdminsAsync(Func<ApplicationDbContext, Task> body)
        {
            List<int> hidden;
            List<int> preExisting;
            await using (var setup = _fixture.CreateDbContext())
            {
                preExisting = await setup.NguoiDungs.Where(x => x.TenDangNhap == "admin").Select(x => x.NguoiDungId).ToListAsync();
                var admins = await setup.NguoiDungs.Where(x => x.TenDangNhap == "admin" && !x.IsDeleted).ToListAsync();
                hidden = admins.Select(x => x.NguoiDungId).ToList();
                foreach (var admin in admins)
                {
                    admin.IsDeleted = true;
                }
                await setup.SaveChangesAsync();
            }

            try
            {
                await using var context = _fixture.CreateDbContext();
                await body(context);
            }
            finally
            {
                await using var cleanup = _fixture.CreateDbContext();
                var created = await cleanup.NguoiDungs
                    .Where(x => x.TenDangNhap == "admin" && !preExisting.Contains(x.NguoiDungId))
                    .ToListAsync();
                cleanup.NguoiDungs.RemoveRange(created);

                var restored = await cleanup.NguoiDungs.Where(x => hidden.Contains(x.NguoiDungId)).ToListAsync();
                foreach (var admin in restored)
                {
                    admin.IsDeleted = false;
                }
                await cleanup.SaveChangesAsync();
            }
        }

        private sealed class ListLogger : ILogger<DatabaseInitializer>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
