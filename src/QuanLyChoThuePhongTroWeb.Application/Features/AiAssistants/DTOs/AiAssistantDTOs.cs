using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs
{
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatMessageDto> History { get; set; } = new();
    }

    public class ChatMessageDto
    {
        public string Role { get; set; } = string.Empty; // "user" hoặc "model"
        public string Message { get; set; } = string.Empty;
    }
}
