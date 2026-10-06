using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants
{
    public class GeminiChatModel : IAiChatModel
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly ILogger<GeminiChatModel> _logger;

        public GeminiChatModel(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiChatModel>? logger = null)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
            _model = configuration["Gemini:Model"] ?? "gemini-1.5-flash";
            _logger = logger ?? NullLogger<GeminiChatModel>.Instance;
        }

        public async Task<AiModelResult> GenerateAsync(AiModelRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return AiModelResult.Fail(AiModelErrorKind.NotConfigured);
            }

            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(BuildBody(request).ToJsonString(), Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("x-goog-api-key", _apiKey);

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var truncated = responseText.Length > 500 ? responseText.Substring(0, 500) : responseText;
                    _logger.LogWarning("Lỗi Gemini API (HTTP {StatusCode}): {ErrorContent}", (int)response.StatusCode, truncated);
                    return AiModelResult.Fail(AiModelErrorKind.HttpError, (int)response.StatusCode);
                }

                return ParseResponse(responseText);
            }
            catch (Exception ex) when ((ex is HttpRequestException or JsonException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Không gọi được Gemini API");
                return AiModelResult.Fail(AiModelErrorKind.Unavailable);
            }
        }

        private static JsonObject BuildBody(AiModelRequest request)
        {
            var contents = new JsonArray();
            foreach (var turn in request.Turns)
            {
                contents.Add(BuildContent(turn));
            }

            var body = new JsonObject
            {
                ["systemInstruction"] = new JsonObject
                {
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = request.SystemInstruction })
                },
                ["contents"] = contents
            };

            if (request.Tools.Count > 0)
            {
                var declarations = new JsonArray();
                foreach (var tool in request.Tools)
                {
                    var declaration = new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description
                    };

                    if (tool.Parameters.Count > 0)
                    {
                        var properties = new JsonObject();
                        foreach (var p in tool.Parameters)
                        {
                            properties[p.Name] = new JsonObject
                            {
                                ["type"] = p.Type.ToString().ToUpperInvariant(),
                                ["description"] = p.Description
                            };
                        }

                        var parameters = new JsonObject
                        {
                            ["type"] = "OBJECT",
                            ["properties"] = properties
                        };

                        var required = tool.Parameters.Where(p => p.Required).Select(p => p.Name).ToList();
                        if (required.Count > 0)
                        {
                            var requiredArray = new JsonArray();
                            foreach (var name in required) requiredArray.Add(name);
                            parameters["required"] = requiredArray;
                        }

                        declaration["parameters"] = parameters;
                    }

                    declarations.Add(declaration);
                }

                body["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = declarations });

                if (!request.AllowToolCalls)
                {
                    body["toolConfig"] = new JsonObject
                    {
                        ["functionCallingConfig"] = new JsonObject { ["mode"] = "NONE" }
                    };
                }
            }

            return body;
        }

        private static JsonNode BuildContent(AiTurn turn)
        {
            // Lượt model gốc được gửi lại nguyên văn để giữ thoughtSignature.
            if (turn.RawProviderContent != null && JsonNode.Parse(turn.RawProviderContent) is JsonObject raw)
            {
                if (raw["role"] == null)
                {
                    raw["role"] = "model";
                }
                return raw;
            }

            var parts = new JsonArray();
            foreach (var part in turn.Parts)
            {
                switch (part)
                {
                    case AiTextPart text:
                        parts.Add(new JsonObject { ["text"] = text.Text });
                        break;
                    case AiFunctionCallPart call:
                        var functionCall = new JsonObject
                        {
                            ["name"] = call.Call.Name,
                            ["args"] = JsonNode.Parse(string.IsNullOrWhiteSpace(call.Call.ArgumentsJson) ? "{}" : call.Call.ArgumentsJson)
                        };
                        if (call.Call.Id != null) functionCall["id"] = call.Call.Id;
                        parts.Add(new JsonObject { ["functionCall"] = functionCall });
                        break;
                    case AiFunctionResponsePart fr:
                        var functionResponse = new JsonObject { ["name"] = fr.Name };
                        if (fr.CallId != null) functionResponse["id"] = fr.CallId;
                        functionResponse["response"] = JsonNode.Parse(fr.ResponseJson);
                        parts.Add(new JsonObject { ["functionResponse"] = functionResponse });
                        break;
                }
            }

            return new JsonObject
            {
                ["role"] = turn.Role == AiTurnRole.Model ? "model" : "user",
                ["parts"] = parts
            };
        }

        private AiModelResult ParseResponse(string responseText)
        {
            var root = JsonNode.Parse(responseText);
            var candidate = root?["candidates"] is JsonArray { Count: > 0 } candidates ? candidates[0] : null;
            var content = candidate?["content"];
            if (content is not JsonObject contentObject)
            {
                _logger.LogWarning("Gemini không trả nội dung (finishReason: {FinishReason}, blockReason: {BlockReason})",
                    candidate?["finishReason"]?.ToString(),
                    root?["promptFeedback"]?["blockReason"]?.ToString());
                return AiModelResult.Fail(AiModelErrorKind.EmptyResponse);
            }

            var texts = new List<string>();
            var calls = new List<AiFunctionCall>();
            var turnParts = new List<AiPart>();

            if (contentObject["parts"] is JsonArray parts)
            {
                foreach (var part in parts)
                {
                    if (part is not JsonObject partObject) continue;

                    if (partObject["functionCall"] is JsonObject fc)
                    {
                        var call = new AiFunctionCall(
                            fc["name"]?.ToString() ?? string.Empty,
                            fc["args"]?.ToJsonString() ?? "{}",
                            fc["id"]?.ToString());
                        calls.Add(call);
                        turnParts.Add(new AiFunctionCallPart(call));
                    }
                    else if (partObject["text"] != null)
                    {
                        var isThought = partObject["thought"] is JsonValue v && v.TryGetValue<bool>(out var flag) && flag;
                        if (!isThought)
                        {
                            var text = partObject["text"]!.ToString();
                            texts.Add(text);
                            turnParts.Add(new AiTextPart(text));
                        }
                    }
                }
            }

            if (texts.Count == 0 && calls.Count == 0)
            {
                _logger.LogWarning("Gemini trả phản hồi không có văn bản lẫn lời gọi công cụ (finishReason: {FinishReason})",
                    candidate?["finishReason"]?.ToString());
                return AiModelResult.Fail(AiModelErrorKind.EmptyResponse);
            }

            var modelTurn = new AiTurn(AiTurnRole.Model, turnParts, contentObject.ToJsonString());
            return AiModelResult.Ok(texts.Count > 0 ? string.Concat(texts) : null, calls, modelTurn);
        }
    }
}
