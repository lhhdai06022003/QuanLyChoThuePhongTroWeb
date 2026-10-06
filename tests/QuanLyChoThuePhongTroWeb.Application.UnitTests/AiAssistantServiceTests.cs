using System.Text.Json;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Tools;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class AiAssistantServiceTests
    {
        private readonly FakeAiChatModel _model = new();
        private readonly FakeAiAssistantStore _store = new();
        private readonly FakeEmployeeAccessService _access = new();
        private readonly AiAssistantService _service;

        public AiAssistantServiceTests()
        {
            var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-09-30T18:00:00Z"));
            var executor = new AiToolExecutor(_store, NullLogger<AiToolExecutor>.Instance, time);
            _service = new AiAssistantService(_model, executor, _access, NullLogger<AiAssistantService>.Instance, time);
            _access.ScopeToReturn = new EmployeeAccessScope(1, true);
        }

        private static ChatRequest Ask(string message, params (string Role, string Message)[] history)
            => new()
            {
                Message = message,
                History = history.Select(h => new ChatMessageDto { Role = h.Role, Message = h.Message }).ToList()
            };

        private static AiFunctionCall Call(string name, string json = "{}", string? id = null) => new(name, json, id);

        private static string[] ToolNames(AiModelRequest r) => r.Tools.Select(t => t.Name).ToArray();

        [Fact]
        public async Task Admin_AndAssignedStaff_GetNineManagerTools()
        {
            await _service.ChatQuanLyAsync(1, Ask("xin chào"));

            _access.ScopeToReturn = new EmployeeAccessScope(2, false, new[] { 10 });
            await _service.ChatQuanLyAsync(2, Ask("xin chào"));

            Assert.Equal(2, _model.Requests.Count);
            Assert.All(_model.Requests, r =>
            {
                Assert.Equal(9, r.Tools.Count);
                Assert.Contains(AiToolNames.PhongTrong, ToolNames(r));
                Assert.DoesNotContain(AiToolNames.MyHopDongInfo, ToolNames(r));
            });
        }

        [Fact]
        public async Task Tenant_GetsOnlyThreeTenantTools()
        {
            await _service.ChatKhachThueAsync(5, 55, Ask("xin chào"));

            var names = ToolNames(_model.Requests.Single());
            Assert.Equal(
                new[] { AiToolNames.MyHopDongInfo, AiToolNames.MyChiSoDienNuoc, AiToolNames.MyHoaDonChuaThanhToan }.OrderBy(x => x),
                names.OrderBy(x => x));
        }

        [Fact]
        public async Task Staff_WithoutAssignedBranch_GetsFixedMessage_AndModelIsNotCalled()
        {
            _access.ScopeToReturn = new EmployeeAccessScope(2, false);

            var result = await _service.ChatQuanLyAsync(2, Ask("xin chào"));

            Assert.True(result.Success);
            Assert.Equal("Bạn chưa được phân công chi nhánh nào.", result.Message);
            Assert.Empty(_model.Requests);
        }

        [Fact]
        public async Task NullScope_IsForbidden_AndModelIsNotCalled()
        {
            _access.ScopeToReturn = null;

            var result = await _service.ChatQuanLyAsync(2, Ask("xin chào"));

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Empty(_model.Requests);
        }

        [Fact]
        public async Task Tenant_WithoutNguoiThueId_IsForbidden()
        {
            var result = await _service.ChatKhachThueAsync(5, 0, Ask("xin chào"));

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Empty(_model.Requests);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task EmptyMessage_Fails_WithoutCallingModel(string message)
        {
            var result = await _service.ChatQuanLyAsync(1, Ask(message));

            Assert.False(result.Success);
            Assert.Equal("Nội dung tin nhắn không được trống.", result.Message);
            Assert.Empty(_model.Requests);
        }

        [Fact]
        public async Task NullRequest_Fails_WithoutCallingModel()
        {
            var result = await _service.ChatQuanLyAsync(1, null!);

            Assert.False(result.Success);
            Assert.Empty(_model.Requests);
        }

        [Fact]
        public async Task MessageLength_1001Fails_1000Passes()
        {
            var tooLong = await _service.ChatQuanLyAsync(1, Ask(new string('a', 1001)));
            Assert.False(tooLong.Success);
            Assert.Equal("Câu hỏi tối đa 1.000 ký tự.", tooLong.Message);
            Assert.Empty(_model.Requests);

            var ok = await _service.ChatQuanLyAsync(1, Ask(new string('a', 1000)));
            Assert.True(ok.Success);
            Assert.Single(_model.Requests);
        }

        [Fact]
        public async Task History_NullIsAccepted()
        {
            var request = new ChatRequest { Message = "xin chào", History = null! };

            var result = await _service.ChatQuanLyAsync(1, request);

            Assert.True(result.Success);
            Assert.Single(_model.Requests.Single().Turns);
        }

        [Fact]
        public async Task History_FiltersInvalidItems_KeepsLastTenInOrder()
        {
            var history = new List<(string Role, string Message)>
            {
                ("system", "bỏ"), ("function", "bỏ"), ("User", "bỏ"), ("user", "  "), ("model", "")
            };
            for (var i = 1; i <= 12; i++)
            {
                history.Add((i % 2 == 1 ? "user" : "model", $"m{i}"));
            }
            var request = Ask("câu hỏi nay", history.ToArray());
            request.History.Add(null!);

            await _service.ChatQuanLyAsync(1, request);

            var turns = _model.Requests.Single().Turns;
            Assert.Equal(11, turns.Count);
            var texts = turns.Select(t => ((AiTextPart)t.Parts.Single()).Text).ToList();
            Assert.Equal(Enumerable.Range(3, 10).Select(i => $"m{i}").Append("câu hỏi nay"), texts);
            Assert.Equal(AiTurnRole.User, turns[^1].Role);
            Assert.Equal(AiTurnRole.User, turns[0].Role);
            Assert.Equal(AiTurnRole.Model, turns[1].Role);
        }

        [Fact]
        public async Task History_TruncatesOverlongItems_ByRole()
        {
            var request = Ask("câu hỏi nay",
                ("user", new string('u', 5000)),
                ("model", new string('m', 10000)),
                ("user", "ngắn"));

            await _service.ChatQuanLyAsync(1, request);

            var texts = _model.Requests.Single().Turns.Select(t => ((AiTextPart)t.Parts.Single()).Text).ToList();
            Assert.Equal(new string('u', 1000), texts[0]);
            Assert.Equal(new string('m', 4000), texts[1]);
            Assert.Equal("ngắn", texts[2]);
            Assert.Equal("câu hỏi nay", texts[3]);
        }

        [Fact]
        public async Task SingleToolCall_ThenText_CallsModelTwice_AndReturnsFunctionResponse()
        {
            _model.Responder = (_, n) => n == 1
                ? FakeAiChatModel.CallTools(Call(AiToolNames.PhongTrong, "{}", "call-1"))
                : AiModelResult.Ok("Có 0 phòng trống");

            var result = await _service.ChatQuanLyAsync(1, Ask("phòng trống?"));

            Assert.True(result.Success);
            Assert.Equal("Có 0 phòng trống", result.Message);
            Assert.Equal(2, _model.Requests.Count);

            var turns = _model.Requests[1].Turns;
            Assert.Equal(FakeAiChatModel.RawModelContent, turns[^2].RawProviderContent);
            var response = Assert.IsType<AiFunctionResponsePart>(Assert.Single(turns[^1].Parts));
            Assert.Equal(AiTurnRole.User, turns[^1].Role);
            Assert.Equal(AiToolNames.PhongTrong, response.Name);
            Assert.Equal("call-1", response.CallId);
            Assert.True(JsonDocument.Parse(response.ResponseJson).RootElement.GetProperty("thanhCong").GetBoolean());
        }

        [Fact]
        public async Task ThreeCallsInOneTurn_AllRun_ResponsesKeepOrder()
        {
            _model.Responder = (_, n) => n == 1
                ? FakeAiChatModel.CallTools(
                    Call(AiToolNames.PhongTrong, "{}", "a"),
                    Call(AiToolNames.DoanhThuThucThu, "{}", "b"),
                    Call(AiToolNames.HopDongSapHetHan, "{}", "c"))
                : AiModelResult.Ok("xong");

            await _service.ChatQuanLyAsync(1, Ask("tổng hợp"));

            var parts = _model.Requests[1].Turns[^1].Parts.Cast<AiFunctionResponsePart>().ToList();
            Assert.Equal(new[] { "a", "b", "c" }, parts.Select(p => p.CallId));
            Assert.Equal(
                new[] { AiToolNames.PhongTrong, AiToolNames.DoanhThuThucThu, AiToolNames.HopDongSapHetHan },
                parts.Select(p => p.Name));
            Assert.Equal(3, _store.Calls.Count);
        }

        [Fact]
        public async Task SixCallsInOneTurn_OnlyFiveRun_SixthIsRateLimited()
        {
            _model.Responder = (_, n) => n == 1
                ? FakeAiChatModel.CallTools(Enumerable.Range(1, 6).Select(i => Call(AiToolNames.PhongTrong, "{}", $"c{i}")).ToArray())
                : AiModelResult.Ok("xong");

            await _service.ChatQuanLyAsync(1, Ask("nhiều"));

            Assert.Equal(5, _store.Calls.Count);
            var parts = _model.Requests[1].Turns[^1].Parts.Cast<AiFunctionResponsePart>().ToList();
            Assert.Equal(6, parts.Count);
            Assert.Equal("VUOT_GIOI_HAN", JsonDocument.Parse(parts[5].ResponseJson).RootElement.GetProperty("maLoi").GetString());
            Assert.True(JsonDocument.Parse(parts[4].ResponseJson).RootElement.GetProperty("thanhCong").GetBoolean());
        }

        [Fact]
        public async Task ModelAlwaysCallsTools_StopsAtFourModelCalls_WithFinalNoToolRequest()
        {
            _model.Responder = (_, _) => FakeAiChatModel.CallTools(Call(AiToolNames.PhongTrong));

            var result = await _service.ChatQuanLyAsync(1, Ask("lặp mãi"));

            Assert.Equal(4, _model.Requests.Count);
            Assert.All(_model.Requests.Take(3), r => Assert.True(r.AllowToolCalls));
            Assert.False(_model.Requests[3].AllowToolCalls);
            Assert.Contains(AiPrompts.HetLuotCongCu, _model.Requests[3].SystemInstruction);
            Assert.True(result.Success);
            Assert.StartsWith("Tôi chưa thể hoàn tất", result.Message);
            Assert.Equal(3, _store.Calls.Count);
        }

        [Fact]
        public async Task FinalRequest_WithText_IsReturned()
        {
            _model.Responder = (_, n) => n < 4
                ? FakeAiChatModel.CallTools(Call(AiToolNames.PhongTrong))
                : AiModelResult.Ok("Trả lời bằng dữ liệu đã có");

            var result = await _service.ChatQuanLyAsync(1, Ask("lặp"));

            Assert.Equal("Trả lời bằng dữ liệu đã có", result.Message);
        }

        [Fact]
        public async Task Tenant_ModelCallsManagerTool_IsRejected_StoreUntouched()
        {
            _model.Responder = (_, n) => n == 1
                ? FakeAiChatModel.CallTools(Call(AiToolNames.PhongTrong))
                : AiModelResult.Ok("xong");

            await _service.ChatKhachThueAsync(5, 55, Ask("phòng trống?"));

            Assert.Empty(_store.Calls);
            var response = (AiFunctionResponsePart)_model.Requests[1].Turns[^1].Parts.Single();
            Assert.Equal("CONG_CU_KHONG_KHA_DUNG", JsonDocument.Parse(response.ResponseJson).RootElement.GetProperty("maLoi").GetString());
        }

        [Fact]
        public async Task Manager_ModelCallsTenantTool_IsRejected_StoreUntouched()
        {
            _model.Responder = (_, n) => n == 1
                ? FakeAiChatModel.CallTools(Call(AiToolNames.MyHoaDonChuaThanhToan))
                : AiModelResult.Ok("xong");

            await _service.ChatQuanLyAsync(1, Ask("hóa đơn của tôi"));

            Assert.Empty(_store.Calls);
            var response = (AiFunctionResponsePart)_model.Requests[1].Turns[^1].Parts.Single();
            Assert.Equal("CONG_CU_KHONG_KHA_DUNG", JsonDocument.Parse(response.ResponseJson).RootElement.GetProperty("maLoi").GetString());
        }

        [Fact]
        public async Task ModelErrors_MapToVietnameseMessages()
        {
            _model.Responder = (_, _) => AiModelResult.Fail(AiModelErrorKind.NotConfigured);
            var notConfigured = await _service.ChatQuanLyAsync(1, Ask("a"));
            Assert.False(notConfigured.Success);
            Assert.Contains("Lỗi cấu hình", notConfigured.Message);

            _model.Responder = (_, _) => AiModelResult.Fail(AiModelErrorKind.HttpError, 400);
            var http = await _service.ChatQuanLyAsync(1, Ask("a"));
            Assert.Equal("Trợ lý AI tạm thời không phản hồi (HTTP 400). Vui lòng thử lại sau.", http.Message);

            _model.Responder = (_, _) => AiModelResult.Fail(AiModelErrorKind.Unavailable);
            var unavailable = await _service.ChatQuanLyAsync(1, Ask("a"));
            Assert.Equal("Trợ lý AI tạm thời không phản hồi. Vui lòng thử lại sau.", unavailable.Message);

            _model.Responder = (_, _) => AiModelResult.Fail(AiModelErrorKind.EmptyResponse);
            var empty = await _service.ChatQuanLyAsync(1, Ask("a"));
            Assert.Equal("Trợ lý AI tạm thời không phản hồi. Vui lòng thử lại sau.", empty.Message);
        }

        [Fact]
        public async Task ModelError_OnSecondCall_IsMappedToo()
        {
            _model.Responder = (_, n) => n == 1
                ? FakeAiChatModel.CallTools(Call(AiToolNames.PhongTrong))
                : AiModelResult.Fail(AiModelErrorKind.HttpError, 400);

            var result = await _service.ChatQuanLyAsync(1, Ask("a"));

            Assert.False(result.Success);
            Assert.Equal("Trợ lý AI tạm thời không phản hồi (HTTP 400). Vui lòng thử lại sau.", result.Message);
        }

        [Fact]
        public async Task Prompts_ContainRoleSuggestionsAndVietnamDate()
        {
            await _service.ChatQuanLyAsync(1, Ask("a"));
            await _service.ChatKhachThueAsync(5, 55, Ask("a"));

            var manager = _model.Requests[0].SystemInstruction;
            Assert.Contains("Tôi chưa hỗ trợ câu hỏi này", manager);
            Assert.Contains("01/10/2026", manager);
            foreach (var s in AiPrompts.Suggestions(AiCallerRole.Admin))
            {
                Assert.Contains(s, manager);
            }
            Assert.Equal(5, AiPrompts.Suggestions(AiCallerRole.NhanVien).Count);

            var tenant = _model.Requests[1].SystemInstruction;
            Assert.Equal(3, AiPrompts.Suggestions(AiCallerRole.KhachThue).Count);
            foreach (var s in AiPrompts.Suggestions(AiCallerRole.KhachThue))
            {
                Assert.Contains(s, tenant);
            }
            Assert.Contains("Không tiết lộ thông tin của người khác", tenant);
            Assert.DoesNotContain("Không tiết lộ thông tin của người khác", manager);
        }
    }
}
