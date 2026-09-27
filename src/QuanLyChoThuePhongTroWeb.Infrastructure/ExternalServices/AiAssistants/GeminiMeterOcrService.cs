using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants
{
    public class GeminiMeterOcrService : IMeterOcrService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public GeminiMeterOcrService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
            _model = configuration["Gemini:Model"] ?? "gemini-1.5-flash";
        }

        public async Task<MeterOcrResult> ProcessImageAsync(byte[] imageBytes, string contentType, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return MeterOcrResult.Failed("Dịch vụ nhận diện chưa được cấu hình.");
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                return MeterOcrResult.Failed("Dữ liệu ảnh trống.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var loaiText = loaiDongHo == LoaiDongHo.Dien ? "đồng hồ điện" : "đồng hồ nước";
            var prompt = $@"Bạn là trợ lý AI chuyên đọc chỉ số từ ảnh công tơ / {loaiText}.
Hãy phân tích hình ảnh được cung cấp và trích xuất chỉ số hiển thị.
Yêu cầu phản hồi DUY NHẤT một chuỗi JSON hợp lệ với cấu trúc sau:
Nếu đọc được chỉ số rõ ràng:
{{
  ""status"": ""readable"",
  ""suggested_value"": <số thập phân chỉ số mới nhất hiển thị trên mặt số, không âm, tối đa 3 chữ số thập phân>,
  ""confidence"": <độ tin cậy từ 0.0 đến 1.0>
}}
Nếu ảnh mờ, chói, góc chụp không thấy mặt số hoặc không thể đọc được:
{{
  ""status"": ""unreadable"",
  ""reason"": ""<lý do ngắn gọn vì sao không đọc được>""
}}";

            var base64Data = Convert.ToBase64String(imageBytes);

            var requestPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType,
                                    data = base64Data
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json"
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Headers.Add("x-goog-api-key", _apiKey);
                httpRequest.Content = new StringContent(JsonSerializer.Serialize(requestPayload, JsonOptions), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return MeterOcrResult.Failed($"Lỗi kết nối Gemini API (HTTP {(int)response.StatusCode}).");
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JsonNode.Parse(responseBody);
                var text = root?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (string.IsNullOrWhiteSpace(text))
                {
                    return MeterOcrResult.Failed("Phản hồi từ AI không có nội dung.");
                }

                return ParseGeminiResponse(text);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return MeterOcrResult.Failed("Lỗi kết nối dịch vụ AI hoặc nhận diện chỉ số.");
            }
        }

        private static MeterOcrResult ParseGeminiResponse(string rawText)
        {
            var text = rawText.Trim();
            if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(7);
            }
            else if (text.StartsWith("```"))
            {
                text = text.Substring(3);
            }

            if (text.EndsWith("```"))
            {
                text = text.Substring(0, text.Length - 3);
            }
            text = text.Trim();

            try
            {
                var doc = JsonNode.Parse(text);
                if (doc == null)
                {
                    return MeterOcrResult.Failed("Không thể phân tích dữ liệu JSON từ AI.");
                }

                var status = doc["status"]?.ToString()?.ToLowerInvariant();
                if (status == "readable")
                {
                    var valNode = doc["suggested_value"];
                    if (valNode == null)
                    {
                        return MeterOcrResult.Failed("Phản hồi AI thiếu giá trị gợi ý.");
                    }

                    if (!decimal.TryParse(valNode.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var suggestedValue))
                    {
                        return MeterOcrResult.Failed("Giá trị gợi ý từ AI không phải số hợp lệ.");
                    }

                    if (suggestedValue < 0)
                    {
                        return MeterOcrResult.Failed("Giá trị gợi ý từ AI không được âm.");
                    }

                    var bits = decimal.GetBits(suggestedValue);
                    var scale = (bits[3] >> 16) & 0x7F;
                    if (scale > 3)
                    {
                        return MeterOcrResult.Failed("Giá trị gợi ý từ AI có quá 3 chữ số thập phân.");
                    }

                    double? confidence = null;
                    var confNode = doc["confidence"];
                    if (confNode != null)
                    {
                        if (double.TryParse(confNode.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var confVal))
                        {
                            if (confVal < 0 || confVal > 1)
                            {
                                return MeterOcrResult.Failed("Độ tin cậy từ AI nằm ngoài khoảng 0 đến 1.");
                            }
                            confidence = confVal;
                        }
                    }

                    return MeterOcrResult.Readable(suggestedValue, confidence);
                }
                else if (status == "unreadable")
                {
                    var reason = doc["reason"]?.ToString() ?? "Không thể đọc chỉ số trên đồng hồ.";
                    if (reason.Length > 500)
                    {
                        reason = reason.Substring(0, 500);
                    }
                    return MeterOcrResult.Unreadable(reason);
                }

                return MeterOcrResult.Failed($"Trạng thái nhận diện không hợp lệ từ AI: {status}");
            }
            catch (Exception)
            {
                return MeterOcrResult.Failed("Không thể phân tích phản hồi từ AI.");
            }
        }
    }
}
