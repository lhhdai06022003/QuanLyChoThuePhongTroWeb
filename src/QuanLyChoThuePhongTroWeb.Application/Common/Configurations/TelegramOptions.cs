using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    // Tin Telegram về thu tiền và cảnh báo chuyển khoản cần xử lý tay (spec thanh toán trực tuyến §6.2).
    public class TelegramOptions
    {
        public const string SectionName = "Telegram";

        public bool Enabled { get; set; }
        public string BotToken { get; set; } = string.Empty;
        // Chat (nhóm) chung của chủ trọ: nhận mọi tin.
        public string DefaultChatId { get; set; } = string.Empty;
        // ChiNhanhId -> ChatId của chi nhánh đó, tùy chọn.
        public Dictionary<int, string> BranchChatIds { get; set; } = new();

        // Bật và đủ BotToken, DefaultChatId thì mới gửi tin.
        public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(DefaultChatId);

        // Chưa cấu hình thì rỗng. Luôn có DefaultChatId; thêm chat riêng của chi nhánh nếu có khai báo, không trống
        // và khác chat chung (so sau khi cắt khoảng trắng). Không trả chat trùng lặp.
        public IReadOnlyList<string> ResolveChatIds(int? chiNhanhId)
        {
            if (!IsConfigured)
            {
                return System.Array.Empty<string>();
            }

            var defaultChatId = DefaultChatId.Trim();
            var chatIds = new List<string> { defaultChatId };

            if (chiNhanhId.HasValue &&
                BranchChatIds.TryGetValue(chiNhanhId.Value, out var branchChatId) &&
                !string.IsNullOrWhiteSpace(branchChatId))
            {
                var trimmed = branchChatId.Trim();
                if (!string.Equals(trimmed, defaultChatId, System.StringComparison.Ordinal))
                {
                    chatIds.Add(trimmed);
                }
            }

            return chatIds;
        }

        public void Validate()
        {
            if (!Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(BotToken))
            {
                throw new System.InvalidOperationException("Cấu hình Telegram:BotToken không được để trống khi bật Telegram.");
            }

            if (string.IsNullOrWhiteSpace(DefaultChatId))
            {
                throw new System.InvalidOperationException("Cấu hình Telegram:DefaultChatId không được để trống khi bật Telegram.");
            }

            foreach (var (chiNhanhId, chatId) in BranchChatIds)
            {
                if (chiNhanhId <= 0)
                {
                    throw new System.InvalidOperationException("Cấu hình Telegram:BranchChatIds phải dùng mã chi nhánh lớn hơn 0 làm khóa.");
                }

                if (string.IsNullOrWhiteSpace(chatId))
                {
                    throw new System.InvalidOperationException($"Cấu hình Telegram:BranchChatIds:{chiNhanhId} không được để trống.");
                }
            }
        }
    }
}
