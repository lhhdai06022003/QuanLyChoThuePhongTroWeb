using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class TelegramOptionsTests
    {
        private static TelegramOptions Enabled(string defaultChatId = "-100100") => new()
        {
            Enabled = true,
            BotToken = "123456:token",
            DefaultChatId = defaultChatId
        };

        // ---------- Validate ----------

        [Fact]
        public void Defaults_AreValid_AndTelegramIsOff()
        {
            var options = new TelegramOptions();

            options.Validate();

            Assert.False(options.Enabled);
            Assert.False(options.IsConfigured);
            Assert.Empty(options.BranchChatIds);
        }

        [Fact]
        public void Disabled_WithoutAnything_IsValid_EvenWithBrokenBranchChats()
        {
            new TelegramOptions { Enabled = false, BotToken = "", DefaultChatId = "", BranchChatIds = { [0] = "" } }.Validate();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Enabled_WithoutBotToken_Throws(string token)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new TelegramOptions { Enabled = true, BotToken = token, DefaultChatId = "-100100" }.Validate());

            Assert.Contains("Telegram:BotToken", ex.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Enabled_WithoutDefaultChatId_Throws(string chatId)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new TelegramOptions { Enabled = true, BotToken = "123456:token", DefaultChatId = chatId }.Validate());

            Assert.Contains("Telegram:DefaultChatId", ex.Message);
        }

        [Fact]
        public void Enabled_WithTokenAndDefaultChat_IsValid()
        {
            Enabled().Validate();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void Enabled_BranchChatWithANonPositiveBranchId_Throws(int chiNhanhId)
        {
            var options = Enabled();
            options.BranchChatIds[chiNhanhId] = "-100200";

            var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());

            Assert.Contains("Telegram:BranchChatIds", ex.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Enabled_BranchChatWithABlankChatId_Throws(string chatId)
        {
            var options = Enabled();
            options.BranchChatIds[2] = chatId;

            var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());

            Assert.Contains("Telegram:BranchChatIds:2", ex.Message);
        }

        [Fact]
        public void Enabled_WithValidBranchChats_IsValid()
        {
            var options = Enabled();
            options.BranchChatIds[1] = "-100200";
            options.BranchChatIds[2] = "-100300";

            options.Validate();
        }

        // ---------- IsConfigured ----------

        [Theory]
        [InlineData(true, "123456:token", "-100100", true)]
        [InlineData(false, "123456:token", "-100100", false)]
        [InlineData(true, "", "-100100", false)]
        [InlineData(true, "123456:token", "", false)]
        [InlineData(true, "  ", "-100100", false)]
        [InlineData(true, "123456:token", "  ", false)]
        public void IsConfigured_NeedsEnabledTokenAndDefaultChat(bool enabled, string token, string chatId, bool expected)
        {
            var options = new TelegramOptions { Enabled = enabled, BotToken = token, DefaultChatId = chatId };

            Assert.Equal(expected, options.IsConfigured);
        }

        // ---------- ResolveChatIds ----------

        [Fact]
        public void ResolveChatIds_NotConfigured_IsEmpty()
        {
            var options = new TelegramOptions { Enabled = false, BotToken = "123456:token", DefaultChatId = "-100100", BranchChatIds = { [1] = "-100200" } };

            Assert.Empty(options.ResolveChatIds(1));
            Assert.Empty(options.ResolveChatIds(null));
        }

        [Fact]
        public void ResolveChatIds_WithoutABranch_IsTheDefaultChatOnly()
        {
            var options = Enabled();
            options.BranchChatIds[1] = "-100200";

            Assert.Equal(new[] { "-100100" }, options.ResolveChatIds(null));
        }

        [Fact]
        public void ResolveChatIds_WithABranchChat_AddsItAfterTheDefaultChat()
        {
            var options = Enabled();
            options.BranchChatIds[1] = "-100200";

            Assert.Equal(new[] { "-100100", "-100200" }, options.ResolveChatIds(1));
        }

        [Fact]
        public void ResolveChatIds_BranchWithoutItsOwnChat_IsTheDefaultChatOnly()
        {
            var options = Enabled();
            options.BranchChatIds[1] = "-100200";

            Assert.Equal(new[] { "-100100" }, options.ResolveChatIds(2));
        }

        [Fact]
        public void ResolveChatIds_BranchChatEqualToTheDefaultChat_IsNotRepeated()
        {
            var options = Enabled();
            options.BranchChatIds[1] = " -100100 ";

            Assert.Equal(new[] { "-100100" }, options.ResolveChatIds(1));
        }

        [Fact]
        public void ResolveChatIds_TrimsBothChats()
        {
            var options = Enabled("  -100100  ");
            options.BranchChatIds[1] = "  -100200 ";

            Assert.Equal(new[] { "-100100", "-100200" }, options.ResolveChatIds(1));
        }

        [Fact]
        public void ResolveChatIds_BlankBranchChat_IsIgnored()
        {
            var options = Enabled();
            options.BranchChatIds[1] = "   ";

            Assert.Equal(new[] { "-100100" }, options.ResolveChatIds(1));
        }
    }
}
