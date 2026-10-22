using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel
{
    public interface IAiChatModel
    {
        Task<AiModelResult> GenerateAsync(AiModelRequest request, CancellationToken cancellationToken = default);
    }

    public enum AiTurnRole { User, Model }

    public abstract record AiPart;

    public sealed record AiTextPart(string Text) : AiPart;

    public sealed record AiFunctionCallPart(AiFunctionCall Call) : AiPart;

    // ResponseJson luôn là một JSON object.
    public sealed record AiFunctionResponsePart(string Name, string? CallId, string ResponseJson) : AiPart;

    // RawProviderContent: JSON gốc của lượt model do client trả về; khác null thì client gửi lại nguyên văn (giữ thoughtSignature của Gemini).
    public sealed record AiTurn(AiTurnRole Role, IReadOnlyList<AiPart> Parts, string? RawProviderContent = null);

    // ArgumentsJson = "{}" khi không có args.
    public sealed record AiFunctionCall(string Name, string ArgumentsJson, string? Id);

    public enum AiToolParameterType { String, Integer, Number }

    public sealed record AiToolParameter(string Name, AiToolParameterType Type, string Description, bool Required = false);

    public sealed record AiToolDefinition(string Name, string Description, IReadOnlyList<AiToolParameter> Parameters);

    public sealed record AiModelRequest(
        string SystemInstruction,
        IReadOnlyList<AiTurn> Turns,
        IReadOnlyList<AiToolDefinition> Tools,
        bool AllowToolCalls);

    public enum AiModelErrorKind { None, NotConfigured, HttpError, Unavailable, EmptyResponse }

    public sealed record AiModelResult(
        bool Success,
        AiModelErrorKind ErrorKind,
        int? HttpStatusCode,
        string? Text,
        IReadOnlyList<AiFunctionCall> FunctionCalls,
        AiTurn? ModelTurn)
    {
        public static AiModelResult Ok(string? text, IReadOnlyList<AiFunctionCall>? functionCalls = null, AiTurn? modelTurn = null)
            => new(true, AiModelErrorKind.None, null, text, functionCalls ?? new List<AiFunctionCall>(), modelTurn);

        public static AiModelResult Fail(AiModelErrorKind kind, int? httpStatusCode = null)
            => new(false, kind, httpStatusCode, null, new List<AiFunctionCall>(), null);
    }
}
