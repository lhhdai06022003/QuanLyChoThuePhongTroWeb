using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QuanLyChoThuePhongTroWeb.Web.Helpers;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Không cần DB. Khóa Data Protection phải nằm ở DataProtection:KeysPath khi có cấu hình (deploy container
    // không làm mất phiên đăng nhập), và giữ mặc định khi không cấu hình.
    public class DataProtectionKeyStorageTests
    {
        private static ServiceProvider Build(string? keysPath)
        {
            var values = new Dictionary<string, string?>();
            if (keysPath != null)
            {
                values[DataProtectionKeyStorage.KeysPathKey] = keysPath;
            }
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddPersistentDataProtectionKeys(configuration);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void WithKeysPath_WritesKeyToThatFolder_AndAnotherInstanceCanUnprotect()
        {
            var dir = Path.Combine(Path.GetTempPath(), "qlctpt-dp-" + Guid.NewGuid().ToString("N"), "keys");
            try
            {
                string token;
                using (var first = Build(dir))
                {
                    token = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("phien-dang-nhap");
                }

                Assert.NotEmpty(Directory.GetFiles(dir, "key-*.xml"));

                // Mô phỏng container khởi động lại: provider mới đọc khóa từ cùng thư mục.
                using var second = Build(dir);
                var plain = second.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Unprotect(token);
                Assert.Equal("phien-dang-nhap", plain);
            }
            finally
            {
                var root = Directory.GetParent(dir)!.FullName;
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithoutKeysPath_KeepsDefaultRepository(string? keysPath)
        {
            using var provider = Build(keysPath);

            var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
            Assert.Null(options.XmlRepository);
        }
    }
}
