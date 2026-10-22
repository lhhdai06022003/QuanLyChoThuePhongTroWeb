using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Tools;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Services
{
    public class AiAssistantService : IAiAssistantService
    {
        private const int MaxMessageLength = 1000;
        private const int MaxHistoryItems = 10;
        // Câu trả lời của model thường dài hơn câu hỏi (danh sách phòng, hóa đơn) nên được giữ nhiều ký tự hơn.
        private const int MaxHistoryModelLength = 4000;
        private const int MaxToolRounds = 3;
        private const int MaxCallsPerRound = 5;

        private readonly IAiChatModel _model;
        private readonly AiToolExecutor _executor;
        private readonly IEmployeeAccessService _access;
        private readonly ILogger<AiAssistantService> _logger;
        private readonly TimeProvider _timeProvider;

        public AiAssistantService(
            IAiChatModel model,
            AiToolExecutor executor,
            IEmployeeAccessService access,
            ILogger<AiAssistantService> logger,
            TimeProvider? timeProvider = null)
        {
            _model = model;
            _executor = executor;
            _access = access;
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public async Task<ServiceResult> ChatQuanLyAsync(int actorId, ChatRequest request, CancellationToken cancellationToken = default)
        {
            var invalid = ValidateMessage(request);
            if (invalid != null) return invalid;

            var scope = await _access.GetScopeAsync(actorId, cancellationToken);
            if (scope == null)
            {
                return ServiceResult.Forbidden("Bạn không có quyền sử dụng trợ lý AI.");
            }
            if (!scope.IsAdmin && scope.ActiveBranchIds.Count == 0)
            {
                return ServiceResult.Ok("Bạn chưa được phân công chi nhánh nào.");
            }

            var caller = new AiCallerContext(actorId, scope.IsAdmin ? AiCallerRole.Admin : AiCallerRole.NhanVien, scope.AllowedBranchIds, null);
            return await ChatAsync(caller, request, cancellationToken);
        }

        public async Task<ServiceResult> ChatKhachThueAsync(int nguoiDungId, int nguoiThueId, ChatRequest request, CancellationToken cancellationToken = default)
        {
            var invalid = ValidateMessage(request);
            if (invalid != null) return invalid;

            if (nguoiThueId <= 0)
            {
                return ServiceResult.Forbidden("Tài khoản chưa liên kết hồ sơ người thuê.");
            }

            var caller = new AiCallerContext(nguoiDungId, AiCallerRole.KhachThue, null, nguoiThueId);
            return await ChatAsync(caller, request, cancellationToken);
        }

        private static ServiceResult? ValidateMessage(ChatRequest? request)
        {
            var message = request?.Message?.Trim();
            if (string.IsNullOrEmpty(message))
            {
                return ServiceResult.Fail("Nội dung tin nhắn không được trống.");
            }
            if (message.Length > MaxMessageLength)
            {
                return ServiceResult.Fail("Câu hỏi tối đa 1.000 ký tự.");
            }
            return null;
        }

        private async Task<ServiceResult> ChatAsync(AiCallerContext caller, ChatRequest request, CancellationToken ct)
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var prompt = AiPrompts.ForRole(caller.Role, nowUtc);
            var tools = AiToolCatalog.ForRole(caller.Role);
            var turns = BuildTurns(request);

            for (var round = 1; round <= MaxToolRounds; round++)
            {
                var result = await _model.GenerateAsync(new AiModelRequest(prompt, turns, tools, true), ct);
                if (!result.Success) return MapError(result);

                if (result.FunctionCalls.Count == 0)
                {
                    return ServiceResult.Ok(result.Text ?? string.Empty);
                }

                turns = new List<AiTurn>(turns) { result.ModelTurn ?? BuildModelTurn(result) };

                var responses = new List<AiPart>();
                for (var i = 0; i < result.FunctionCalls.Count; i++)
                {
                    var call = result.FunctionCalls[i];
                    var toolResult = i < MaxCallsPerRound
                        ? await _executor.ExecuteAsync(caller, call, ct)
                        : AiToolResult.Error("VUOT_GIOI_HAN", $"Đã vượt quá {MaxCallsPerRound} lời gọi công cụ trong một lượt; yêu cầu này chưa được xử lý.");
                    responses.Add(new AiFunctionResponsePart(call.Name, call.Id, toolResult.Json));
                }
                turns.Add(new AiTurn(AiTurnRole.User, responses));
            }

            // Hết lượt công cụ: gọi lần cuối không cho gọi công cụ để model trả lời bằng dữ liệu đã có.
            var final = await _model.GenerateAsync(new AiModelRequest(prompt + "\n" + AiPrompts.HetLuotCongCu, turns, tools, false), ct);
            if (!final.Success) return MapError(final);

            if (!string.IsNullOrWhiteSpace(final.Text))
            {
                return ServiceResult.Ok(final.Text);
            }

            return ServiceResult.Ok("Tôi chưa thể hoàn tất câu trả lời vì câu hỏi cần quá nhiều bước tra cứu. Bạn hãy tách thành câu hỏi nhỏ hơn.");
        }

        private static List<AiTurn> BuildTurns(ChatRequest request)
        {
            var turns = new List<AiTurn>();
            var history = (request.History ?? new List<ChatMessageDto>())
                .Where(h => h != null
                            && (h.Role == "user" || h.Role == "model")
                            && !string.IsNullOrWhiteSpace(h.Message))
                .TakeLast(MaxHistoryItems);

            foreach (var item in history)
            {
                var role = item.Role == "user" ? AiTurnRole.User : AiTurnRole.Model;
                var maxLength = role == AiTurnRole.User ? MaxMessageLength : MaxHistoryModelLength;
                var text = item.Message.Length > maxLength ? item.Message[..maxLength] : item.Message;
                turns.Add(new AiTurn(role, new AiPart[] { new AiTextPart(text) }));
            }

            turns.Add(new AiTurn(AiTurnRole.User, new AiPart[] { new AiTextPart(request.Message.Trim()) }));
            return turns;
        }

        // Dự phòng khi model không trả ModelTurn: dựng lượt model từ các lời gọi công cụ.
        private static AiTurn BuildModelTurn(AiModelResult result)
            => new(AiTurnRole.Model, result.FunctionCalls.Select(c => (AiPart)new AiFunctionCallPart(c)).ToList());

        private ServiceResult MapError(AiModelResult result)
        {
            switch (result.ErrorKind)
            {
                case AiModelErrorKind.NotConfigured:
                    return ServiceResult.Fail("Lỗi cấu hình: Chưa thiết lập Gemini API Key trong hệ thống.");
                case AiModelErrorKind.HttpError:
                    return ServiceResult.Fail($"Trợ lý AI tạm thời không phản hồi (HTTP {result.HttpStatusCode}). Vui lòng thử lại sau.");
                default:
                    return ServiceResult.Fail("Trợ lý AI tạm thời không phản hồi. Vui lòng thử lại sau.");
            }
        }
    }
}
