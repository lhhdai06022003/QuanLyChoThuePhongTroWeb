using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeAiChatModel : IAiChatModel
    {
        public List<AiModelRequest> Requests { get; } = new();

        // Tham số int là lần gọi, bắt đầu từ 1.
        public Func<AiModelRequest, int, AiModelResult> Responder { get; set; } = (_, _) => AiModelResult.Ok("Xin chào");

        public Task<AiModelResult> GenerateAsync(AiModelRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Responder(request, Requests.Count));
        }

        public const string RawModelContent = "{\"role\":\"model\",\"parts\":[{\"raw\":true}]}";

        public static AiModelResult CallTools(params AiFunctionCall[] calls)
        {
            var turn = new AiTurn(
                AiTurnRole.Model,
                calls.Select(c => (AiPart)new AiFunctionCallPart(c)).ToList(),
                RawModelContent);
            return AiModelResult.Ok(null, calls, turn);
        }
    }
}
