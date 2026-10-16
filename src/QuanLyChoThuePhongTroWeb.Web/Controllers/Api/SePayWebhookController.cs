using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Controllers.Api;

// Webhook SePay báo tiền vào tài khoản (spec thanh toán trực tuyến §4.1). Ẩn danh nhưng bắt buộc chữ ký
// hoặc API key. Không dùng [ApiController] và model binding vì phải đọc body thô để kiểm chữ ký trước khi parse.
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/webhooks/payment/sepay")]
public sealed class SePayWebhookController(
    SePayOptions options,
    ISePayWebhookVerifier verifier,
    IBankTransferIngestionService ingestion,
    ILogger<SePayWebhookController> logger) : ControllerBase
{
    public const int MaxBodyBytes = 64 * 1024;

    // SePay chỉ coi webhook thành công khi nhận 200 hoặc 201 và body đúng như vậy.
    private const string SuccessBody = "{\"success\": true}";
    private const string FailureBody = "{\"success\": false}";

    [HttpPost("")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        // Tắt thì endpoint coi như không tồn tại, trước cả khi đọc body.
        if (!options.Enabled)
        {
            return NotFound();
        }

        byte[]? body;
        try
        {
            body = await ReadBodyAsync(cancellationToken);
        }
        catch (BadHttpRequestException ex)
        {
            // Kestrel từ chối body vượt giới hạn; trả đúng mã của nó thay vì để rơi vào GlobalExceptionFilter (log Error).
            logger.LogWarning("Webhook SePay: không đọc được body, mã {StatusCode}", ex.StatusCode);
            return StatusCode(ex.StatusCode);
        }

        if (body == null)
        {
            logger.LogWarning("Webhook SePay: body vượt giới hạn {MaxBodyBytes} byte", MaxBodyBytes);
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var verification = verifier.Verify(body, Request.Headers["X-SePay-Timestamp"], Request.Headers["X-SePay-Signature"], Request.Headers.Authorization);
        if (!verification.IsValid)
        {
            // Không log header, body hay secret.
            logger.LogWarning("Webhook SePay bị từ chối: {Reason}", verification.Reason);
            return Unauthorized();
        }

        if (!SePayWebhookPayloadParser.TryParse(body, out var transfer))
        {
            logger.LogWarning("Webhook SePay có payload không hợp lệ");
            return BadRequest();
        }

        var outcome = await ingestion.IngestAsync(transfer, cancellationToken);

        // Lỗi hệ thống khi ghi sổ thì trả 500 để SePay gửi lại; an toàn nhờ khóa chống trùng.
        // Mọi trường hợp còn lại (kể cả bỏ qua, trùng, không khớp) là 200 để SePay không gửi lại.
        return outcome.Kind == TransferOutcomeKind.Failed
            ? new ContentResult { StatusCode = StatusCodes.Status500InternalServerError, Content = FailureBody, ContentType = "application/json" }
            : Content(SuccessBody, "application/json");
    }

    // Trả null khi body vượt MaxBodyBytes.
    private async Task<byte[]?> ReadBodyAsync(CancellationToken cancellationToken)
    {
        if (Request.ContentLength > MaxBodyBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[8 * 1024];
        int read;
        while ((read = await Request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
